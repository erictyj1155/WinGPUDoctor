using WinGPUDoctor.Core;
using WinGPUDoctor.Host;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class CancellationHandoffTests
{
    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly CollectionSnapshot Snapshot = ModelAndPrivacyTests.Sample();
    private static readonly CollectionSnapshot InvalidSnapshot = new(null!, []);

    [Fact]
    public async Task CancellationWinnerVetoesOutputEvenWhileTokenCallbackIsStillExecuting()
    {
        using var scan = new HostScanSession();
        var controller = scan.Cancellation;
        var entered = Gate();
        using var release = new ManualResetEventSlim();
        using var registration = controller.Token.Register(() =>
        {
            entered.SetResult(true);
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
        });
        var callback = Task.Run(controller.Interrupt);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var removed = false;
            var outcome = await scan.RunAsync(_ => Task.FromResult(InvalidSnapshot), () => removed = true);
            Assert.Equal(HostScanOutcomeKind.Cancelled, outcome.Kind);
            Assert.True(removed);
            Assert.Null(outcome.Report); // The invalid snapshot cannot reach privacy projection.
            Assert.True(controller.IsCancellationRequested);
            Assert.False(controller.TryCommitOutput());
            controller.Dispose(); // Must not wait for or dispose the source underneath the callback.
        }
        finally { release.Set(); }
        Assert.Equal(HostInterruptResult.Controlled, await callback.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CapturedCallbackCompetesWithHandoffAtDeterministicGates(bool callbackWins)
    {
        using var scan = new HostScanSession();
        var controller = scan.Cancellation;
        var collectionComplete = Gate();
        var callbackDispatched = Gate();
        var enterCallback = Gate();
        var removed = false;
        // Capture the delegate before removal, just as the console event dispatcher can.
        Func<HostInterruptResult> captured = scan.RequestCancellation;
        var callback = Task.Run(async () =>
        {
            callbackDispatched.SetResult(true);
            await enterCallback.Task;
            return captured();
        });
        var host = scan.RunAsync(async _ =>
        {
            await collectionComplete.Task;
            return Snapshot;
        }, () => removed = true);
        await callbackDispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (callbackWins)
        {
            enterCallback.SetResult(true);
            Assert.Equal(HostInterruptResult.Controlled, await callback.WaitAsync(TimeSpan.FromSeconds(10)));
            collectionComplete.SetResult(true);
            var outcome = await host.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HostScanOutcomeKind.Cancelled, outcome.Kind);
            Assert.Null(outcome.Report);
        }
        else
        {
            collectionComplete.SetResult(true);
            var outcome = await host.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HostScanOutcomeKind.Completed, outcome.Kind);
            Assert.NotNull(outcome.Report);
            Assert.True(removed);
            enterCallback.SetResult(true);
            Assert.Equal(HostInterruptResult.Forced, await callback.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(controller.IsCancellationRequested);
            Assert.False(controller.Token.IsCancellationRequested);
        }
        Assert.True(removed);
    }

    [Fact]
    public async Task CallbackAtRemovalAfterCommitCannotCreateHiddenCancellation()
    {
        using var scan = new HostScanSession();
        var controller = scan.Cancellation;
        var outcome = await scan.RunAsync(_ => Task.FromResult(Snapshot),
            () => Assert.Equal(HostInterruptResult.Forced, scan.RequestCancellation()));
        Assert.Equal(HostScanOutcomeKind.Completed, outcome.Kind);
        Assert.NotNull(outcome.Report);
        Assert.False(controller.IsCancellationRequested);
        Assert.Equal(HostInterruptResult.Forced, scan.RequestCancellation());
        Assert.False(controller.IsCancellationRequested);
    }

    [Fact]
    public void ReentrantAndSecondInterruptPermitForcedTerminationWithoutChangingWinner()
    {
        using var controller = new HostCancellationController();
        HostInterruptResult? reentrantResult = null;
        bool? reentrantOutput = null;
        using var registration = controller.Token.Register(() =>
        {
            reentrantResult = controller.Interrupt();
            reentrantOutput = controller.TryCommitOutput();
        });
        Assert.Equal(HostInterruptResult.Controlled, controller.Interrupt());
        // Assert outside the token callback: the controller contains callback exceptions.
        Assert.Equal(HostInterruptResult.Forced, reentrantResult);
        Assert.False(reentrantOutput);
        Assert.Equal(HostInterruptResult.Forced, controller.Interrupt());
        Assert.True(controller.IsCancellationRequested);
    }

    [Fact]
    public async Task FailedCollectionClosesLifecycleWithoutOutput()
    {
        using var scan = new HostScanSession();
        var controller = scan.Cancellation;
        var outcome = await scan.RunAsync(_ => throw new IOException("private exception"));
        Assert.Equal(HostScanOutcomeKind.CollectionFailed, outcome.Kind);
        Assert.Null(outcome.Report);
        Assert.Equal(HostInterruptResult.Forced, scan.RequestCancellation());
        Assert.False(controller.IsCancellationRequested);
    }
}
