using WinGPUDoctor.Core;
using WinGPUDoctor.Supervisor;

namespace WinGPUDoctor.Host;

// One scan owns one cancellation controller. Report preparation follows the same
// cancel-versus-output commitment that guarded the original CLI output delegate.
public sealed class HostScanSession : IDisposable
{
    private const int Idle = 0, Collecting = 1, CollectionEnded = 2, DisposePending = 3, Disposed = 4;
    private readonly HostCancellationController _cancellation = new();
    private int _state;

    // Test-only access; hosts use RequestCancellation and never commit output themselves.
    internal HostCancellationController Cancellation => _cancellation;

    // Controlled only when this request wins before output commitment; otherwise the
    // caller permits default handling. It never changes a decided winner.
    public HostInterruptResult RequestCancellation() => _cancellation.Interrupt();

    public async Task<HostScanOutcome> RunAsync(Func<CancellationToken, Task<CollectionSnapshot>> collect,
        Action? collectionEnded = null)
    {
        ArgumentNullException.ThrowIfNull(collect);
        var previous = Interlocked.CompareExchange(ref _state, Collecting, Idle);
        if (previous != Idle)
        {
            ObjectDisposedException.ThrowIf(previous is DisposePending or Disposed, this);
            throw new InvalidOperationException("A scan session can run only once.");
        }

        CollectionSnapshot snapshot;
        bool committed;
        try
        {
            snapshot = await collect(_cancellation.Token).ConfigureAwait(false);
            committed = _cancellation.TryCommitOutput();
        }
        catch (SupervisedCollectionException ex) when (ex.Code == "host-cancelled")
        {
            return HostScanOutcome.Cancelled();
        }
        catch (Exception)
        {
            return HostScanOutcome.CollectionFailed();
        }
        finally
        {
            _cancellation.CloseCollection();
            try { collectionEnded?.Invoke(); }
            finally { EndCollection(); }
        }

        if (!committed) return HostScanOutcome.Cancelled();
        var report = PrivacyPolicy.Prepare(snapshot, DateOnly.FromDateTime(DateTime.UtcNow));
        return HostScanOutcome.Completed(report, snapshot.Collection.Any(c => c.IsIncomplete()));
    }

    // Disposal during collection requests controlled cancellation first and leaves the
    // token source alive until collection ends; it does not wait for the scan.
    public void Dispose()
    {
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state is DisposePending or Disposed) return;
            var next = state == Collecting ? DisposePending : Disposed;
            if (Interlocked.CompareExchange(ref _state, next, state) != state) continue;
            if (next == DisposePending) _cancellation.Interrupt();
            else _cancellation.Dispose();
            return;
        }
    }

    private void EndCollection()
    {
        if (Interlocked.CompareExchange(ref _state, CollectionEnded, Collecting) == Collecting) return;
        if (Interlocked.CompareExchange(ref _state, Disposed, DisposePending) == DisposePending)
            _cancellation.Dispose();
    }
}
