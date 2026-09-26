using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Host;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class HostTests
{
    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Like the supervised collector, a cancelled token ends collection as host-cancelled.
    private static Func<CancellationToken, Task<CollectionSnapshot>> WaitForCancellation(
        TaskCompletionSource<bool> collecting, Action<CancellationToken>? cancelled = null) => async token =>
    {
        var signal = Gate();
        using var registration = token.Register(() => signal.TrySetResult(true));
        collecting.SetResult(true);
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancelled?.Invoke(token);
        throw new SupervisedCollectionException("host-cancelled");
    };

    [Fact]
    public async Task CompletedScanReturnsOnlyPreparedReportAndCompletionFlag()
    {
        var sample = ModelAndPrivacyTests.Sample("token=private") with
        {
            Collection = [new CollectorRun(DataSource.WmiVideoController, CollectorStatus.Failed, ReasonCode.QueryFailed)]
        };
        using var scan = new HostScanSession();
        var outcome = await scan.RunAsync(_ => Task.FromResult(sample));

        Assert.Equal(HostScanOutcomeKind.Completed, outcome.Kind);
        Assert.True(outcome.IsIncomplete);
        var json = ReportWriter.Json(Assert.IsType<ShareableReport>(outcome.Report));
        Assert.DoesNotContain("token=private", json);
        var report = JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;
        Assert.Equal(DataState.Redacted, report.Facts.Gpus.Value![0].Name.State);
        Assert.Contains(WarningCode.CollectionIncomplete, report.Warnings);
    }

    [Fact]
    public async Task HostCancellationAndOtherFailuresHaveDistinctOutcomesWithoutReports()
    {
        using var cancelled = new HostScanSession();
        var stopped = await cancelled.RunAsync(_ => throw new SupervisedCollectionException("host-cancelled"));
        Assert.Equal(HostScanOutcomeKind.Cancelled, stopped.Kind);
        Assert.Null(stopped.Report);

        using var failed = new HostScanSession();
        var error = await failed.RunAsync(_ => throw new SupervisedCollectionException("cleanup-unconfirmed"));
        Assert.Equal(HostScanOutcomeKind.CollectionFailed, error.Kind);
        Assert.Null(error.Report);
    }

    [Fact]
    public async Task EachScanHasFreshTerminalControllerAndSessionRunsOnlyOnce()
    {
        using var first = new HostScanSession();
        using var second = new HostScanSession();
        Assert.NotSame(first.Cancellation, second.Cancellation);
        Assert.Equal(HostScanOutcomeKind.Completed,
            (await first.RunAsync(_ => Task.FromResult(ModelAndPrivacyTests.Sample()))).Kind);
        Assert.Equal(HostInterruptResult.Forced, first.RequestCancellation());
        Assert.Equal(HostScanOutcomeKind.Completed,
            (await second.RunAsync(_ => Task.FromResult(ModelAndPrivacyTests.Sample()))).Kind);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            first.RunAsync(_ => Task.FromResult(ModelAndPrivacyTests.Sample())));
    }

    [Fact]
    public async Task RequestCancellationIsTheOnlyPublicCancellationSurface()
    {
        var exposed = typeof(HostScanSession).GetProperties().Select(p => p.PropertyType)
            .Concat(typeof(HostScanSession).GetMethods().Select(m => m.ReturnType));
        Assert.DoesNotContain(typeof(HostCancellationController), exposed);

        using var scan = new HostScanSession();
        var collecting = Gate();
        var run = scan.RunAsync(WaitForCancellation(collecting));
        await collecting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HostInterruptResult.Controlled, scan.RequestCancellation());
        Assert.Equal(HostInterruptResult.Forced, scan.RequestCancellation());
        Assert.Equal(HostScanOutcomeKind.Cancelled, (await run.WaitAsync(TimeSpan.FromSeconds(10))).Kind);
    }

    [Fact]
    public async Task DisposeDuringCollectionRequestsControlledCancellationBeforeReleasingController()
    {
        var scan = new HostScanSession();
        var controller = scan.Cancellation;
        var collecting = Gate();
        var ended = false;
        bool? sourceAliveAfterDispose = null;
        var run = scan.RunAsync(WaitForCancellation(collecting,
            token => sourceAliveAfterDispose = token.WaitHandle.WaitOne(0)), () => ended = true);
        await collecting.Task.WaitAsync(TimeSpan.FromSeconds(10));

        scan.Dispose(); // For example, a window closing while the scan is still collecting.

        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HostScanOutcomeKind.Cancelled, outcome.Kind);
        Assert.Null(outcome.Report);
        Assert.True(ended);
        Assert.True(sourceAliveAfterDispose); // Collector cleanup can still observe the cancelled token.
        Assert.True(controller.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => controller.Token); // Released once collection ended.
        Assert.Equal(HostInterruptResult.Forced, scan.RequestCancellation());
        scan.Dispose();
    }

    [Fact]
    public async Task DisposeAfterOutputCommitmentKeepsCompletedOutcome()
    {
        var scan = new HostScanSession();
        var controller = scan.Cancellation;
        var outcome = await scan.RunAsync(_ => Task.FromResult(ModelAndPrivacyTests.Sample()), scan.Dispose);
        Assert.Equal(HostScanOutcomeKind.Completed, outcome.Kind);
        Assert.NotNull(outcome.Report);
        Assert.False(controller.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => controller.Token);
    }

    [Fact]
    public async Task DisposedSessionRejectsRunWithoutCollecting()
    {
        var scan = new HostScanSession();
        scan.Dispose();
        var collected = false;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => scan.RunAsync(_ =>
        {
            collected = true;
            return Task.FromResult(ModelAndPrivacyTests.Sample());
        }));
        Assert.False(collected);
        Assert.Equal(HostInterruptResult.Forced, scan.RequestCancellation());
    }

    [Theory]
    [InlineData(@"\\server\share\report.md")]
    [InlineData(@"\\?\C:\report.md")]
    [InlineData(@"C:\report.md:alternate")]
    public void DestinationRejectsNetworkDeviceAndAlternateStreamPaths(string path)
    {
        Assert.NotEqual(ExportDestinationStatus.Accepted, HostExport.ResolveDestination(path).Status);
    }

    [Fact]
    public void DestinationRejectsInvalidPath()
    {
        Assert.Equal(ExportDestinationStatus.InvalidLocalDestination,
            HostExport.ResolveDestination("bad\0path").Status);
    }

    [Fact]
    public void ExportUsesUtf8WithoutBomAndNeverOverwrites()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wgd-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "report.md");
        try
        {
            var destination = HostExport.ResolveDestination(path);
            Assert.Equal(ExportDestinationStatus.Accepted, destination.Status);
            Assert.True(HostExport.TryWriteNew(destination, "first\n"));
            Assert.False(HostExport.TryWriteNew(destination, "second\n"));
            Assert.Equal("first\n", File.ReadAllText(path));
            Assert.Equal("first\n"u8.ToArray(), File.ReadAllBytes(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(folder);
        }
    }
}
