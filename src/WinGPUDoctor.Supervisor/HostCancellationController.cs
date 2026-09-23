namespace WinGPUDoctor.Supervisor;

public enum HostInterruptResult { Controlled, Forced }

// Interrupt and output commitment compete for the same atomic state. Removing an
// event subscription alone cannot fence a callback already dispatched by the runtime.
public sealed class HostCancellationController : IDisposable
{
    private const int Active = 0, Cancelled = 1, OutputCommitted = 2, Closed = 3;
    private readonly CancellationTokenSource _cancellation = new();
    private int _state;
    private int _callbacks;
    private int _disposeRequested;
    private int _sourceDisposed;

    public CancellationToken Token => _cancellation.Token;
    public bool IsCancellationRequested => Volatile.Read(ref _state) == Cancelled;

    public bool TryCommitOutput() => Interlocked.CompareExchange(ref _state, OutputCommitted, Active) == Active;

    // Failure/disposal closes collection without changing a cancellation/output winner.
    public void CloseCollection() => Interlocked.CompareExchange(ref _state, Closed, Active);

    public HostInterruptResult Interrupt()
    {
        Interlocked.Increment(ref _callbacks);
        try
        {
            if (Interlocked.CompareExchange(ref _state, Cancelled, Active) != Active)
                return HostInterruptResult.Forced;
            // Publish the winner before signalling, outside any lock. Reentrant/second
            // callbacks see Cancelled and permit default termination. No cleanup or I/O.
            try { _cancellation.Cancel(); }
            catch (AggregateException) { }
            return HostInterruptResult.Controlled;
        }
        finally
        {
            if (Interlocked.Decrement(ref _callbacks) == 0) DisposeSourceIfRequested();
        }
    }

    public void Dispose()
    {
        CloseCollection();
        Volatile.Write(ref _disposeRequested, 1);
        if (Volatile.Read(ref _callbacks) == 0) DisposeSourceIfRequested();
    }

    private void DisposeSourceIfRequested()
    {
        if (Volatile.Read(ref _disposeRequested) != 0 && Interlocked.Exchange(ref _sourceDisposed, 1) == 0)
            _cancellation.Dispose();
    }
}
