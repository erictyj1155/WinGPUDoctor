using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
namespace WinGPUDoctor.Supervisor;
internal enum SupervisorPhase { Created, Ready, AwaitingReady, AwaitingStart, AwaitingAttemptStarted, AwaitingResult, Cleanup, Completed, Poisoned }
internal enum SupervisorTerminalKind { ResultAccepted, Timeout, Cancelled, ProtocolFailure, ResourceLimit, HostFailure, Skipped }
internal sealed record CompletedOperationResult(WorkerOperation Operation, ResultFrame Result);
internal sealed record SupervisorOutcome(SupervisorTerminalKind Kind, ReasonCode Reason, ResultFrame? Result, string? Code = null, int Attempts = 0);
internal sealed record SupervisorSnapshot(SupervisorPhase Phase, int RemainingOperations, long? OverallDeadline,
    long? OperationDeadline, long? CleanupDeadline, SupervisorOutcome? LastOutcome, bool AdmissionPoisoned,
    IReadOnlyList<CompletedOperationResult> CompletedResults, bool HostCancelled);
internal sealed class CollectionSupervisorStateMachine(CollectionTimingPolicy policy, long timestampFrequency)
{
    private readonly List<CompletedOperationResult> _completed = [];
    private SupervisorOutcome? _pending;
    private WorkerOperation _operation;
    private int _remaining;
    private int _attempts;
    private long _cleanupLimit;
    private bool _cancelled;
    internal SupervisorPhase Phase { get; private set; } = SupervisorPhase.Created;
    internal SupervisorOutcome? LastOutcome { get; private set; }
    internal bool AdmissionPoisoned { get; private set; }
    internal IReadOnlyList<CompletedOperationResult> CompletedResults => _completed;
    internal long? OverallDeadline { get; private set; }
    internal long? OperationDeadline { get; private set; }
    internal long? CleanupDeadline { get; private set; }
    internal long BookkeepingDeadline => Deadline.Subtract(OverallDeadline!.Value, Ticks(policy.FinalBookkeepingReserve));
    internal long OperationCleanupLimit => _cleanupLimit;
    internal SupervisorSnapshot Snapshot() => new(Phase, _remaining, OverallDeadline, OperationDeadline, CleanupDeadline, LastOutcome, AdmissionPoisoned, _completed.ToArray(), _cancelled);
    internal void Begin(int operationCount, long now)
    {
        if (Phase != SupervisorPhase.Created || operationCount is < 1 or > 5) throw new InvalidOperationException("Invalid collection start.");
        policy.Validate(); _ = Ticks(TimeSpan.Zero);
        _remaining = operationCount; OverallDeadline = Deadline.Add(now, Ticks(policy.OverallBudget)); Phase = SupervisorPhase.Ready;
    }
    internal bool TryStartOperation(WorkerOperation operation, long now, out SupervisorOutcome? skipped)
    {
        skipped = null;
        if (Phase != SupervisorPhase.Ready || AdmissionPoisoned || _cancelled) return false;
        var later = Deadline.Add(Ticks(policy.LaterOperationReservation), Ticks(policy.CleanupAllowance));
        var reservation = (long)Math.Min(long.MaxValue, (decimal)later * (_remaining - 1));
        _cleanupLimit = Deadline.Subtract(BookkeepingDeadline, reservation);
        var cap = Deadline.Subtract(_cleanupLimit, Ticks(policy.CleanupAllowance));
        var deadline = Math.Min(Deadline.Add(now, Ticks(policy.OperationBudget)), cap);
        _remaining--; _operation = operation; _attempts = 0; _pending = null; LastOutcome = null;
        if (deadline <= now)
        {
            LastOutcome = skipped = new(SupervisorTerminalKind.Skipped, ReasonCode.Timeout, null, "insufficient-budget", 0);
            Phase = _remaining > 0 ? SupervisorPhase.Ready : SupervisorPhase.Completed;
            return false;
        }
        OperationDeadline = deadline; Phase = SupervisorPhase.AwaitingReady; return true;
    }
    private bool Advance(SupervisorPhase from, SupervisorPhase to, long now)
    {
        if (Phase != from || _cancelled) return false;
        if (now >= OperationDeadline) { TryTimeout(now); return false; }
        Phase = to; return true;
    }
    internal bool TryRecordReady(long now) => Advance(SupervisorPhase.AwaitingReady, SupervisorPhase.AwaitingStart, now);
    internal bool TryRecordStartSent(long now) => Advance(SupervisorPhase.AwaitingStart, SupervisorPhase.AwaitingAttemptStarted, now);
    internal bool TryRecordAttemptStarted(long now)
    {
        if (Phase is not (SupervisorPhase.AwaitingAttemptStarted or SupervisorPhase.AwaitingResult)) return false;
        if (!Advance(Phase, SupervisorPhase.AwaitingResult, now)) return false;
        _attempts++; return true;
    }
    internal bool TryAcceptResult(ResultFrame result, long now)
    {
        var preAttemptFailure = Phase == SupervisorPhase.AwaitingAttemptStarted &&
            result.Payload?.DisplayActiveTopology?.Run is { Attempts: 0, Status: CollectorStatus.Failed or CollectorStatus.Unsupported };
        if ((Phase != SupervisorPhase.AwaitingResult && !preAttemptFailure) || result.Operation != _operation || _cancelled) return false;
        if (now >= OperationDeadline) { TryTimeout(now); return false; }
        _pending = new(SupervisorTerminalKind.ResultAccepted, result.Reason, result, "validated-result", _attempts);
        _completed.Add(new(_operation, result)); // Accepted independent data survives a later host veto.
        BeginCleanup(now); return true;
    }
    internal bool TryProtocolFailure(ReasonCode reason, long now)
    {
        if (_pending is not null || LastOutcome is not null) return false;
        if (now >= OperationDeadline) return TryTimeout(now);
        _pending = new(reason == ReasonCode.ResourceLimit ? SupervisorTerminalKind.ResourceLimit : SupervisorTerminalKind.ProtocolFailure,
            reason == ReasonCode.ResourceLimit ? reason : ReasonCode.QueryFailed, null, "protocol", _attempts);
        BeginCleanup(now); return true;
    }
    internal bool TryTimeout(long now)
    {
        if (_pending is not null || LastOutcome is not null) return false;
        _pending = new(SupervisorTerminalKind.Timeout, ReasonCode.Timeout, null, "deadline", _attempts); BeginCleanup(now); return true;
    }
    internal bool TryCancel(long now)
    {
        _cancelled = true;
        if (Phase == SupervisorPhase.Cleanup) return true; // Preserve pending accepted operation; host cancellation still vetoes continuation.
        if (Phase is SupervisorPhase.Completed or SupervisorPhase.Poisoned) return false;
        _pending ??= new(SupervisorTerminalKind.Cancelled, ReasonCode.QueryFailed, null, "host-cancelled", _attempts);
        BeginCleanup(now); return true;
    }
    internal void HostFailure(AdmissionFailure failure, long now)
    {
        AdmissionPoisoned = true;
        _pending = new(SupervisorTerminalKind.HostFailure, ReasonCode.NativeError, null, failure.ToString(), _attempts);
        BeginCleanup(now);
    }
    internal bool TryPoison() { AdmissionPoisoned = true; Phase = SupervisorPhase.Poisoned; return true; }
    internal void OmitOperation()
    {
        if (Phase != SupervisorPhase.Ready || _remaining < 1 || AdmissionPoisoned || _cancelled)
            throw new InvalidOperationException("Operation cannot be omitted from its current phase.");
        _remaining--;
        if (_remaining == 0) Phase = SupervisorPhase.Completed;
    }
    private void BeginCleanup(long now)
    {
        if (Phase != SupervisorPhase.Cleanup)
            CleanupDeadline = Math.Min(Deadline.Add(now, Ticks(policy.CleanupAllowance)), Math.Min(_cleanupLimit == 0 ? BookkeepingDeadline : _cleanupLimit, BookkeepingDeadline));
        Phase = SupervisorPhase.Cleanup;
    }
    internal bool FinalizeCleanup(bool confirmed, long now, bool ownedResources = true)
    {
        if (Phase != SupervisorPhase.Cleanup) return false;
        if (!confirmed || ownedResources && now >= CleanupDeadline)
        {
            AdmissionPoisoned = true; LastOutcome = new(SupervisorTerminalKind.HostFailure, ReasonCode.NativeError, null, "cleanup-unconfirmed", _attempts);
        }
        else LastOutcome = _pending;
        if (_cancelled && !AdmissionPoisoned) LastOutcome = new(SupervisorTerminalKind.Cancelled, ReasonCode.QueryFailed, null, "host-cancelled", _attempts);
        _pending = null; OperationDeadline = null; CleanupDeadline = null;
        Phase = AdmissionPoisoned ? SupervisorPhase.Poisoned : _cancelled || _remaining == 0 ? SupervisorPhase.Completed : SupervisorPhase.Ready;
        return true;
    }
    internal long ClipDeadline(long now, TimeSpan budget) => Math.Min(Deadline.Add(now, Ticks(budget)), OperationDeadline ?? BookkeepingDeadline);
    private long Ticks(TimeSpan duration) => Deadline.Ticks(duration, timestampFrequency);
}
