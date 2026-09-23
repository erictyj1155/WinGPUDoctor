using WinGPUDoctor.Core;
using WinGPUDoctor.Supervisor;

namespace WinGPUDoctor.Cli;

// The actual CLI collection/output boundary, also compiled into deterministic tests.
// All report preparation, preview and export live inside the guarded output delegate.
internal static class CollectionOutput
{
    internal static async Task<int> RunAsync(Func<CancellationToken, Task<CollectionSnapshot>> collect,
        HostCancellationController cancellation, Action unregister, Func<CollectionSnapshot, int> output,
        Action<string> notice)
    {
        CollectionSnapshot snapshot;
        bool committed;
        try
        {
            snapshot = await collect(cancellation.Token).ConfigureAwait(false);
            committed = cancellation.TryCommitOutput();
        }
        catch (SupervisedCollectionException ex) when (ex.Code == "host-cancelled")
        {
            notice("Collection cancelled. No report was exported.");
            return 3;
        }
        catch (Exception)
        {
            notice("Collection could not complete. No report was exported.");
            return 3;
        }
        finally
        {
            cancellation.CloseCollection();
            unregister();
        }
        if (!committed)
        {
            notice("Collection cancelled. No report was exported.");
            return 3;
        }
        return output(snapshot);
    }
}
