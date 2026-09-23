namespace WinGPUDoctor.Supervisor;

// Absolute monotonic timestamps are passed unchanged through every partial I/O step.
internal readonly record struct Deadline(TimeProvider Clock, long Timestamp)
{
    internal static Deadline After(TimeProvider clock, TimeSpan duration) =>
        new(clock, Add(clock.GetTimestamp(), Ticks(duration, clock.TimestampFrequency)));
    internal Deadline Clip(TimeSpan duration) =>
        new(Clock, Math.Min(Timestamp, Add(Clock.GetTimestamp(), Ticks(duration, Clock.TimestampFrequency))));
    internal bool Expired => Clock.GetTimestamp() >= Timestamp;
    internal TimeSpan Remaining => TimeSpan.FromTicks((long)Math.Clamp(
        ((decimal)Timestamp - Clock.GetTimestamp()) * TimeSpan.TicksPerSecond / Clock.TimestampFrequency,
        0, TimeSpan.MaxValue.Ticks));
    internal void Check(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (Expired) throw new TimeoutException("Worker deadline expired.");
    }
    internal static long Ticks(TimeSpan duration, long frequency)
    {
        if (frequency <= 0 || duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var scaled = (UInt128)(ulong)duration.Ticks * (ulong)frequency;
        var ticks = (scaled + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond;
        return ticks > long.MaxValue ? long.MaxValue : (long)ticks;
    }
    internal static long Add(long timestamp, long delta) =>
        (long)Math.Clamp((decimal)timestamp + delta, long.MinValue, long.MaxValue);
    internal static long Subtract(long timestamp, long delta) =>
        (long)Math.Clamp((decimal)timestamp - delta, long.MinValue, long.MaxValue);
    internal static uint FiniteMilliseconds(TimeSpan remaining) => remaining <= TimeSpan.Zero ? 0 :
        (uint)Math.Min(uint.MaxValue - 1m, decimal.Ceiling((decimal)remaining.Ticks / TimeSpan.TicksPerMillisecond));

    // Observe without taking ownership: the caller must retain an unfinished operation.
    internal async ValueTask<bool> ObserveAsync(Task task, CancellationToken token = default)
    {
        if (Expired || token.IsCancellationRequested) return false;
        using var timerCancellation = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timerCancellation.Token);
        var timer = Task.Delay(Remaining, Clock, linked.Token);
        await Task.WhenAny(task, timer).ConfigureAwait(false);
        timerCancellation.Cancel();
        return task.IsCompleted && !Expired && !token.IsCancellationRequested;
    }
}

// A single async operation and its cancellation callback remain owned until BOTH finish.
// No Task.Run, blocking wait, or continuation frees pending native state.
internal sealed class PendingIo : IDisposable
{
    private CancellationTokenSource? _cancellation;
    private Task? _operation;
    private Task _cancellationCompletion = Task.CompletedTask;
    private Task? _drainCompletion;
    internal bool IsResolved => (_operation?.IsCompleted ?? true) && _cancellationCompletion.IsCompleted && (_drainCompletion?.IsCompleted ?? true);

    internal async ValueTask<T> RunAsync<T>(Func<CancellationToken, Task<T>> start, Deadline deadline,
        CancellationToken token)
    {
        deadline.Check(token);
        if (!IsResolved) throw new InvalidOperationException("Previous I/O remains owned.");
        ReleaseCompleted();
        _cancellation = new CancellationTokenSource();
        var operation = start(_cancellation.Token);
        _operation = operation;
        if (!await deadline.ObserveAsync(operation, token).ConfigureAwait(false))
        {
            RequestCancellation();
            deadline.Check(token);
            throw new TimeoutException("Pipe deadline expired.");
        }
        try { return await operation.ConfigureAwait(false); }
        finally { if (IsResolved) ReleaseCompleted(); }
    }

    internal void RequestCancellation()
    {
        if (_cancellation is { IsCancellationRequested: false })
            _cancellationCompletion = _cancellation.CancelAsync();
    }

    internal async ValueTask<bool> DrainAsync(Deadline deadline)
    {
        RequestCancellation();
        var completion = _drainCompletion ??= Task.WhenAll(_operation ?? Task.CompletedTask, _cancellationCompletion);
        if (!IsResolved && !await deadline.ObserveAsync(completion).ConfigureAwait(false)) return false;
        if (!IsResolved || deadline.Expired) return false;
        // Observe faults without treating cancellation/error as successful provider data.
        _ = completion.Exception;
        ReleaseCompleted();
        return true;
    }

    private void ReleaseCompleted()
    {
        if (!IsResolved) throw new InvalidOperationException("Pending I/O cannot be released.");
        _ = _operation?.Exception;
        _ = _cancellationCompletion.Exception;
        _ = _drainCompletion?.Exception;
        _drainCompletion = null;
        _operation = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _cancellationCompletion = Task.CompletedTask;
    }
    public void Dispose() => ReleaseCompleted();
}
