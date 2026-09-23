using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;

namespace WinGPUDoctor.Tests;

internal static class M4TestData
{
    internal static Observation<string> Text(string value, DataSource source) => Observation<string>.Known(value, source);

    internal static WorkerResultPayload OperatingSystem(string version = "10.0.26200", string build = "26200") =>
        new(new WmiOperatingSystemResult(Text(version, DataSource.WmiOperatingSystem), Text(build, DataSource.WmiOperatingSystem)),
            null, null, null, null);

    internal static WorkerResultPayload ComputerSystem(string manufacturer = "Example OEM", string model = "Example Model") =>
        new(null, new WmiComputerSystemResult(Text(manufacturer, DataSource.WmiComputerSystem), Text(model, DataSource.WmiComputerSystem)),
            null, null, null);

    internal static WorkerResultPayload Video(params (string? InstanceId, string Name, string Vendor, string Device)[] items) =>
        new(null, null, new WmiVideoControllersResult(Observation<IReadOnlyList<WmiVideoControllerFact>>.Known(
            items.Select(item => new WmiVideoControllerFact(item.InstanceId, Text(item.Name, DataSource.WmiVideoController),
                Text(item.Vendor, DataSource.WmiVideoController), Text(item.Device, DataSource.WmiVideoController))).ToArray(),
            DataSource.WmiVideoController)), null, null);

    internal static WorkerResultPayload Drivers(params (string InstanceId, string Provider, string Version, string Date)[] items) =>
        new(null, null, null, new WmiDisplayDriversResult(Observation<IReadOnlyList<WmiDisplayDriverFact>>.Known(
            items.Select(item => new WmiDisplayDriverFact(item.InstanceId, Text(item.Provider, DataSource.WmiSignedDriver),
                Text(item.Version, DataSource.WmiSignedDriver), Text(item.Date, DataSource.WmiSignedDriver))).ToArray(),
            DataSource.WmiSignedDriver)), null);

    internal static ResultFrame Success(WorkerOperation operation, WorkerResultPayload payload) =>
        new(operation, WorkerResultState.Succeeded, ReasonCode.None, payload);

    internal static ResultFrame Failure(WorkerOperation operation, ReasonCode reason) =>
        new(operation, WorkerResultState.Failed, reason, null);

    internal static ResultFrame TopologySuccess(params DisplayFacts[] displays) =>
        new(WorkerOperation.DisplayActiveTopology, WorkerResultState.Succeeded, ReasonCode.None,
            new(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known(displays, DataSource.DisplayConfig), ProtocolTests.Run())));

    internal static ResultFrame TopologyUnmatched() =>
        new(WorkerOperation.DisplayActiveTopology, WorkerResultState.Partial, ReasonCode.UnmatchedAdapter,
            new(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known([UnmatchedDisplay()], DataSource.DisplayConfig),
                ProtocolTests.Run() with
                {
                    Status = CollectorStatus.Partial,
                    Reason = ReasonCode.UnmatchedAdapter,
                    Issues = [new(CollectionOperation.ResolveAdapter, ReasonCode.UnmatchedAdapter, null),
                        new(CollectionOperation.TargetName, ReasonCode.MissingValue, null)]
                })));

    internal static ResultFrame TopologyAmbiguous() =>
        new(WorkerOperation.DisplayActiveTopology, WorkerResultState.Partial, ReasonCode.AmbiguousAdapter,
            new(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known([UnavailableDisplay(ReasonCode.AmbiguousAdapter)], DataSource.DisplayConfig),
                ProtocolTests.Run() with
                {
                    Status = CollectorStatus.Partial,
                    Reason = ReasonCode.AmbiguousAdapter,
                    Issues = [new(CollectionOperation.ResolveAdapter, ReasonCode.AmbiguousAdapter, null),
                        new(CollectionOperation.TargetName, ReasonCode.MissingValue, null)]
                })));

    internal static ResultFrame TopologyTargetNameFailure() =>
        new(WorkerOperation.DisplayActiveTopology, WorkerResultState.Partial, ReasonCode.SessionAccessDenied,
            new(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known([ProtocolTests.Display("gpu-1") with
                {
                    Name = Observation<string>.Absent(DataState.Failed, DataSource.DisplayConfig, ReasonCode.SessionAccessDenied)
                }], DataSource.DisplayConfig),
                ProtocolTests.Run() with
                {
                    Status = CollectorStatus.Partial,
                    Reason = ReasonCode.SessionAccessDenied,
                    Issues = [new(CollectionOperation.TargetName, ReasonCode.SessionAccessDenied, 5)]
                })));

    internal static ResultFrame TopologyPreQueryFailure() =>
        new(WorkerOperation.DisplayActiveTopology, WorkerResultState.Unsupported, ReasonCode.ApiUnavailable,
            new(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Unsupported, DataSource.DisplayConfig, ReasonCode.ApiUnavailable),
                new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Unsupported, ReasonCode.ApiUnavailable)
                {
                    Attempts = 0,
                    Issues = [new(CollectionOperation.QueryPaths, ReasonCode.ApiUnavailable, null)]
                })));

    private static DisplayFacts UnmatchedDisplay() => UnavailableDisplay(ReasonCode.UnmatchedAdapter);

    private static DisplayFacts UnavailableDisplay(ReasonCode reason) => ProtocolTests.Display("gpu-1") with
    {
        SourceAdapter = Observation<AdapterMatch>.Absent(DataState.Unknown, DataSource.SetupApiInstanceJoin, reason),
        TargetAdapter = Observation<AdapterMatch>.Absent(DataState.Unknown, DataSource.SetupApiInstanceJoin, reason)
    };
}
