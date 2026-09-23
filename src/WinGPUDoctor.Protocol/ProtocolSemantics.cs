using System.Text.RegularExpressions;
using WinGPUDoctor.Core;
namespace WinGPUDoctor.Protocol;

internal static class ProtocolSemantics
{
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition) { if (!condition) throw ProtocolCodec.Invalid(); }
    private static void Label(string? value, string prefix) => Require(value is not null && Regex.IsMatch(value, $@"\A{prefix}-[1-9][0-9]*\z"));
    private static void Identifier(string? value)
    {
        if (value is null) return; // Missing identity is meaningful and never synthesized.
        Require(!string.IsNullOrWhiteSpace(value) && value.Length <= ProtocolConstants.MaxDecodedStringChars &&
            !value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format));
    }
    internal static void Start(WorkerOperation operation, WorkerRequestPayload payload)
    {
        Require(payload is not null);
        if (operation != WorkerOperation.DisplayActiveTopology) { Require(payload!.DisplayActiveTopology is null); return; }
        Require(payload!.DisplayActiveTopology?.Inventory is not null);
        var inventory = payload.DisplayActiveTopology!.Inventory;
        if (inventory.Count > ProtocolConstants.MaxCollectionItems) throw ProtocolCodec.Limit();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in inventory)
        {
            Require(item is not null); Label(item!.Label, "gpu"); Require(labels.Add(item.Label)); Identifier(item.TransientInstanceId);
        }
    }
    private static void Observation<T>(Observation<T>? observation, params DataSource[] sources) where T : class
    {
        Require(observation is not null);
        var value = observation!;
        Require(Enum.IsDefined(value.State) && Enum.IsDefined(value.Reason) && sources.Contains(value.Source) && value.State != DataState.Redacted);
        Require(value.State == DataState.Available ? value.Value is not null && value.Reason == ReasonCode.None : value.Value is null && value.Reason != ReasonCode.None);
        Require(value.Reason != ReasonCode.SensitiveValue);
    }
    private static void Text(Observation<string>? value, DataSource source)
    {
        Observation(value, source);
        Require(value!.State is DataState.Available or DataState.Unknown);
        if (value.State == DataState.Available) Require(!string.IsNullOrWhiteSpace(value.Value));
    }
    private static void MonitorName(Observation<string>? value)
    {
        Observation(value, DataSource.DisplayConfig);
        var name = value!;
        switch (name.State)
        {
            case DataState.Available:
                Require(!string.IsNullOrWhiteSpace(name.Value));
                return;
            case DataState.Unknown:
                Require(name.Reason == ReasonCode.MissingValue);
                return;
            case DataState.Failed:
                Require(name.Reason is not (ReasonCode.MissingValue or ReasonCode.SensitiveValue or ReasonCode.None));
                Require(name.Reason is not (ReasonCode.ApiUnavailable or ReasonCode.NotSupported or ReasonCode.InteropLayoutUnsupported or ReasonCode.NotImplemented));
                return;
            case DataState.Unsupported:
                Require(name.Reason is ReasonCode.ApiUnavailable or ReasonCode.NotSupported or ReasonCode.InteropLayoutUnsupported);
                return;
            default:
                throw ProtocolCodec.Invalid();
        }
    }
    internal static void Result(ResultFrame frame)
    {
        Require(Enum.IsDefined(frame.Operation) && Enum.IsDefined(frame.State) && Enum.IsDefined(frame.Reason));
        var p = frame.Payload ?? new WorkerResultPayload(null, null, null, null, null);
        var fields = new object?[] { p.WmiOperatingSystem, p.WmiComputerSystem, p.WmiVideoControllers, p.WmiDisplayDrivers, p.DisplayActiveTopology };
        if (frame.Operation == WorkerOperation.DisplayActiveTopology)
        {
            Require(fields.Count(x => x is not null) == 1 && p.DisplayActiveTopology is not null);
            Topology(p.DisplayActiveTopology!, frame.State, frame.Reason);
            return;
        }
        if (frame.State != WorkerResultState.Succeeded)
        {
            Require(frame.State is WorkerResultState.Failed or WorkerResultState.Unsupported && frame.Reason != ReasonCode.None && fields.All(x => x is null));
            if (frame.State == WorkerResultState.Unsupported)
                Require(frame.Reason is ReasonCode.NotImplemented or ReasonCode.NotSupported or ReasonCode.ApiUnavailable or ReasonCode.InteropLayoutUnsupported);
            return;
        }
        Require(frame.Reason == ReasonCode.None && fields.Count(x => x is not null) == 1 && fields[(int)frame.Operation] is not null);
        switch (frame.Operation)
        {
            case WorkerOperation.WmiOperatingSystem:
                Text(p.WmiOperatingSystem!.WindowsVersion, DataSource.WmiOperatingSystem); Text(p.WmiOperatingSystem.WindowsBuild, DataSource.WmiOperatingSystem); break;
            case WorkerOperation.WmiComputerSystem:
                Text(p.WmiComputerSystem!.Manufacturer, DataSource.WmiComputerSystem); Text(p.WmiComputerSystem.Model, DataSource.WmiComputerSystem); break;
            case WorkerOperation.WmiVideoControllers:
                var controllers = p.WmiVideoControllers!.Controllers;
                Observation(controllers, DataSource.WmiVideoController); Require(controllers.State == DataState.Available);
                foreach (var c in controllers.Value!)
                {
                    Require(c is not null); Identifier(c!.TransientInstanceId); Text(c.Name, DataSource.WmiVideoController);
                    Text(c.PciVendorId, DataSource.WmiVideoController); Text(c.PciDeviceId, DataSource.WmiVideoController);
                }
                break;
            case WorkerOperation.WmiDisplayDrivers:
                var drivers = p.WmiDisplayDrivers!.Drivers;
                Observation(drivers, DataSource.WmiSignedDriver); Require(drivers.State == DataState.Available);
                foreach (var d in drivers.Value!)
                {
                    Require(d is not null); Identifier(d!.TransientDeviceInstanceId); Text(d.Provider, DataSource.WmiSignedDriver);
                    Text(d.Version, DataSource.WmiSignedDriver); Text(d.Date, DataSource.WmiSignedDriver);
                }
                break;
        }
    }
    private static void Topology(DisplayActiveTopologyResult topology, WorkerResultState state, ReasonCode reason)
    {
        var run = topology.Run;
        Require(run is not null && run.Source == DataSource.DisplayConfig && Enum.IsDefined(run.Status) && Enum.IsDefined(run.QueryMode) &&
            run.Attempts is >= 0 and <= 3 && run.Issues is not null && run.Reason == reason);
        Require((int)run!.Status == (int)state); // Both explicitly ordered Succeeded, Partial, Failed, Unsupported.
        Observation(topology.Displays, DataSource.DisplayConfig);
        Require(state == WorkerResultState.Succeeded ? reason == ReasonCode.None : reason != ReasonCode.None);
        foreach (var issue in run.Issues)
            Require(issue is not null && Enum.IsDefined(issue.Operation) && Enum.IsDefined(issue.Reason) && issue.Reason != ReasonCode.None &&
                issue.NativeErrorCode is not < 0 && !(issue.Operation == CollectionOperation.TargetName && issue.Reason == ReasonCode.MissingValue && issue.NativeErrorCode is not null));
        if (state is WorkerResultState.Failed or WorkerResultState.Unsupported)
        {
            var unsupportedReason = reason is ReasonCode.ApiUnavailable or ReasonCode.NotSupported or ReasonCode.InteropLayoutUnsupported or ReasonCode.NotImplemented;
            Require((state == WorkerResultState.Unsupported) == unsupportedReason);
            Require(topology.Displays.State == (state == WorkerResultState.Failed ? DataState.Failed : DataState.Unsupported) && topology.Displays.Reason == reason);
            if (reason != ReasonCode.NotImplemented)
            {
                var queries = run.Issues.Where(i => i.Operation == CollectionOperation.QueryPaths).ToArray();
                Require(queries.Length == Math.Max(1, run.Attempts) && queries[^1].Reason == reason);
                Require(queries.SkipLast(1).All(i => i.Reason == ReasonCode.TopologyChanged && i.NativeErrorCode == 122));
            }
            return;
        }
        Require(topology.Displays.State == DataState.Available && run.Attempts >= 1 && run.QueryMode != DisplayQueryMode.NotQueried);
        var blocking = run.Issues.Where(i => i.BlocksCompletion()).ToArray();
        Require(state == WorkerResultState.Succeeded ? blocking.Length == 0 : blocking.Length > 0 && reason == blocking[0].Reason);
        Require(run.Issues.Where(i => i.Operation == CollectionOperation.QueryPaths).All(i => i.Reason == ReasonCode.TopologyChanged && i.NativeErrorCode == 122));
        Require(run.Issues.Count(i => i.Operation == CollectionOperation.QueryPaths) == run.Attempts - 1);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = new Dictionary<string, Observation<AdapterMatch>>(StringComparer.Ordinal);
        void Issue<T>(Observation<T> value, params CollectionOperation[] operations) where T : class
        {
            if (value.State != DataState.Available)
                Require(run.Issues.Any(i => operations.Contains(i.Operation) && i.Reason == value.Reason));
        }
        foreach (var d in topology.Displays.Value!)
        {
            Require(d is not null); Label(d!.Id, "display"); Require(ids.Add(d.Id));
            Label(d.SourceId, "source"); Label(d.TargetId, "target"); Label(d.SourceAdapterId, "adapter"); Label(d.TargetAdapterId, "adapter");
            Require(!sources.TryGetValue(d.SourceId, out var sa) || sa == d.SourceAdapterId); sources[d.SourceId] = d.SourceAdapterId;
            Require(!targets.TryGetValue(d.TargetId, out var ta) || ta == d.TargetAdapterId); targets[d.TargetId] = d.TargetAdapterId;
            Require(d.PathActive && d.QueryMode == run.QueryMode);
            foreach (var match in new[] { d.SourceAdapter, d.TargetAdapter })
            {
                Observation(match, DataSource.DisplayConfig, DataSource.SetupApiInstanceJoin);
                if (match.State == DataState.Available)
                {
                    Require(match.Source == DataSource.SetupApiInstanceJoin && match.Value!.Evidence == AdapterMatchEvidence.ExactSetupApiInstanceId && match.Value.Confidence == AdapterMatchConfidence.Exact);
                    Label(match.Value!.GpuId, "gpu");
                }
                Issue(match, CollectionOperation.AdapterName, CollectionOperation.ResolveAdapter);
            }
            foreach (var (id, match) in new[] { (d.SourceAdapterId, d.SourceAdapter), (d.TargetAdapterId, d.TargetAdapter) })
            { Require(!matches.TryGetValue(id, out var prior) || prior == match); matches[id] = match; }
            foreach (var text in new[] { d.SourceGdiName, d.OutputTechnology, d.Rotation, d.ScanLineOrdering, d.CloneGroupId }) Observation(text, DataSource.DisplayConfig);
            MonitorName(d.Name);
            Observation(d.SourceResolution, DataSource.DisplayConfig); Observation(d.PathRefreshRate, DataSource.DisplayConfig);
            Observation(d.SignalRefreshRate, DataSource.DisplayConfig); Observation(d.RefreshRateBoost, DataSource.DisplayConfig);
            foreach (var text in new[] { d.SourceGdiName, d.OutputTechnology, d.Rotation, d.ScanLineOrdering })
                if (text.State == DataState.Available) Require(!string.IsNullOrWhiteSpace(text.Value));
            Issue(d.SourceGdiName, CollectionOperation.SourceName); Issue(d.Name, CollectionOperation.TargetName);
            Issue(d.SourceResolution, CollectionOperation.DecodeMode); Issue(d.PathRefreshRate, CollectionOperation.DecodeMode); Issue(d.SignalRefreshRate, CollectionOperation.DecodeMode);
            foreach (var field in new[] { d.OutputTechnology, d.Rotation, d.ScanLineOrdering }) Issue(field, CollectionOperation.ValidatePath);
            Require(d.CloneGroupId.State == DataState.Available || d.CloneGroupId.State == DataState.Unknown && d.CloneGroupId.Reason == ReasonCode.MissingValue);
            if (d.SourceResolution.Value is { } size) Require(size.WidthPixels > 0 && size.HeightPixels > 0);
            foreach (var rate in new[] { d.PathRefreshRate, d.SignalRefreshRate })
                if (rate.Value is { } value) Require(value.Numerator > 0 && value.Denominator > 0);
            if (d.CloneGroupId.Value is { } clone) Label(clone, "clone");
            Require(d.QueryMode == DisplayQueryMode.VirtualModeAndRefreshAware ? d.RefreshRateBoost.State == DataState.Available :
                d.RefreshRateBoost.State == DataState.Unsupported && d.RefreshRateBoost.Reason == ReasonCode.NotSupported);
            if (d.Name.State == DataState.Unknown && d.Name.Reason == ReasonCode.MissingValue)
                Require(run.Issues.Any(i => i.Operation == CollectionOperation.TargetName && i.Reason == ReasonCode.MissingValue));
            if (!d.TargetAvailable) Require(run.Issues.Any(i => i.Operation == CollectionOperation.ValidatePath && i.Reason == ReasonCode.TargetUnavailable));
        }
    }
}
