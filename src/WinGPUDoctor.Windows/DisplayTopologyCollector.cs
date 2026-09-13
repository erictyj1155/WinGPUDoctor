using WinGPUDoctor.Core;

namespace WinGPUDoctor.Windows;

internal sealed record GpuCorrelationIdentity(string GpuId, string? InstanceId);
internal sealed record TopologyResult(Observation<IReadOnlyList<DisplayFacts>> Displays, CollectorRun Run);

internal sealed class DisplayTopologyCollector(IDisplayConfigApi api, IAdapterInstanceResolver resolver, DisplayQueryMode queryMode)
{
    internal const int MaxAttempts = 3;
    internal static DisplayQueryMode CurrentQueryMode => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
        ? DisplayQueryMode.VirtualModeAndRefreshAware : OperatingSystem.IsWindowsVersionAtLeast(10)
        ? DisplayQueryMode.VirtualModeAware : DisplayQueryMode.ActivePaths;
    internal static uint Flags(DisplayQueryMode mode) => mode switch
    {
        DisplayQueryMode.ActivePaths => Ccd.ActivePaths,
        DisplayQueryMode.VirtualModeAware => Ccd.ActivePaths | Ccd.VirtualModeAware,
        DisplayQueryMode.VirtualModeAndRefreshAware => Ccd.ActivePaths | Ccd.VirtualModeAware | Ccd.VirtualRefreshAware,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
    internal static ReasonCode ErrorReason(int code, bool sessionQuery = true) => code switch
    {
        Ccd.AccessDenied => sessionQuery ? ReasonCode.SessionAccessDenied : ReasonCode.AccessDenied,
        Ccd.NotSupported => ReasonCode.NotSupported,
        Ccd.InsufficientBuffer => ReasonCode.TopologyChanged,
        Ccd.ApiUnavailable => ReasonCode.ApiUnavailable,
        Ccd.InvalidLayout => ReasonCode.InteropLayoutUnsupported,
        1167 => ReasonCode.TopologyChanged, // DEVICE_NOT_CONNECTED; generic failures retain NativeError.
        _ => ReasonCode.NativeError
    };
    private static DataState ErrorState(ReasonCode reason) => reason is ReasonCode.ApiUnavailable or ReasonCode.NotSupported or ReasonCode.InteropLayoutUnsupported
        ? DataState.Unsupported : DataState.Failed;
    private static int? ExportError(int error) => error is -1 or -2 or -3 ? null : error;
    private static Observation<T> Missing<T>(ReasonCode reason = ReasonCode.MissingValue, DataSource source = DataSource.DisplayConfig) where T : class =>
        Observation<T>.Absent(DataState.Unknown, source, reason);
    private static Observation<T> Known<T>(T value) where T : class => Observation<T>.Known(value, DataSource.DisplayConfig);

    // Native/provider exceptions are reduced to a code; messages and identifiers are never logged.
    private static NativeResult<T> Invoke<T>(Func<NativeResult<T>> call)
    {
        try { return call(); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        { return new(Ccd.ApiUnavailable, default!); }
        catch (Exception) { return new(-3, default!); }
    }

    internal TopologyResult Collect(IReadOnlyList<GpuCorrelationIdentity> inventory)
    {
        var issues = new List<CollectionIssue>();
        var attempts = 0;
        TopologyResult Failure(ReasonCode reason, int? error = null)
        {
            issues.Add(new(CollectionOperation.QueryPaths, reason, error is { } code ? ExportError(code) : null));
            var state = ErrorState(reason);
            return new(Observation<IReadOnlyList<DisplayFacts>>.Absent(state, DataSource.DisplayConfig, reason),
                new(DataSource.DisplayConfig, state == DataState.Unsupported ? CollectorStatus.Unsupported : CollectorStatus.Failed, reason)
                { Attempts = attempts, QueryMode = queryMode, Issues = issues.ToArray() });
        }
        try
        {
            var flags = Flags(queryMode);
            for (attempts = 1; attempts <= MaxAttempts; attempts++)
            {
                var error = api.GetBufferSizes(flags, out var pathCount, out var modeCount);
                if (error != 0) return Failure(ErrorReason(error), error);
                if (pathCount > 128 || modeCount > 512) return Failure(ReasonCode.ResourceLimit);
                // Pass non-null arrays even for an empty configuration and query again to detect a sizing race.
                var paths = new NativePath[Math.Max(1, pathCount)];
                var modes = new NativeMode[Math.Max(1, modeCount)];
                error = api.Query(flags, ref pathCount, paths, ref modeCount, modes);
                if (error == Ccd.InsufficientBuffer)
                {
                    if (attempts == MaxAttempts) return Failure(ReasonCode.TopologyChanged, error);
                    issues.Add(new(CollectionOperation.QueryPaths, ReasonCode.TopologyChanged, error));
                    continue;
                }
                if (error != 0) return Failure(ErrorReason(error), error);
                if (pathCount > paths.Length || modeCount > modes.Length) return Failure(ReasonCode.InvalidValue);
                return Transform(paths.Take((int)pathCount).ToArray(), modes.Take((int)modeCount).ToArray(), inventory, issues, attempts);
            }
            return Failure(ReasonCode.TopologyChanged);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        { return Failure(ReasonCode.ApiUnavailable); }
        catch (Exception) { return Failure(ReasonCode.NativeError); }
    }

    private TopologyResult Transform(NativePath[] paths, NativeMode[] modes, IReadOnlyList<GpuCorrelationIdentity> inventory,
        List<CollectionIssue> issues, int attempts)
    {
        var adapters = new Dictionary<Luid, string>();
        var sources = new Dictionary<(Luid, uint), string>();
        var targets = new Dictionary<(Luid, uint), string>();
        var clones = new Dictionary<(Luid, uint), string>();
        var matches = new Dictionary<Luid, Observation<AdapterMatch>>();
        var sourceNames = new Dictionary<(Luid, uint), Observation<string>>();
        var targetNames = new Dictionary<(Luid, uint), Observation<string>>();
        static string Label<K>(Dictionary<K, string> labels, K key, string prefix) where K : notnull
        {
            if (!labels.TryGetValue(key, out var label)) { label = $"{prefix}-{labels.Count + 1}"; labels.Add(key, label); }
            return label;
        }
        Observation<T> ApiFailure<T>(int error, CollectionOperation operation, bool session = true) where T : class
        {
            var reason = ErrorReason(error, session);
            issues.Add(new(operation, reason, ExportError(error)));
            return Observation<T>.Absent(ErrorState(reason), session ? DataSource.DisplayConfig : DataSource.SetupApiInstanceJoin, reason);
        }
        Observation<string> Text(string? value, CollectionOperation operation)
        {
            if (!string.IsNullOrWhiteSpace(value)) return Known(value);
            issues.Add(new(operation, ReasonCode.MissingValue, null));
            return Missing<string>();
        }
        Observation<AdapterMatch> Match(Luid luid)
        {
            if (matches.TryGetValue(luid, out var cached)) return cached;
            var path = Invoke(() => api.AdapterName(luid));
            Observation<AdapterMatch> value;
            if (path.Error != 0) value = ApiFailure<AdapterMatch>(path.Error, CollectionOperation.AdapterName);
            else if (string.IsNullOrWhiteSpace(path.Value))
            {
                issues.Add(new(CollectionOperation.AdapterName, ReasonCode.MissingValue, null));
                value = Missing<AdapterMatch>();
            }
            else
            {
                var id = Invoke(() => resolver.Resolve(path.Value));
                if (id.Error != 0) value = ApiFailure<AdapterMatch>(id.Error, CollectionOperation.ResolveAdapter, false);
                else
                {
                    var candidates = string.IsNullOrWhiteSpace(id.Value) ? [] : inventory.Where(g =>
                        !string.IsNullOrWhiteSpace(g.InstanceId) && string.Equals(g.InstanceId, id.Value, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (candidates.Length == 1) value = Observation<AdapterMatch>.Known(
                        new(candidates[0].GpuId, AdapterMatchEvidence.ExactSetupApiInstanceId, AdapterMatchConfidence.Exact), DataSource.SetupApiInstanceJoin);
                    else
                    {
                        var reason = candidates.Length == 0 ? ReasonCode.UnmatchedAdapter : ReasonCode.AmbiguousAdapter;
                        issues.Add(new(CollectionOperation.ResolveAdapter, reason, null));
                        value = Missing<AdapterMatch>(reason, DataSource.SetupApiInstanceJoin);
                    }
                }
            }
            matches.Add(luid, value);
            return value;
        }
        NativeMode? Mode(uint index, uint invalid, uint type, Luid adapter, uint id)
        {
            var reason = index == invalid ? ReasonCode.MissingValue : ReasonCode.InvalidModeIndex;
            if (index != invalid && index < modes.Length)
            {
                var mode = modes[index];
                if (mode.InfoType == type && mode.AdapterId == adapter && mode.Id == id) return mode;
            }
            issues.Add(new(CollectionOperation.DecodeMode, reason, null));
            return null;
        }
        Observation<RationalRate> Rate(NativeRational value)
        {
            if (value.Numerator != 0 && value.Denominator != 0) return Known(new RationalRate(value.Numerator, value.Denominator));
            issues.Add(new(CollectionOperation.DecodeMode, ReasonCode.InvalidValue, null));
            return Missing<RationalRate>(ReasonCode.InvalidValue);
        }
        var displays = new List<DisplayFacts>();
        foreach (var path in paths)
        {
            if ((path.Flags & Ccd.PathActive) == 0)
            { issues.Add(new(CollectionOperation.ValidatePath, ReasonCode.InactivePathSkipped, null)); continue; }
            var source = (path.Source.AdapterId, path.Source.Id);
            var target = (path.Target.AdapterId, path.Target.Id);
            if (!sourceNames.TryGetValue(source, out var sourceName))
            {
                var result = Invoke(() => api.SourceName(source.AdapterId, source.Id));
                sourceName = result.Error == 0 ? Text(result.Value, CollectionOperation.SourceName) : ApiFailure<string>(result.Error, CollectionOperation.SourceName);
                sourceNames.Add(source, sourceName);
            }
            if (!targetNames.TryGetValue(target, out var name))
            {
                var result = Invoke(() => api.TargetName(target.AdapterId, target.Id));
                // MonitorDevicePath, EDID fields and connector instance never leave native working memory.
                name = result.Error == 0 ? Text(result.Value.FriendlyName, CollectionOperation.TargetName) : ApiFailure<string>(result.Error, CollectionOperation.TargetName);
                targetNames.Add(target, name);
            }
            var virtualMode = (queryMode is DisplayQueryMode.VirtualModeAware or DisplayQueryMode.VirtualModeAndRefreshAware) && (path.Flags & Ccd.PathVirtualMode) != 0;
            var sourceIndex = virtualMode ? path.Source.ModeIndex >> 16 : path.Source.ModeIndex;
            var targetIndex = virtualMode ? path.Target.ModeIndex >> 16 : path.Target.ModeIndex;
            var invalid = virtualMode ? ushort.MaxValue : uint.MaxValue;
            var sourceMode = Mode(sourceIndex, invalid, 1, source.AdapterId, source.Id);
            var targetMode = Mode(targetIndex, invalid, 2, target.AdapterId, target.Id);
            var resolution = Missing<PixelSize>(sourceIndex == invalid ? ReasonCode.MissingValue : ReasonCode.InvalidModeIndex);
            if (sourceMode is { } sm)
            {
                if (sm.SourceMode.Width > 0 && sm.SourceMode.Height > 0) resolution = Known(new PixelSize(sm.SourceMode.Width, sm.SourceMode.Height));
                else { issues.Add(new(CollectionOperation.DecodeMode, ReasonCode.InvalidValue, null)); resolution = Missing<PixelSize>(ReasonCode.InvalidValue); }
            }
            var signalRate = targetMode is { } tm ? Rate(tm.TargetSignal.VSync) :
                Missing<RationalRate>(targetIndex == invalid ? ReasonCode.MissingValue : ReasonCode.InvalidModeIndex);
            var cloneId = virtualMode && sourceIndex == invalid && (path.Source.ModeIndex & 0xffff) != 0xffff
                ? Known(Label(clones, (source.AdapterId, path.Source.ModeIndex & 0xffff), "clone")) : Missing<string>();
            if (path.Target.TargetAvailable == 0) issues.Add(new(CollectionOperation.ValidatePath, ReasonCode.TargetUnavailable, null));
            Observation<string> EnumText(string? value) => value is null ? Missing<string>(ReasonCode.NotSupported) : Known(value);
            var output = EnumText(OutputName(path.Target.OutputTechnology));
            var rotation = EnumText(path.Target.Rotation switch { 1 => "identity", 2 => "rotate90", 3 => "rotate180", 4 => "rotate270", _ => null });
            var scan = EnumText(path.Target.ScanLineOrdering switch { 1 => "progressive", 2 => "interlacedUpperFirst", 3 => "interlacedLowerFirst", _ => null });
            if (output.State != DataState.Available || rotation.State != DataState.Available || scan.State != DataState.Available)
                issues.Add(new(CollectionOperation.ValidatePath, ReasonCode.NotSupported, null));
            displays.Add(new($"display-{displays.Count + 1}", Label(sources, source, "source"), Label(targets, target, "target"),
                Label(adapters, source.AdapterId, "adapter"), Label(adapters, target.AdapterId, "adapter"), Match(source.AdapterId), Match(target.AdapterId),
                sourceName, name, output, resolution, Rate(path.Target.RefreshRate), signalRate, rotation, scan, true, path.Target.TargetAvailable != 0,
                queryMode == DisplayQueryMode.VirtualModeAndRefreshAware ? Known(new FlagValue((path.Flags & Ccd.PathBoostRefresh) != 0)) :
                    Observation<FlagValue>.Absent(DataState.Unsupported, DataSource.DisplayConfig, ReasonCode.NotSupported), cloneId, queryMode));
        }
        // A recovered size race is documented but does not make the final coherent path arrays partial.
        var incomplete = issues.Any(i => i.Operation != CollectionOperation.QueryPaths);
        return new(Observation<IReadOnlyList<DisplayFacts>>.Known(displays.ToArray(), DataSource.DisplayConfig),
            new(DataSource.DisplayConfig, incomplete ? CollectorStatus.Partial : CollectorStatus.Succeeded, incomplete ? issues.First(i => i.Operation != CollectionOperation.QueryPaths).Reason : ReasonCode.None)
            { Attempts = attempts, QueryMode = queryMode, Issues = issues.ToArray() });
    }

    internal static string? OutputName(uint value) => value switch
    {
        0 => "hd15", 1 => "sVideo", 2 => "compositeVideo", 3 => "componentVideo", 4 => "dvi", 5 => "hdmi", 6 => "lvds",
        8 => "dJpn", 9 => "sdi", 10 => "displayPortExternal", 11 => "displayPortEmbedded", 12 => "udiExternal", 13 => "udiEmbedded",
        14 => "sdTvDongle", 15 => "miracast", 16 => "indirectWired", 17 => "indirectVirtual", 18 => "displayPortUsbTunnel",
        0x80000000 => "internal", 0xffffffff => "other", _ => null
    };
}
