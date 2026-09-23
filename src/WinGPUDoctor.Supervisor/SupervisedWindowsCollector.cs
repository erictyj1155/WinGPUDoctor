using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;

namespace WinGPUDoctor.Supervisor;

public sealed class SupervisedCollectionException : Exception
{
    public string Code { get; }
    public SupervisedCollectionException(string code) : base("Supervised collection stopped safely.")
    {
        Code = code;
    }
}

public sealed class SupervisedWindowsCollector
{
    private readonly CollectionTimingPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly IWorkerSessionFactory _sessionFactory;
    private readonly HostAdmission _admission;
    private readonly ICollectionTimingSink _timing;
    private readonly Action<CollectionProgress>? _progress;

    internal SupervisedWindowsCollector(CollectionTimingPolicy? policy = null, TimeProvider? clock = null,
        IWorkerSessionFactory? sessionFactory = null, HostAdmission? admission = null,
        ICollectionTimingSink? timing = null, Action<CollectionProgress>? progress = null)
    {
        _policy = policy ?? CollectionTimingPolicy.CalibratedProduction;
        _clock = clock ?? TimeProvider.System;
        _timing = timing ?? NullCollectionTimingSink.Instance;
        _sessionFactory = sessionFactory ?? new NativeWorkerSessionFactory(_timing);
        _admission = admission ?? HostAdmission.Process;
        _progress = progress;
    }

    public static SupervisedWindowsCollector CreateLocal() => new();

    public async Task<CollectionSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        var supervisor = new CollectionSupervisor(_policy, _clock, _sessionFactory, _admission, _timing, _progress);
        supervisor.Begin(5);

        var operatingSystem = await RunAsync(supervisor, WorkerOperation.WmiOperatingSystem, null, cancellationToken)
            .ConfigureAwait(false);
        var computerSystem = await RunAsync(supervisor, WorkerOperation.WmiComputerSystem, null, cancellationToken)
            .ConfigureAwait(false);
        var video = await RunAsync(supervisor, WorkerOperation.WmiVideoControllers, null, cancellationToken)
            .ConfigureAwait(false);

        var hasVideoInventory = TryControllers(video, out var controllers) && controllers.Count > 0;
        var drivers = hasVideoInventory
            ? await RunAsync(supervisor, WorkerOperation.WmiDisplayDrivers, null, cancellationToken).ConfigureAwait(false)
            : null;
        if (!hasVideoInventory) supervisor.OmitOperation();

        var topologyInput = new WorkerRequestPayload(new DisplayActiveTopologyRequest(BuildTopologyInput(video)));
        var topology = await RunAsync(supervisor, WorkerOperation.DisplayActiveTopology, topologyInput, cancellationToken)
            .ConfigureAwait(false);

        return Assemble(operatingSystem, computerSystem, video, drivers, topology);
    }

    private static async ValueTask<SupervisorOutcome> RunAsync(CollectionSupervisor supervisor, WorkerOperation operation,
        WorkerRequestPayload? input, CancellationToken cancellationToken)
    {
        var outcome = await supervisor.RunOperationAsync(operation, null, cancellationToken, input).ConfigureAwait(false);
        if (outcome.Kind is SupervisorTerminalKind.HostFailure or SupervisorTerminalKind.Cancelled)
            throw new SupervisedCollectionException(outcome.Code ?? outcome.Kind.ToString());
        return outcome;
    }

    private static bool TryControllers(SupervisorOutcome outcome, out IReadOnlyList<WmiVideoControllerFact> controllers)
    {
        controllers = [];
        if (outcome.Kind != SupervisorTerminalKind.ResultAccepted ||
            outcome.Result is not { State: WorkerResultState.Succeeded } result ||
            result.Payload?.WmiVideoControllers is not { } payload ||
            payload.Controllers.State != DataState.Available)
            return false;
        controllers = payload.Controllers.Value!;
        return true;
    }

    private static IReadOnlyList<DisplayTopologyIdentity> BuildTopologyInput(SupervisorOutcome video)
    {
        if (!TryControllers(video, out var controllers)) return [];
        return controllers.Select((controller, index) =>
            new DisplayTopologyIdentity($"gpu-{index + 1}", controller.TransientInstanceId)).ToArray();
    }

    private static CollectionSnapshot Assemble(SupervisorOutcome operatingSystem, SupervisorOutcome computerSystem,
        SupervisorOutcome video, SupervisorOutcome? drivers, SupervisorOutcome topology)
    {
        var os = operatingSystem.Result?.Payload?.WmiOperatingSystem;
        var system = new SystemFacts(
            os?.WindowsVersion ?? Failure<string>(operatingSystem, DataSource.WmiOperatingSystem),
            os?.WindowsBuild ?? Failure<string>(operatingSystem, DataSource.WmiOperatingSystem),
            computerSystem.Result?.Payload?.WmiComputerSystem?.Manufacturer ?? Failure<string>(computerSystem, DataSource.WmiComputerSystem),
            computerSystem.Result?.Payload?.WmiComputerSystem?.Model ?? Failure<string>(computerSystem, DataSource.WmiComputerSystem));

        var gpus = AssembleGpus(video, drivers);
        var displays = AssembleDisplays(topology);

        var videoRun = Run(video, DataSource.WmiVideoController,
            gpus.State == DataState.Available && gpus.Value!.Any(g => g.Name.State != DataState.Available));
        var collection = new List<CollectorRun>
        {
            Run(operatingSystem, DataSource.WmiOperatingSystem,
                system.WindowsVersion.State != DataState.Available || system.WindowsBuild.State != DataState.Available),
            Run(computerSystem, DataSource.WmiComputerSystem,
                system.Manufacturer.State != DataState.Available || system.Model.State != DataState.Available),
            videoRun,
            displays.Run
        };
        if (drivers is not null)
            collection.Add(Run(drivers, DataSource.WmiSignedDriver,
                gpus.State == DataState.Available && gpus.Value!.Any(g =>
                    g.Driver.Provider.State != DataState.Available ||
                    g.Driver.Version.State != DataState.Available ||
                    g.Driver.Date.State != DataState.Available)));

        return new(new(system, gpus, displays.Displays), collection.ToArray());
    }

    private static (Observation<IReadOnlyList<DisplayFacts>> Displays, CollectorRun Run) AssembleDisplays(SupervisorOutcome topology)
    {
        if (topology.Kind == SupervisorTerminalKind.ResultAccepted &&
            topology.Result is { } result &&
            result.Payload?.DisplayActiveTopology is { } payload)
            return payload.Displays is { } displays
                ? (displays, payload.Run)
                : (Observation<IReadOnlyList<DisplayFacts>>.Absent(FailureState(topology), DataSource.DisplayConfig, FailureReason(topology)),
                    new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Failed, FailureReason(topology)) { Attempts = topology.Attempts });
        var reason = FailureReason(topology);
        return (Observation<IReadOnlyList<DisplayFacts>>.Absent(FailureState(topology), DataSource.DisplayConfig, reason),
            new CollectorRun(DataSource.DisplayConfig, FailureState(topology) == DataState.Unsupported ? CollectorStatus.Unsupported : CollectorStatus.Failed, reason)
            { Attempts = topology.Attempts });
    }

    private static Observation<IReadOnlyList<GpuFacts>> AssembleGpus(SupervisorOutcome video, SupervisorOutcome? drivers)
    {
        if (!TryControllers(video, out var controllers))
            return Observation<IReadOnlyList<GpuFacts>>.Absent(FailureState(video), DataSource.WmiVideoController, FailureReason(video));
        var gpus = controllers.Select((controller, index) => new GpuFacts(
            $"gpu-{index + 1}", controller.Name, controller.PciVendorId, controller.PciDeviceId,
            Observation<string>.Absent(DataState.Unsupported, DataSource.NotCollected, ReasonCode.NotImplemented),
            DriverFactsFor(controller.TransientInstanceId, drivers))).ToArray();
        return Observation<IReadOnlyList<GpuFacts>>.Known(gpus, DataSource.WmiVideoController);
    }

    private static DriverFacts DriverFactsFor(string? instanceId, SupervisorOutcome? driverOutcome)
    {
        static DriverFacts Absent(DataState state, ReasonCode reason)
        {
            var field = Observation<string>.Absent(state, DataSource.WmiSignedDriver, reason);
            return new(field, field, field);
        }
        if (driverOutcome is null) return Absent(DataState.Unknown, ReasonCode.NoMatchingDriver);
        if (driverOutcome.Kind != SupervisorTerminalKind.ResultAccepted ||
            driverOutcome.Result is not { State: WorkerResultState.Succeeded } result ||
            result.Payload?.WmiDisplayDrivers is not { } payload ||
            payload.Drivers.State != DataState.Available)
            return Absent(FailureState(driverOutcome), FailureReason(driverOutcome));
        if (string.IsNullOrWhiteSpace(instanceId)) return Absent(DataState.Unknown, ReasonCode.NoMatchingDriver);
        var matches = payload.Drivers.Value!.Where(fact =>
            !string.IsNullOrWhiteSpace(fact.TransientDeviceInstanceId) &&
            string.Equals(fact.TransientDeviceInstanceId, instanceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0) return Absent(DataState.Unknown, ReasonCode.NoMatchingDriver);
        if (matches.Length > 1) return Absent(DataState.Unknown, ReasonCode.AmbiguousDriver);
        return new(matches[0].Provider, matches[0].Version, matches[0].Date);
    }

    private static CollectorRun Run(SupervisorOutcome outcome, DataSource source, bool partial)
    {
        if (outcome.Kind == SupervisorTerminalKind.ResultAccepted &&
            outcome.Result is { State: WorkerResultState.Succeeded })
            return new(source, partial ? CollectorStatus.Partial : CollectorStatus.Succeeded,
                partial ? ReasonCode.MissingValue : ReasonCode.None) { Attempts = Math.Max(1, outcome.Attempts) };
        var state = FailureState(outcome);
        return new(source, state == DataState.Unsupported ? CollectorStatus.Unsupported : CollectorStatus.Failed,
            FailureReason(outcome)) { Attempts = outcome.Attempts };
    }

    private static Observation<T> Failure<T>(SupervisorOutcome outcome, DataSource source) where T : class =>
        Observation<T>.Absent(FailureState(outcome), source, FailureReason(outcome));

    private static DataState FailureState(SupervisorOutcome outcome) =>
        outcome.Result is { State: WorkerResultState.Unsupported } ? DataState.Unsupported : DataState.Failed;

    private static ReasonCode FailureReason(SupervisorOutcome outcome) => outcome.Kind switch
    {
        SupervisorTerminalKind.Timeout or SupervisorTerminalKind.Skipped => ReasonCode.Timeout,
        SupervisorTerminalKind.ProtocolFailure => ReasonCode.QueryFailed,
        SupervisorTerminalKind.ResourceLimit => ReasonCode.ResourceLimit,
        SupervisorTerminalKind.ResultAccepted when outcome.Result is { } result => result.Reason,
        _ => ReasonCode.QueryFailed
    };
}
