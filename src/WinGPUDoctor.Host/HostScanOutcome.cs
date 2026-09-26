using WinGPUDoctor.Core;

namespace WinGPUDoctor.Host;

public enum HostScanOutcomeKind { Completed, Cancelled, CollectionFailed }

public sealed class HostScanOutcome
{
    public HostScanOutcomeKind Kind { get; }
    public ShareableReport? Report { get; }
    public bool IsIncomplete { get; }

    private HostScanOutcome(HostScanOutcomeKind kind, ShareableReport? report = null, bool isIncomplete = false)
    {
        Kind = kind;
        Report = report;
        IsIncomplete = isIncomplete;
    }

    internal static HostScanOutcome Completed(ShareableReport report, bool isIncomplete) =>
        new(HostScanOutcomeKind.Completed, report, isIncomplete);
    internal static HostScanOutcome Cancelled() => new(HostScanOutcomeKind.Cancelled);
    internal static HostScanOutcome CollectionFailed() => new(HostScanOutcomeKind.CollectionFailed);
}
