using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using Xunit;
namespace WinGPUDoctor.Tests;
public class DeadlineTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(4, 10)]
    public async Task SlowDripPrefixAndPayloadShareAbsoluteDeadline(int chunk, int budget)
    {
        var clock = new ManualClock(); var data = ProtocolFraming.Encode(new RequestFrame(WorkerOperation.WmiOperatingSystem));
        var offset = 0; var deadline = Deadline.After(clock, TimeSpan.FromMilliseconds(budget));
        await Assert.ThrowsAsync<TimeoutException>(async () => await DeadlineFraming.ReadAsync((buffer, supplied, _) =>
        {
            Assert.Equal(deadline.Timestamp, supplied.Timestamp); clock.Advance(3);
            var count = Math.Min(chunk, Math.Min(buffer.Length, data.Length - offset));
            data.AsMemory(offset, count).CopyTo(buffer); offset += count; return ValueTask.FromResult(count);
        }, deadline, default));
        Assert.Equal(12, clock.GetTimestamp()); Assert.True(offset < data.Length);
    }
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public async Task CompletionIsAcceptedOnlyStrictlyBeforeDeadline(int elapsed, bool accept)
    {
        var clock = new ManualClock(); var bytes = ProtocolFraming.Encode(new RequestFrame(WorkerOperation.WmiOperatingSystem)); var offset = 0;
        var operation = DeadlineFraming.ReadAsync((buffer, _, _) =>
        {
            bytes.AsMemory(offset, buffer.Length).CopyTo(buffer); offset += buffer.Length;
            if (offset == bytes.Length) clock.Advance(elapsed); return ValueTask.FromResult(buffer.Length);
        }, Deadline.After(clock, TimeSpan.FromMilliseconds(10)), default).AsTask();
        if (accept) Assert.IsType<RequestFrame>(await operation); else await Assert.ThrowsAsync<TimeoutException>(() => operation);
    }
    [Fact]
    public async Task DelayedIoCompletionIsRetainedUntilConfirmedWithinCleanupDeadline()
    {
        var clock = new ManualClock(); var pending = new PendingIo();
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        var operation = pending.RunAsync(token => { observed = token; return completion.Task; }, Deadline.After(clock, TimeSpan.FromMilliseconds(10)), default).AsTask();
        clock.Advance(10); await Assert.ThrowsAsync<TimeoutException>(() => operation);
        Assert.True(observed.IsCancellationRequested); Assert.False(pending.IsResolved);
        Assert.Throws<InvalidOperationException>(() => pending.Dispose());
        var cleanup = pending.DrainAsync(Deadline.After(clock, TimeSpan.FromMilliseconds(5))).AsTask();
        clock.Advance(4); Assert.False(cleanup.IsCompleted);
        completion.SetResult(1); Assert.True(await cleanup); pending.Dispose();
    }
    [Fact]
    public async Task UnresolvedIoReturnsAtCleanupDeadlineAndPoisonRetainsOwnership()
    {
        var clock = new ManualClock(); var pending = new PendingIo(); var completion = new TaskCompletionSource<int>();
        var operation = pending.RunAsync(_ => completion.Task, Deadline.After(clock, TimeSpan.FromMilliseconds(10)), default).AsTask();
        clock.Advance(10); await Assert.ThrowsAsync<TimeoutException>(() => operation);
        var cleanup = pending.DrainAsync(Deadline.After(clock, TimeSpan.FromMilliseconds(5))).AsTask();
        clock.Advance(5); Assert.False(await cleanup);
        var admission = new HostAdmission(); admission.Poison(pending);
        Assert.Throws<WorkerAdmissionException>(() => admission.Enter()); Assert.Equal(1, admission.RetainedOwners);
        completion.SetResult(0); // synthetic completion does not clear process poison
        Assert.True(admission.IsPoisoned); Assert.Throws<WorkerAdmissionException>(() => admission.Enter());
    }
    internal sealed class ManualClock : TimeProvider
    {
        private long _now; private readonly List<Timer> _timers = [];
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new Timer(this, callback, state); _timers.Add(timer); timer.Change(dueTime, period); return timer; }
        internal void Advance(long milliseconds)
        { _now += milliseconds; foreach (var timer in _timers.ToArray()) timer.Fire(); }
        private sealed class Timer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due = long.MaxValue;
            public bool Change(TimeSpan dueTime, TimeSpan period) { _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._now + (long)dueTime.TotalMilliseconds; return true; }
            internal void Fire() { if (_due > clock._now) return; _due = long.MaxValue; callback(state); }
            public void Dispose() => _due = long.MaxValue;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
