using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class CancellationTests
{
    private static readonly CollectionTimingPolicy Policy = new(
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

    [Fact]
    public void FirstInterruptIsControlledAndSecondIsForced()
    {
        using var controller = new HostCancellationController();
        Assert.False(controller.IsCancellationRequested);
        Assert.Equal(HostInterruptResult.Controlled, controller.Interrupt());
        Assert.True(controller.IsCancellationRequested);
        Assert.Equal(HostInterruptResult.Forced, controller.Interrupt());
    }

    [Fact]
    public async Task CancellationDuringHandshakeIsControlledAndDoesNotFabricateProviderTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new CancellableSession(cancellation.Token);
        var supervisor = new CollectionSupervisor(Policy, sessionFactory: new Factory(session), admission: new HostAdmission());
        supervisor.Begin(2);
        var task = supervisor.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, cancellation.Token).AsTask();
        await Task.Delay(50);
        cancellation.Cancel();
        var outcome = await task;
        Assert.Equal(SupervisorTerminalKind.Cancelled, outcome.Kind);
        Assert.Equal("host-cancelled", outcome.Code);
        Assert.Equal(ReasonCode.QueryFailed, outcome.Reason);
        Assert.NotEqual(ReasonCode.Timeout, outcome.Reason);
        Assert.Empty(supervisor.Snapshot.CompletedResults);
    }

    [Fact]
    public async Task CancellationAfterAcceptedOperationPreventsLaterStart()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new CancellableSession(null);
        var supervisor = new CollectionSupervisor(Policy, sessionFactory: new Factory(session), admission: new HostAdmission());
        supervisor.Begin(2);
        var first = await supervisor.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, cancellation.Token);
        Assert.Equal(SupervisorTerminalKind.ResultAccepted, first.Kind);
        cancellation.Cancel();
        var second = await supervisor.RunOperationAsync(WorkerOperation.WmiComputerSystem, null, cancellation.Token);
        Assert.Equal(SupervisorTerminalKind.Cancelled, second.Kind);
        Assert.Equal("host-cancelled", second.Code);
    }

    [Fact]
    public async Task CancellationAfterAttemptMarkerClosesResultAcceptance()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new AfterAttemptSession();
        var progress = new List<CollectionProgress>();
        var supervisor = new CollectionSupervisor(Policy, sessionFactory: new Factory(session), admission: new HostAdmission(), progress: progress.Add);
        supervisor.Begin(2);
        var task = supervisor.RunOperationAsync(WorkerOperation.WmiOperatingSystem, null, cancellation.Token).AsTask();
        await session.AttemptObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        session.ReleaseResult.SetResult(true);
        var outcome = await task;
        Assert.Equal(SupervisorTerminalKind.Cancelled, outcome.Kind);
        Assert.Equal("host-cancelled", outcome.Code);
        Assert.Equal(ReasonCode.QueryFailed, outcome.Reason);
        Assert.NotEqual(ReasonCode.Timeout, outcome.Reason);
        Assert.Empty(supervisor.Snapshot.CompletedResults);
        Assert.False(supervisor.Snapshot.AdmissionPoisoned);
        Assert.Equal(new[] { CollectionProgressKind.WorkerStarted, CollectionProgressKind.AttemptValidated,
            CollectionProgressKind.CancellationObserved, CollectionProgressKind.CleanupConfirmed }, progress.Select(item => item.Kind));
        Assert.Equal(1, progress[1].Attempt);
    }

    private sealed class Factory(IWorkerSession session) : IWorkerSessionFactory
    {
        internal int Calls;
        public ValueTask<IWorkerSession> CreateAsync(WorkerOperation operation, WorkerLaunchOptions? options,
            Deadline connectDeadline, Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission,
            CancellationToken token)
        {
            Calls++;
            return ValueTask.FromResult<IWorkerSession>(session);
        }
    }

    // Emits the attempt marker, then waits for the host to cancel before offering a valid result.
    private sealed class AfterAttemptSession : IWorkerSession
    {
        private int _read;
        internal TaskCompletionSource<bool> AttemptObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> ReleaseResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsInCreationTimeJob => true;
        public bool IsElevated => false;
        public WorkerBuildIdentity ExpectedIdentity => ProtocolTests.Identity;
        public ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token) =>
            ValueTask.CompletedTask;
        public async ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token)
        {
            switch (++_read)
            {
                case 1: return new ReadyFrame(WorkerOperation.WmiOperatingSystem, ProtocolTests.Identity);
                case 2: AttemptObserved.SetResult(true); return new AttemptStartedFrame(WorkerOperation.WmiOperatingSystem);
                default:
                    await ReleaseResult.Task.ConfigureAwait(false);
                    return ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem);
            }
        }
        public ValueTask<bool> CleanupAsync(Deadline deadline) => ValueTask.FromResult(true);
        public void Dispose() { }
    }

    private sealed class CancellableSession(CancellationToken? waitOnRead) : IWorkerSession
    {
        private int _read;
        public bool IsInCreationTimeJob => true;
        public bool IsElevated => false;
        public WorkerBuildIdentity ExpectedIdentity => ProtocolTests.Identity;
        public ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token) =>
            ValueTask.CompletedTask;
        public async ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token)
        {
            if (_read == 0 && waitOnRead is { CanBeCanceled: true } wait)
                await Task.Delay(Timeout.Infinite, wait);
            return ++_read switch
            {
                1 => new ReadyFrame(WorkerOperation.WmiOperatingSystem, ProtocolTests.Identity),
                2 => new AttemptStartedFrame(WorkerOperation.WmiOperatingSystem),
                _ => ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem)
            };
        }
        public ValueTask<bool> CleanupAsync(Deadline deadline) => ValueTask.FromResult(true);
        public void Dispose() { }
    }
}
