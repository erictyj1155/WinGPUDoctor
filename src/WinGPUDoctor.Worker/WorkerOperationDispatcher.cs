using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Windows;

namespace WinGPUDoctor.Worker;

internal sealed record DispatchOutcome(ResultFrame Frame, int Attempts);

internal sealed class WorkerOperationDispatcher
{
    private readonly IWmiReader _reader;
    private readonly Func<IReadOnlyList<GpuCorrelationIdentity>, Action<int>?, TopologyResult> _topology;

    internal static WorkerOperationDispatcher CreateLocal() => new();

    internal WorkerOperationDispatcher(IWmiReader? reader = null,
        Func<IReadOnlyList<GpuCorrelationIdentity>, Action<int>?, TopologyResult>? topology = null)
    {
        _reader = reader ?? new WmiReader();
        _topology = topology ?? ((inventory, progress) => new DisplayTopologyCollector(
            new DisplayConfigApi(), new SetupApiAdapterResolver(), DisplayTopologyCollector.CurrentQueryMode)
            .Collect(inventory, progress));
    }

    internal DispatchOutcome Dispatch(WorkerOperation operation, WorkerRequestPayload start, Action<int>? attemptStarted = null)
    {
        // Commit to the one WMI query before entering its provider. Topology owns its retry boundaries.
        if (operation is >= WorkerOperation.WmiOperatingSystem and <= WorkerOperation.WmiDisplayDrivers)
            attemptStarted?.Invoke(1);
        return operation switch
        {
            WorkerOperation.WmiOperatingSystem => DispatchOperatingSystem(),
            WorkerOperation.WmiComputerSystem => DispatchComputerSystem(),
            WorkerOperation.WmiVideoControllers => DispatchVideoControllers(),
            WorkerOperation.WmiDisplayDrivers => DispatchDisplayDrivers(),
            WorkerOperation.DisplayActiveTopology => DispatchTopology(start, attemptStarted),
            _ => throw ProtocolCodec.Invalid()
        };
    }

    private DispatchOutcome DispatchOperatingSystem()
    {
        const DataSource source = DataSource.WmiOperatingSystem;
        var result = _reader.Read(WmiQuery.OperatingSystem);
        if (result.State != DataState.Available) return Failure(WorkerOperation.WmiOperatingSystem, result.State, result.Reason);
        var payload = new WorkerResultPayload(
            new WmiOperatingSystemResult(
                WindowsCollector.Single(result, "Version", source),
                WindowsCollector.Single(result, "BuildNumber", source)),
            null, null, null, null);
        return new(new ResultFrame(WorkerOperation.WmiOperatingSystem, WorkerResultState.Succeeded, ReasonCode.None, payload), 1);
    }

    private DispatchOutcome DispatchComputerSystem()
    {
        const DataSource source = DataSource.WmiComputerSystem;
        var result = _reader.Read(WmiQuery.ComputerSystem);
        if (result.State != DataState.Available) return Failure(WorkerOperation.WmiComputerSystem, result.State, result.Reason);
        var payload = new WorkerResultPayload(
            null,
            new WmiComputerSystemResult(
                WindowsCollector.Single(result, "Manufacturer", source),
                WindowsCollector.Single(result, "Model", source)),
            null, null, null);
        return new(new ResultFrame(WorkerOperation.WmiComputerSystem, WorkerResultState.Succeeded, ReasonCode.None, payload), 1);
    }

    private DispatchOutcome DispatchVideoControllers()
    {
        const DataSource source = DataSource.WmiVideoController;
        var result = _reader.Read(WmiQuery.VideoControllers);
        if (result.State != DataState.Available) return Failure(WorkerOperation.WmiVideoControllers, result.State, result.Reason);
        var controllers = result.Rows.Select(row =>
        {
            var (vendor, device) = WindowsCollector.PciIds(row.Get("PNPDeviceID"));
            return new WmiVideoControllerFact(Identity(row.Get("PNPDeviceID")), WindowsCollector.Text(row, "Name", source), vendor, device);
        }).ToArray();
        var payload = new WorkerResultPayload(null, null,
            new WmiVideoControllersResult(Observation<IReadOnlyList<WmiVideoControllerFact>>.Known(controllers, source)),
            null, null);
        return new(new ResultFrame(WorkerOperation.WmiVideoControllers, WorkerResultState.Succeeded, ReasonCode.None, payload), 1);
    }

    private DispatchOutcome DispatchDisplayDrivers()
    {
        const DataSource source = DataSource.WmiSignedDriver;
        var result = _reader.Read(WmiQuery.DisplayDrivers);
        if (result.State != DataState.Available) return Failure(WorkerOperation.WmiDisplayDrivers, result.State, result.Reason);
        var drivers = result.Rows.Select(row => new WmiDisplayDriverFact(Identity(row.Get("DeviceID")),
            WindowsCollector.Text(row, "DriverProviderName", source), WindowsCollector.Text(row, "DriverVersion", source),
            WindowsCollector.DriverDate(row.Get("DriverDate")))).ToArray();
        var payload = new WorkerResultPayload(null, null, null,
            new WmiDisplayDriversResult(Observation<IReadOnlyList<WmiDisplayDriverFact>>.Known(drivers, source)), null);
        return new(new ResultFrame(WorkerOperation.WmiDisplayDrivers, WorkerResultState.Succeeded, ReasonCode.None, payload), 1);
    }

    private static string? Identity(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private DispatchOutcome DispatchTopology(WorkerRequestPayload start, Action<int>? attemptStarted)
    {
        if (start.DisplayActiveTopology is not { } request) throw ProtocolCodec.Invalid();
        var inventory = request.Inventory.Select(item => new GpuCorrelationIdentity(item.Label, item.TransientInstanceId)).ToArray();
        var result = _topology(inventory, attemptStarted);
        var state = (WorkerResultState)result.Run.Status;
        var payload = new WorkerResultPayload(null, null, null, null, new DisplayActiveTopologyResult(result.Displays, result.Run));
        return new(new ResultFrame(WorkerOperation.DisplayActiveTopology, state, result.Run.Reason, payload), result.Run.Attempts);
    }

    private static DispatchOutcome Failure(WorkerOperation operation, DataState state, ReasonCode reason)
    {
        var workerState = state == DataState.Unsupported ? WorkerResultState.Unsupported : WorkerResultState.Failed;
        if (workerState == WorkerResultState.Unsupported &&
            reason is not (ReasonCode.NotImplemented or ReasonCode.NotSupported or ReasonCode.ApiUnavailable or ReasonCode.InteropLayoutUnsupported))
            workerState = WorkerResultState.Failed;
        return new(new ResultFrame(operation, workerState, reason, null), 1);
    }
}
