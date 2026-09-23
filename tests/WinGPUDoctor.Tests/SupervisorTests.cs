using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;
public class SupervisorTests
{
    internal static readonly CollectionTimingPolicy Policy = new(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    private static CollectionSupervisorStateMachine Started(int count = 1, long now = 0)
    {
        var s = new CollectionSupervisorStateMachine(Policy, 1000); s.Begin(count, now);
        Assert.True(s.TryStartOperation(WorkerOperation.WmiOperatingSystem, now, out _)); Handshake(s, now); return s;
    }
    private static void Handshake(CollectionSupervisorStateMachine s, long now)
    { Assert.True(s.TryRecordReady(now)); Assert.True(s.TryRecordStartSent(now)); Assert.True(s.TryRecordAttemptStarted(now)); }
    [Fact]
    public void ResourceLimitArrivingAtDeadlineCannotReplaceTimeout()
    {
        var s = Started();
        Assert.True(s.TryProtocolFailure(ReasonCode.ResourceLimit, s.OperationDeadline!.Value));
        Assert.True(s.FinalizeCleanup(true, s.OperationDeadline!.Value + 1));
        Assert.Equal(SupervisorTerminalKind.Timeout, s.LastOutcome!.Kind);
        Assert.Equal(1, s.LastOutcome.Attempts);
    }
    [Fact]
    public void OmittedDriverReleasesReservationWithoutResettingOverallDeadline()
    {
        var policy = new CollectionTimingPolicy(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        var s = new CollectionSupervisorStateMachine(policy, 1000); s.Begin(5, 0);
        for (var i = 0; i < 3; i++)
        {
            var now = i * 14000L;
            Assert.True(s.TryStartOperation((WorkerOperation)i, now, out _));
            Handshake(s, now); Assert.True(s.TryTimeout(now + 10000));
            Assert.True(s.FinalizeCleanup(true, now + 13999));
        }
        var prior = s.Snapshot();
        Assert.Equal(2, prior.RemainingOperations);
        s.OmitOperation();
        Assert.Equal(prior.LastOutcome, s.LastOutcome); // Omission invents no operation failure/result.
        Assert.Equal(prior.OverallDeadline, s.OverallDeadline);
        Assert.True(s.TryStartOperation(WorkerOperation.DisplayActiveTopology, 42000, out _));
        Assert.Equal(52000, s.OperationDeadline); // Full operation allowance; old phantom reservation capped at 47000.
        Assert.Equal(0, s.Snapshot().RemainingOperations);
        Assert.Throws<InvalidOperationException>(() => s.OmitOperation());
        Handshake(s, 42000); s.TryTimeout(52000); s.FinalizeCleanup(true, 53000);
        Assert.Equal(SupervisorPhase.Completed, s.Phase);
    }
    [Theory]
    [InlineData(-1, (int)SupervisorTerminalKind.ResultAccepted)]
    [InlineData(0, (int)SupervisorTerminalKind.Timeout)]
    [InlineData(1, (int)SupervisorTerminalKind.Timeout)]
    public void StrictResultBoundary(long offset, int expected)
    {
        var s = Started(); var deadline = s.OperationDeadline!.Value;
        s.TryAcceptResult(ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem), deadline + offset);
        Assert.True(s.FinalizeCleanup(true, deadline + 2)); Assert.Equal((SupervisorTerminalKind)expected, s.LastOutcome!.Kind);
    }
    [Fact]
    public void SkipAt22SecondsStillEvaluatesAllLaterOperations()
    {
        var s = new CollectionSupervisorStateMachine(Policy, 1000); s.Begin(5, 0);
        for (var i = 0; i < 3; i++)
        {
            Assert.False(s.TryStartOperation((WorkerOperation)i, 22_000, out var skipped));
            Assert.Equal(ReasonCode.Timeout, skipped!.Reason); Assert.Equal(0, skipped.Attempts);
            Assert.Equal(SupervisorPhase.Ready, s.Phase);
        }
        Assert.True(s.TryStartOperation(WorkerOperation.WmiDisplayDrivers, 22_000, out _));
        Assert.Equal(23_500, s.OperationDeadline); // current cleanup + later active/cleanup + bookkeeping
        Handshake(s, 22_000); s.TryTimeout(23_500); Assert.Equal(25_500, s.CleanupDeadline);
        Assert.True(s.FinalizeCleanup(true, 24_000));
        Assert.True(s.TryStartOperation(WorkerOperation.DisplayActiveTopology, 24_000, out _));
        Assert.Equal(27_500, s.OperationDeadline); s.TryTimeout(27_500);
        Assert.Equal(29_500, s.CleanupDeadline);
    }
    [Fact]
    public void AcceptedDataSurvivesCleanupFailureAndCancellation()
    {
        foreach (var failure in new[] { false, true })
        {
            var s = Started(2); Assert.True(s.TryAcceptResult(ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem), 1));
            Assert.True(s.TryCancel(2)); Assert.True(s.FinalizeCleanup(!failure, 3)); Assert.Single(s.CompletedResults);
            Assert.Equal(failure ? SupervisorTerminalKind.HostFailure : SupervisorTerminalKind.Cancelled, s.LastOutcome!.Kind);
            Assert.False(s.TryStartOperation(WorkerOperation.WmiComputerSystem, 4, out _));
        }
    }
    [Fact]
    public void EqualityAtCleanupPoisonsAndNeverRenewsBudget()
    {
        var s = Started(); s.TryTimeout(5000); var deadline = s.CleanupDeadline;
        s.TryCancel(5100); Assert.Equal(deadline, s.CleanupDeadline);
        s.FinalizeCleanup(true, deadline!.Value); Assert.True(s.AdmissionPoisoned);
    }
    [Fact]
    public void SaturatingArithmeticCannotBecomeInfiniteOrNegativeWait()
    {
        Assert.Equal(long.MaxValue, Deadline.Add(long.MaxValue - 1, 9));
        Assert.Equal(long.MinValue, Deadline.Subtract(long.MinValue + 1, 9));
        Assert.Equal(long.MaxValue, Deadline.Ticks(TimeSpan.MaxValue, long.MaxValue));
        Assert.Equal(uint.MaxValue - 1, Deadline.FiniteMilliseconds(TimeSpan.MaxValue));
        Assert.Equal(0u, Deadline.FiniteMilliseconds(TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Deadline.Ticks(TimeSpan.Zero, 0));
        var s = Started(1, long.MaxValue - 100_000); Assert.True(s.OperationDeadline <= long.MaxValue);
    }
    [Theory]
    [InlineData("success")]
    [InlineData("false")]
    [InlineData("throw")]
    [InlineData("dispose-throw")]
    [InlineData("cancel")]
    public async Task CleanupFinalizationAndAdmissionPersistAcrossSupervisors(string behavior)
    {
        using var cancel = new CancellationTokenSource();
        var admission = new HostAdmission(); var session = new ScriptedSession { CleanupBehavior = behavior, Cancellation = cancel };
        var factory = new ScriptedFactory(session);
        var s = new CollectionSupervisor(Policy, sessionFactory: factory, admission: admission); s.Begin(2);
        var result = await s.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, cancel.Token);
        Assert.Single(s.Snapshot.CompletedResults);
        var poisoned = behavior is "false" or "throw" or "dispose-throw";
        Assert.Equal(poisoned ? SupervisorTerminalKind.HostFailure : behavior == "cancel" ? SupervisorTerminalKind.Cancelled : SupervisorTerminalKind.ResultAccepted, result.Kind);
        Assert.Equal(poisoned, admission.IsPoisoned);
        if (behavior == "success")
        {
            var next = new CollectionSupervisor(Policy,
                sessionFactory: new ScriptedFactory(new ScriptedSession()), admission: admission);
            next.Begin(1);
            Assert.Equal(SupervisorTerminalKind.ResultAccepted,
                (await next.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default)).Kind);
        }
        if (poisoned)
        {
            var next = new CollectionSupervisor(Policy, sessionFactory: factory, admission: admission); next.Begin(1);
            Assert.Equal(SupervisorTerminalKind.HostFailure, (await next.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default)).Kind);
            Assert.Equal(1, factory.Calls); Assert.Equal(1, admission.RetainedOwners);
        }
        if (behavior == "cancel")
        {
            Assert.Equal(SupervisorTerminalKind.Cancelled, (await s.RunOperationAsync(WorkerOperation.WmiComputerSystem, null, cancel.Token)).Kind);
            Assert.Equal(1, factory.Calls);
        }
    }
    [Fact]
    public async Task UnresolvedCleanupHasOneDeadlineAndRetainsSessionWithoutDisposal()
    {
        var clock = new DeadlineTests.ManualClock(); var admission = new HostAdmission();
        var completion = new TaskCompletionSource<bool>(); var session = new ScriptedSession { CleanupCompletion = completion.Task };
        var factory = new ScriptedFactory(session); var s = new CollectionSupervisor(Policy, clock, factory, admission); s.Begin(2);
        var task = s.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default).AsTask();
        Assert.False(task.IsCompleted); clock.Advance(2000);
        Assert.Equal(SupervisorTerminalKind.HostFailure, (await task).Kind);
        Assert.Single(s.Snapshot.CompletedResults); Assert.False(session.Disposed); Assert.Equal(1, admission.RetainedOwners);
        var next = new CollectionSupervisor(Policy, clock, factory, admission); next.Begin(1);
        Assert.Equal(SupervisorTerminalKind.HostFailure, (await next.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default)).Kind);
        Assert.Equal(1, factory.Calls); completion.SetResult(true); Assert.True(admission.IsPoisoned);
    }
    [Fact]
    public async Task CancelledBeforeSkippedStartDoesNotLaunchOrPoison()
    {
        var clock = new DeadlineTests.ManualClock(); var admission = new HostAdmission(); var factory = new ScriptedFactory(new ScriptedSession());
        var s = new CollectionSupervisor(Policy, clock, factory, admission); s.Begin(5); clock.Advance(30000);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(SupervisorTerminalKind.Cancelled, (await s.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, cancellation.Token)).Kind);
        Assert.Equal(0, factory.Calls); Assert.False(admission.IsPoisoned);
    }
    [Theory]
    [InlineData("core")]
    [InlineData("windows")]
    [InlineData("protocol")]
    [InlineData("worker")]
    [InlineData("runtime")]
    public async Task WrongReadyIdentityNeverSendsStartAndPoisonsAdmission(string member)
    {
        var wrong = Guid.NewGuid().ToString("D");
        var identity = ProtocolTests.Identity;
        var session = new ScriptedSession { ReadyIdentity = member switch
        {
            "core" => identity with { CoreMvid = wrong }, "windows" => identity with { WindowsMvid = wrong },
            "protocol" => identity with { ProtocolMvid = wrong }, "worker" => identity with { WorkerMvid = wrong },
            _ => identity with { RuntimeVersion = "99.0.0" }
        } };
        var admission = new HostAdmission(); var s = new CollectionSupervisor(Policy, sessionFactory: new ScriptedFactory(session), admission: admission); s.Begin(1);
        Assert.Equal(SupervisorTerminalKind.HostFailure, (await s.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default)).Kind);
        Assert.Single(session.Sent); Assert.IsType<RequestFrame>(session.Sent[0]); Assert.True(admission.IsPoisoned);
    }
    [Fact]
    public async Task InvalidReadyVersionIsFatalDeploymentFailure()
    {
        var session = new ScriptedSession { ReadyIdentity = ProtocolTests.Identity with { ProtocolVersion = 2 }, DecodeFrames = true };
        var admission = new HostAdmission(); var s = new CollectionSupervisor(Policy, sessionFactory: new ScriptedFactory(session), admission: admission); s.Begin(1);
        var outcome = await s.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default);
        Assert.Equal(SupervisorTerminalKind.HostFailure, outcome.Kind); Assert.Equal("Deployment", outcome.Code);
        Assert.True(admission.IsPoisoned); Assert.Single(session.Sent);
    }
    [Theory]
    [InlineData((int)AdmissionFailure.SecurityContext)]
    [InlineData((int)AdmissionFailure.Deployment)]
    [InlineData((int)AdmissionFailure.Containment)]
    [InlineData((int)AdmissionFailure.Launch)]
    public async Task AdmissionFailuresAreFatal(int value)
    {
        var failure = (AdmissionFailure)value; var a = new HostAdmission(); var f = new ScriptedFactory(new ScriptedSession()) { Failure = failure };
        var s = new CollectionSupervisor(Policy, sessionFactory: f, admission: a); s.Begin(1);
        var result = await s.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, default);
        Assert.Equal(SupervisorTerminalKind.HostFailure, result.Kind); Assert.Equal(failure.ToString(), result.Code); Assert.True(a.IsPoisoned);
    }
    [Theory]
    [InlineData(DataState.Failed)]
    [InlineData(DataState.Unsupported)]
    public async Task SupervisorRejectsContradictoryOptionalMonitorNameResult(DataState state)
    {
        var operation = WorkerOperation.DisplayActiveTopology;
        var display = ProtocolTests.Display("gpu-1") with
        {
            Name = Observation<string>.Absent(state, DataSource.DisplayConfig, ReasonCode.MissingValue)
        };
        var result = new ResultFrame(operation, WorkerResultState.Succeeded, ReasonCode.None,
            new(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known([display], DataSource.DisplayConfig), ProtocolTests.Run())));
        var session = new TopologyScriptedSession(result);
        var supervisor = new CollectionSupervisor(Policy, sessionFactory: new ScriptedFactory(session));
        supervisor.Begin(1);
        var outcome = await supervisor.RunOperationAsync(operation, null, default);
        Assert.Equal(SupervisorTerminalKind.ProtocolFailure, outcome.Kind);
        Assert.Equal("protocol", outcome.Code);
        Assert.NotEqual(SupervisorTerminalKind.ResultAccepted, outcome.Kind);
    }
    internal sealed class ScriptedFactory(IWorkerSession session) : IWorkerSessionFactory
    {
        internal int Calls; internal AdmissionFailure? Failure;
        public ValueTask<IWorkerSession> CreateAsync(WorkerOperation operation, WorkerLaunchOptions? options, Deadline connectDeadline,
            Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission, CancellationToken token)
        { Calls++; if (Failure is { } f) throw new WorkerAdmissionException(f); return ValueTask.FromResult<IWorkerSession>(session); }
    }
    internal sealed class ScriptedSession : IWorkerSession
    {
        private int _read;
        internal string CleanupBehavior = "success";
        internal CancellationTokenSource? Cancellation;
        internal WorkerBuildIdentity ReadyIdentity = ProtocolTests.Identity;
        internal List<ProtocolFrame> Sent = [];
        internal Task<bool>? CleanupCompletion;
        internal bool Disposed;
        internal bool DecodeFrames;
        public bool IsInCreationTimeJob => true; public bool IsElevated => false;
        public WorkerBuildIdentity ExpectedIdentity => ProtocolTests.Identity;
        public ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token) { Sent.Add(frame); return ValueTask.CompletedTask; }
        public ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token)
        {
            ProtocolFrame frame = ++_read switch
            { 1 => new ReadyFrame(WorkerOperation.WmiOperatingSystem, ReadyIdentity), 2 => new AttemptStartedFrame(WorkerOperation.WmiOperatingSystem), _ => ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem) };
            return ValueTask.FromResult(DecodeFrames ? ProtocolCodec.Decode(ProtocolCodec.Encode(frame)) : frame);
        }
        public ValueTask<bool> CleanupAsync(Deadline deadline)
        {
            if (CleanupCompletion is not null) return new(CleanupCompletion);
            if (CleanupBehavior == "throw") throw new InvalidOperationException("synthetic");
            if (CleanupBehavior == "cancel") Cancellation!.Cancel();
            return ValueTask.FromResult(CleanupBehavior != "false");
        }
        public void Dispose() { if (CleanupBehavior == "dispose-throw") throw new InvalidOperationException("synthetic"); Disposed = true; }
    }
    private sealed class TopologyScriptedSession(ResultFrame result) : IWorkerSession
    {
        private int _read;
        public bool IsInCreationTimeJob => true;
        public bool IsElevated => false;
        public WorkerBuildIdentity ExpectedIdentity => ProtocolTests.Identity;
        public ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token) =>
            ValueTask.FromResult(++_read switch
            {
                1 => (ProtocolFrame)new ReadyFrame(WorkerOperation.DisplayActiveTopology, ProtocolTests.Identity),
                2 => new AttemptStartedFrame(WorkerOperation.DisplayActiveTopology),
                _ => result
            });
        public ValueTask<bool> CleanupAsync(Deadline deadline) => ValueTask.FromResult(true);
        public void Dispose() { }
    }
}
