using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using System.Text;
using System.Text.Json;
using Xunit;

namespace WinGPUDoctor.Tests;

public class SupervisedCollectorTests
{
    private static readonly CollectionTimingPolicy Policy = new(
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

    [Fact]
    public async Task AllFiveOperationsSucceedAndPreserveExpectedAssembly()
    {
        var videoInstance = @"PRIVATE_VIDEO_INSTANCE_A";
        var driverInstance = @"private_video_instance_a";
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video((videoInstance, "GPU A", "10DE", "1234"), ("OTHER_INSTANCE", "GPU B", "8086", "5678"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers((driverInstance, "Provider", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);

        Assert.Equal(["WmiOperatingSystem", "WmiComputerSystem", "WmiVideoController", "DisplayConfig", "WmiSignedDriver"],
            snapshot.Collection.Select(run => run.Source.ToString()));
        Assert.Equal("10.0.26200", snapshot.Facts.System.WindowsVersion.Value);
        Assert.Equal("Example OEM", snapshot.Facts.System.Manufacturer.Value);
        Assert.Equal(2, snapshot.Facts.Gpus.Value!.Count);
        Assert.Equal("Provider", snapshot.Facts.Gpus.Value[0].Driver.Provider.Value);
        Assert.Equal(ReasonCode.NoMatchingDriver, snapshot.Facts.Gpus.Value[1].Driver.Provider.Reason);
        Assert.Single(snapshot.Facts.Displays.Value!);
        Assert.Equal(CollectorStatus.Partial, snapshot.Collection.Single(run => run.Source == DataSource.WmiSignedDriver).Status);
        var topologyInput = Assert.IsType<StartFrame>(factory.Sessions[WorkerOperation.DisplayActiveTopology].StartFrame).Payload;
        Assert.Equal(2, topologyInput.DisplayActiveTopology!.Inventory.Count);
        Assert.Equal("gpu-1", topologyInput.DisplayActiveTopology.Inventory[0].Label);
        Assert.Equal(videoInstance, topologyInput.DisplayActiveTopology.Inventory[0].TransientInstanceId);
        Assert.Equal("gpu-2", topologyInput.DisplayActiveTopology.Inventory[1].Label);
    }

    [Fact]
    public async Task VideoFailureOmitsDriverAndTopologyCanStillRunUnmatched()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Failure(WorkerOperation.WmiVideoControllers, ReasonCode.AccessDenied))
            .For(WorkerOperation.DisplayActiveTopology, M4TestData.TopologyUnmatched);

        var snapshot = await Collect(factory);

        Assert.Equal([WorkerOperation.WmiOperatingSystem, WorkerOperation.WmiComputerSystem,
            WorkerOperation.WmiVideoControllers, WorkerOperation.DisplayActiveTopology], factory.Calls);
        Assert.Equal(DataState.Failed, snapshot.Facts.Gpus.State);
        Assert.Equal(ReasonCode.AccessDenied, snapshot.Facts.Gpus.Reason);
        Assert.Equal(CollectorStatus.Partial, snapshot.Collection.Single(run => run.Source == DataSource.DisplayConfig).Status);
        var input = Assert.IsType<StartFrame>(factory.Sessions[WorkerOperation.DisplayActiveTopology].StartFrame).Payload;
        Assert.Empty(input.DisplayActiveTopology!.Inventory);
    }

    [Fact]
    public async Task DriverTimeoutRetainsVideoInventoryAndTopologyFacts()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .Timeout(WorkerOperation.WmiDisplayDrivers)
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);

        Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
        Assert.Equal(DataState.Failed, snapshot.Facts.Gpus.Value![0].Driver.Provider.State);
        Assert.Equal(ReasonCode.Timeout, snapshot.Facts.Gpus.Value[0].Driver.Provider.Reason);
        Assert.Equal(CollectorStatus.Failed, snapshot.Collection.Single(run => run.Source == DataSource.WmiSignedDriver).Status);
        Assert.Single(snapshot.Facts.Displays.Value!);
    }

    [Fact]
    public async Task DriverProviderFailureRetainsVideoInventory()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Failure(WorkerOperation.WmiDisplayDrivers, ReasonCode.ProviderUnavailable))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);
        Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
        Assert.Equal(DataState.Failed, snapshot.Facts.Gpus.Value![0].Driver.Provider.State);
        Assert.Equal(ReasonCode.ProviderUnavailable, snapshot.Facts.Gpus.Value[0].Driver.Provider.Reason);
    }

    [Fact]
    public async Task VideoTimeoutOmitsDriverAndTopologyStillRuns()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .Timeout(WorkerOperation.WmiVideoControllers)
            .For(WorkerOperation.DisplayActiveTopology, M4TestData.TopologyUnmatched);

        var snapshot = await Collect(factory);
        Assert.DoesNotContain(WorkerOperation.WmiDisplayDrivers, factory.Calls);
        Assert.Equal(ReasonCode.Timeout, snapshot.Facts.Gpus.Reason);
        Assert.Equal(CollectorStatus.Partial, snapshot.Collection.Single(run => run.Source == DataSource.DisplayConfig).Status);
    }

    [Fact]
    public async Task ComputerSystemTimeoutDoesNotEraseOtherFacts()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .Timeout(WorkerOperation.WmiComputerSystem)
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);
        Assert.Equal(DataState.Available, snapshot.Facts.System.WindowsVersion.State);
        Assert.Equal(DataState.Failed, snapshot.Facts.System.Manufacturer.State);
        Assert.Equal(ReasonCode.Timeout, snapshot.Facts.System.Manufacturer.Reason);
        Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
    }

    [Fact]
    public async Task TopologyTimeoutRetainsAllWmiFacts()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
            .Timeout(WorkerOperation.DisplayActiveTopology);

        var snapshot = await Collect(factory);

        Assert.Equal(DataState.Available, snapshot.Facts.System.WindowsVersion.State);
        Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
        Assert.Equal("Provider", snapshot.Facts.Gpus.Value![0].Driver.Provider.Value);
        var displayRun = snapshot.Collection.Single(run => run.Source == DataSource.DisplayConfig);
        Assert.Equal(CollectorStatus.Failed, displayRun.Status);
        Assert.Equal(ReasonCode.Timeout, displayRun.Reason);
        Assert.Empty(displayRun.Issues);
    }

    [Fact]
    public async Task EarlierTimeoutDoesNotEraseLaterFacts()
    {
        var factory = new ScriptedFactory()
            .Timeout(WorkerOperation.WmiOperatingSystem)
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);

        Assert.Equal(DataState.Failed, snapshot.Facts.System.WindowsVersion.State);
        Assert.Equal(ReasonCode.Timeout, snapshot.Facts.System.WindowsVersion.Reason);
        Assert.Equal("Example OEM", snapshot.Facts.System.Manufacturer.Value);
        Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
        Assert.Single(snapshot.Facts.Displays.Value!);
    }

    [Fact]
    public async Task AmbiguousDriverIdentityDoesNotChooseByOrder()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers(("INSTANCE", "Provider A", "1.0", "2026-01-01"),
                    ("instance", "Provider B", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);
        Assert.Equal(ReasonCode.AmbiguousDriver, snapshot.Facts.Gpus.Value![0].Driver.Provider.Reason);
    }

    [Fact]
    public async Task MissingVideoIdentityUsesNoMatchingDriverWithoutInventingIdentity()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video((null, "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess());

        var snapshot = await Collect(factory);
        Assert.Equal(ReasonCode.NoMatchingDriver, snapshot.Facts.Gpus.Value![0].Driver.Provider.Reason);
        var input = Assert.IsType<StartFrame>(factory.Sessions[WorkerOperation.DisplayActiveTopology].StartFrame).Payload;
        Assert.Null(input.DisplayActiveTopology!.Inventory[0].TransientInstanceId);
    }

    [Fact]
    public async Task AmbiguousAndTargetNameFailureTopologyRemainIncomplete()
    {
        foreach (var topology in new[] { M4TestData.TopologyUnmatched(), M4TestData.TopologyAmbiguous(), M4TestData.TopologyTargetNameFailure() })
        {
            var factory = new ScriptedFactory()
                .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
                .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
                .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                    M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
                .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                    M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
                .For(WorkerOperation.DisplayActiveTopology, () => topology);
            var snapshot = await Collect(factory);
            Assert.Equal(CollectorStatus.Partial, snapshot.Collection.Single(run => run.Source == DataSource.DisplayConfig).Status);
            Assert.True(snapshot.Collection.Single(run => run.Source == DataSource.DisplayConfig).IsIncomplete());
        }
    }

    [Fact]
    public async Task WorkerCrashAndMalformedResultBecomeQueryFailedWithoutErasingLaterFacts()
    {
        foreach (var failure in new Func<ResultFrame>[]
        {
            () => throw new EndOfStreamException("synthetic worker exit"),
            () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem())
        })
        {
            var factory = new ScriptedFactory()
                .For(WorkerOperation.WmiOperatingSystem, failure)
                .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
                .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                    M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
                .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                    M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
                .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));
            var snapshot = await Collect(factory);
            Assert.Equal(ReasonCode.QueryFailed, snapshot.Facts.System.WindowsVersion.Reason);
            Assert.Equal("Example OEM", snapshot.Facts.System.Manufacturer.Value);
            Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
        }
    }

    [Fact]
    public async Task CleanupFailureIsFatalAndStopsLaterCollection()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()), cleanupSucceeds: false)
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()));
        var error = await Assert.ThrowsAsync<SupervisedCollectionException>(async () => await Collect(factory));
        Assert.Equal("cleanup-unconfirmed", error.Code);
        Assert.Single(factory.Calls);
    }

    [Fact]
    public async Task PreCancelledCollectionIsFatalHostCancellation()
    {
        var factory = new ScriptedFactory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<SupervisedCollectionException>(async () =>
            await new SupervisedWindowsCollector(Policy, TimeProvider.System, factory, new HostAdmission()).CollectAsync(cancellation.Token));
        Assert.Equal("host-cancelled", error.Code);
        Assert.Empty(factory.Calls);
    }

    [Fact]
    public async Task FatalAdmissionFailureReturnsNoSnapshot()
    {
        var factory = new ScriptedFactory { Failure = AdmissionFailure.Containment };
        var error = await Assert.ThrowsAsync<SupervisedCollectionException>(async () => await Collect(factory));
        Assert.Equal(nameof(AdmissionFailure.Containment), error.Code);
        Assert.Single(factory.Calls);
    }

    [Fact]
    public async Task TransientIdentifiersNeverReachPrivacyProjectedExports()
    {
        var factory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video((@"PRIVATE_VIDEO_INSTANCE_A", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers((@"private_video_instance_a", "Provider", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));

        var snapshot = await Collect(factory);
        var json = ReportWriter.Json(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 20)));
        var markdown = ReportWriter.Markdown(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 20)));
        Assert.DoesNotContain("PRIVATE_VIDEO_INSTANCE", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE_VIDEO_INSTANCE", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactSetupApiInstanceId", json);
    }

    [Fact]
    public async Task IntegratedTimeoutAndIncompleteSnapshotsSerializeForSchemaValidation()
    {
        var timeoutFactory = new ScriptedFactory()
            .Timeout(WorkerOperation.WmiOperatingSystem)
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .Timeout(WorkerOperation.WmiDisplayDrivers)
            .For(WorkerOperation.DisplayActiveTopology, () => M4TestData.TopologySuccess(ProtocolTests.Display("gpu-1")));
        var incompleteFactory = new ScriptedFactory()
            .For(WorkerOperation.WmiOperatingSystem, () => M4TestData.Success(WorkerOperation.WmiOperatingSystem, M4TestData.OperatingSystem()))
            .For(WorkerOperation.WmiComputerSystem, () => M4TestData.Success(WorkerOperation.WmiComputerSystem, M4TestData.ComputerSystem()))
            .For(WorkerOperation.WmiVideoControllers, () => M4TestData.Success(WorkerOperation.WmiVideoControllers,
                M4TestData.Video(("INSTANCE", "GPU", "10DE", "1234"))))
            .For(WorkerOperation.WmiDisplayDrivers, () => M4TestData.Success(WorkerOperation.WmiDisplayDrivers,
                M4TestData.Drivers(("INSTANCE", "Provider", "2.0", "2026-01-02"))))
            .For(WorkerOperation.DisplayActiveTopology, M4TestData.TopologyUnmatched);

        var timeoutJson = Json(await Collect(timeoutFactory));
        var incompleteJson = Json(await Collect(incompleteFactory));
        Assert.Contains(WarningCode.CollectionIncomplete, Parse(timeoutJson).Warnings);
        Assert.Contains(WarningCode.CollectionIncomplete, Parse(incompleteJson).Warnings);
        Assert.Equal(ReasonCode.Timeout, Parse(timeoutJson).Facts.System.WindowsVersion.Reason);
        Assert.Equal(CollectorStatus.Partial, Parse(incompleteJson).Collection.Single(run => run.Source == DataSource.DisplayConfig).Status);

        var directory = Environment.GetEnvironmentVariable("WINGPUDOCTOR_M4_SCHEMA_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "timeout.json"), timeoutJson, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "incomplete.json"), incompleteJson, new UTF8Encoding(false));
        }
    }

    private static string Json(CollectionSnapshot snapshot) =>
        ReportWriter.Json(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 20)));
    private static DiagnosticReport Parse(string json) =>
        JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;

    private static async Task<CollectionSnapshot> Collect(ScriptedFactory factory) =>
        await new SupervisedWindowsCollector(Policy, TimeProvider.System, factory, new HostAdmission()).CollectAsync();

    private sealed class ScriptedFactory : IWorkerSessionFactory
    {
        private readonly Dictionary<WorkerOperation, Func<ResultFrame?>> _results = [];
        private readonly Dictionary<WorkerOperation, bool> _cleanupSucceeds = [];
        internal List<WorkerOperation> Calls { get; } = [];
        internal Dictionary<WorkerOperation, ScriptedSession> Sessions { get; } = [];
        internal AdmissionFailure? Failure { get; init; }

        internal ScriptedFactory For(WorkerOperation operation, Func<ResultFrame> result, bool cleanupSucceeds = true)
        {
            _results[operation] = result;
            _cleanupSucceeds[operation] = cleanupSucceeds;
            return this;
        }
        internal ScriptedFactory Timeout(WorkerOperation operation)
        {
            _results[operation] = () => null;
            return this;
        }
        public ValueTask<IWorkerSession> CreateAsync(WorkerOperation operation, WorkerLaunchOptions? options,
            Deadline connectDeadline, Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission,
            CancellationToken token)
        {
            Calls.Add(operation);
            if (Failure is { } failure) throw new WorkerAdmissionException(failure);
            var session = new ScriptedSession(operation, _results[operation], _cleanupSucceeds.GetValueOrDefault(operation, true));
            Sessions[operation] = session;
            return ValueTask.FromResult<IWorkerSession>(session);
        }
    }

    private sealed class ScriptedSession(WorkerOperation operation, Func<ResultFrame?> resultFactory, bool cleanupSucceeds) : IWorkerSession
    {
        private int _read;
        private bool _resolved;
        private ResultFrame? _result;
        internal ProtocolFrame? StartFrame { get; private set; }
        public bool IsInCreationTimeJob => true;
        public bool IsElevated => false;
        public WorkerBuildIdentity ExpectedIdentity => ProtocolTests.Identity;
        private ResultFrame? Result { get { if (!_resolved) { _result = resultFactory(); _resolved = true; } return _result; } }
        private int Attempts => Result?.Payload?.DisplayActiveTopology?.Run.Attempts ?? 1;
        public ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token)
        {
            if (frame is StartFrame start) StartFrame = start;
            return ValueTask.CompletedTask;
        }
        public ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token)
        {
            _read++;
            if (_read == 1) return ValueTask.FromResult<ProtocolFrame>(new ReadyFrame(operation, ProtocolTests.Identity));
            if (_read <= 1 + Attempts) return ValueTask.FromResult<ProtocolFrame>(new AttemptStartedFrame(operation, _read - 1));
            return ValueTask.FromResult<ProtocolFrame>(Result ?? throw new TimeoutException("synthetic worker timeout"));
        }
        public ValueTask<bool> CleanupAsync(Deadline deadline) => ValueTask.FromResult(cleanupSucceeds);
        public void Dispose() { }
    }
}
