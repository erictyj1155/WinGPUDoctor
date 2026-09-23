using WinGPUDoctor.Cli;
using WinGPUDoctor.Core;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class CancellationHandoffTests
{
    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly CollectionSnapshot Snapshot = new(null!, []);

    [Fact]
    public async Task CancellationWinnerVetoesOutputEvenWhileTokenCallbackIsStillExecuting()
    {
        using var controller = new HostCancellationController();
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
            var outputCalls = 0;
            var removed = false;
            var notices = new List<string>();
            var exit = await CollectionOutput.RunAsync(_ => Task.FromResult(Snapshot), controller,
                () => removed = true, _ => { outputCalls++; return 0; }, notices.Add);
            Assert.Equal(3, exit);
            Assert.True(removed);
            Assert.Equal(0, outputCalls); // Preparation, preview and export are all behind this boundary.
            Assert.Single(notices);
            Assert.Contains("cancelled", notices[0]);
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
        using var controller = new HostCancellationController();
        var collectionComplete = Gate();
        var callbackDispatched = Gate();
        var enterCallback = Gate();
        var removed = false;
        var outputCalls = 0;
        // Capture the delegate before removal, just as the console event dispatcher can.
        Func<HostInterruptResult> captured = controller.Interrupt;
        var callback = Task.Run(async () =>
        {
            callbackDispatched.SetResult(true);
            await enterCallback.Task;
            return captured();
        });
        var cli = CollectionOutput.RunAsync(async _ =>
        {
            await collectionComplete.Task;
            return Snapshot;
        }, controller, () => removed = true, _ => { outputCalls++; return 0; }, _ => { });
        await callbackDispatched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (callbackWins)
        {
            enterCallback.SetResult(true);
            Assert.Equal(HostInterruptResult.Controlled, await callback.WaitAsync(TimeSpan.FromSeconds(10)));
            collectionComplete.SetResult(true);
            Assert.Equal(3, await cli.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(0, outputCalls);
        }
        else
        {
            collectionComplete.SetResult(true);
            Assert.Equal(0, await cli.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(removed);
            enterCallback.SetResult(true);
            Assert.Equal(HostInterruptResult.Forced, await callback.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.False(controller.IsCancellationRequested);
            Assert.False(controller.Token.IsCancellationRequested);
            Assert.Equal(1, outputCalls);
        }
        Assert.True(removed);
    }

    [Fact]
    public async Task CallbackAtRemovalAfterCommitCannotCreateHiddenCancellation()
    {
        using var controller = new HostCancellationController();
        var outputCalls = 0;
        var exit = await CollectionOutput.RunAsync(_ => Task.FromResult(Snapshot), controller,
            () => Assert.Equal(HostInterruptResult.Forced, controller.Interrupt()),
            _ =>
            {
                Assert.False(controller.IsCancellationRequested);
                Assert.Equal(HostInterruptResult.Forced, controller.Interrupt());
                Assert.False(controller.IsCancellationRequested);
                outputCalls++;
                return 0;
            }, _ => Assert.Fail("Unexpected cancellation notice."));
        Assert.Equal(0, exit);
        Assert.Equal(1, outputCalls);
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
        using var controller = new HostCancellationController();
        var outputCalls = 0;
        var exit = await CollectionOutput.RunAsync(_ => throw new IOException("private exception"), controller,
            () => { }, _ => { outputCalls++; return 0; }, text => Assert.DoesNotContain("private", text));
        Assert.Equal(3, exit);
        Assert.Equal(0, outputCalls);
        Assert.Equal(HostInterruptResult.Forced, controller.Interrupt());
        Assert.False(controller.IsCancellationRequested);
    }
}
