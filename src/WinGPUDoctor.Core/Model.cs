using System.Text.Json.Serialization;

namespace WinGPUDoctor.Core;

public enum DataState { Available, Unknown, Unsupported, Failed, Redacted }
public enum DataSource { WmiOperatingSystem, WmiComputerSystem, WmiVideoController, WmiSignedDriver, DisplayConfig, NotCollected, SetupApiInstanceJoin }
public enum ReasonCode { None, MissingValue, NotImplemented, NonPciDevice, InvalidValue, NoMatchingDriver, AmbiguousDriver, AccessDenied, ProviderUnavailable, Timeout, QueryFailed, SensitiveValue,
    ApiUnavailable, NotSupported, SessionAccessDenied, TopologyChanged, NativeError, UnmatchedAdapter, AmbiguousAdapter, InvalidModeIndex, TargetUnavailable, InactivePathSkipped, ResourceLimit, InteropLayoutUnsupported }
public enum CollectorStatus { Succeeded, Partial, Failed, Unsupported }
public enum WarningCode { InventoryOnly, ProviderReportedValues, TopologyNotCollected, ReviewBeforeSharing, CollectionIncomplete, ValuesRedacted, TopologyIsNotRendering }

// Observed values carry provenance. Unavailable values never use a guessed substitute.
public sealed record Observation<T> where T : class
{
    public DataState State { get; }
    public T? Value { get; }
    public DataSource Source { get; }
    public ReasonCode Reason { get; }

    [JsonConstructor]
    public Observation(DataState state, T? value, DataSource source, ReasonCode reason)
    {
        if (!Enum.IsDefined(state) || !Enum.IsDefined(source) || !Enum.IsDefined(reason))
            throw new ArgumentException("Undefined observation enum.");
        if (state == DataState.Available ? value is null || reason != ReasonCode.None : value is not null || reason == ReasonCode.None)
            throw new ArgumentException("Available observations need a value; unavailable observations need a reason and no value.");
        State = state; Value = value; Source = source; Reason = reason;
    }

    public static Observation<T> Known(T value, DataSource source) => new(DataState.Available, value, source, ReasonCode.None);
    public static Observation<T> Absent(DataState state, DataSource source, ReasonCode reason) => new(state, null, source, reason);
}

public sealed record SystemFacts(Observation<string> WindowsVersion, Observation<string> WindowsBuild,
    Observation<string> Manufacturer, Observation<string> Model);
public sealed record DriverFacts(Observation<string> Provider, Observation<string> Version, Observation<string> Date);
public sealed record GpuFacts(string Id, Observation<string> Name, Observation<string> PciVendorId,
    Observation<string> PciDeviceId, Observation<string> Classification, DriverFacts Driver);
public sealed record PixelSize(uint WidthPixels, uint HeightPixels);
public sealed record RationalRate(uint Numerator, uint Denominator);
public sealed record FlagValue(bool Enabled);
public enum AdapterMatchEvidence { ExactSetupApiInstanceId }
public enum AdapterMatchConfidence { Exact }
public sealed record AdapterMatch(string GpuId, AdapterMatchEvidence Evidence, AdapterMatchConfidence Confidence);
public enum DisplayQueryMode { NotQueried, ActivePaths, VirtualModeAware, VirtualModeAndRefreshAware }
// One entry per active path. Shared SourceId or TargetId preserves relationships; labels are report-local.
public sealed record DisplayFacts(string Id, string SourceId, string TargetId, string SourceAdapterId, string TargetAdapterId,
    Observation<AdapterMatch> SourceAdapter, Observation<AdapterMatch> TargetAdapter,
    Observation<string> SourceGdiName, Observation<string> Name, Observation<string> OutputTechnology,
    Observation<PixelSize> SourceResolution, Observation<RationalRate> PathRefreshRate, Observation<RationalRate> SignalRefreshRate,
    Observation<string> Rotation, Observation<string> ScanLineOrdering, bool PathActive, bool TargetAvailable,
    Observation<FlagValue> RefreshRateBoost, Observation<string> CloneGroupId, DisplayQueryMode QueryMode);
public sealed record CollectedFacts(SystemFacts System, Observation<IReadOnlyList<GpuFacts>> Gpus,
    Observation<IReadOnlyList<DisplayFacts>> Displays);
public enum CollectionOperation { QueryPaths, SourceName, TargetName, AdapterName, ResolveAdapter, DecodeMode, ValidatePath }
public sealed record CollectionIssue(CollectionOperation Operation, ReasonCode Reason, int? NativeErrorCode)
{
    // A successful target-name packet may legitimately omit only the optional monitor friendly name.
    // Keep that metadata diagnostic visible without treating it as incomplete collection.
    private bool IsNonBlockingMetadata() =>
        Operation == CollectionOperation.TargetName && Reason == ReasonCode.MissingValue;

    // Only recovered insufficient-buffer retries reach a successful transform, so they remain
    // visible in CollectionIssue history without making the final coherent snapshot partial.
    public bool BlocksCompletion() => Operation != CollectionOperation.QueryPaths && !IsNonBlockingMetadata();
}
public sealed record CollectorRun(DataSource Source, CollectorStatus Status, ReasonCode Reason)
{
    public int Attempts { get; init; } = 1;
    public DisplayQueryMode QueryMode { get; init; } = DisplayQueryMode.NotQueried;
    public IReadOnlyList<CollectionIssue> Issues { get; init; } = [];
    public bool IsIncomplete() => Status is CollectorStatus.Partial or CollectorStatus.Failed ||
        Status == CollectorStatus.Unsupported && Reason != ReasonCode.NotImplemented;
}
public sealed record CollectionSnapshot(CollectedFacts Facts, IReadOnlyList<CollectorRun> Collection);
public sealed record DiagnosticFinding(string Id, string Severity, string Message, IReadOnlyList<string> Evidence);
public sealed record PrivacySummary(string PolicyVersion, int RedactedFields);
public sealed record DiagnosticReport(string SchemaVersion, string ToolVersion, DateOnly CollectedOnUtc,
    CollectedFacts Facts, IReadOnlyList<DiagnosticFinding> Findings, IReadOnlyList<WarningCode> Warnings,
    IReadOnlyList<CollectorRun> Collection, PrivacySummary Privacy);

public interface IDiagnosticCollector { CollectionSnapshot Collect(); }

// Only the privacy boundary can construct this type. Public exporters cannot take a raw snapshot.
public sealed class ShareableReport
{
    internal DiagnosticReport Report { get; }
    internal ShareableReport(DiagnosticReport report) => Report = report;
}
