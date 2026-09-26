using System.Reflection;
using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Desktop;
using WinGPUDoctor.Desktop.ViewModels;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class DesktopTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static TaskCompletionSource<bool> Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Waits for a state set by a continuation on another thread; fails instead of hanging.
    private static async Task WaitUntil(Func<bool> condition)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed > Wait) throw new TimeoutException("Condition was not reached.");
            await Task.Delay(5);
        }
    }

    public static TheoryData<string> Fixtures => new() { "single", "multiple", "empty", "topology", "unmatched", "redacted", "incomplete" };

    internal static CollectionSnapshot Fixture(string name) => name switch
    {
        "single" => ModelAndPrivacyTests.Sample(),
        "multiple" => ModelAndPrivacyTests.Sample(count: 2),
        "empty" => ModelAndPrivacyTests.Sample(count: 0),
        "topology" => TopologyTests.WithTopology(TopologyTests.Collect(TopologyTests.Single(true))),
        "unmatched" => TopologyTests.WithTopology(TopologyTests.Collect(TopologyTests.Single(true), [])),
        "redacted" => ModelAndPrivacyTests.Sample("token=private"),
        "incomplete" => ModelAndPrivacyTests.Sample() with
        {
            Collection = [new CollectorRun(DataSource.WmiVideoController, CollectorStatus.Failed, ReasonCode.QueryFailed)]
        },
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static TaskCompletionSource<CancellationToken> Collecting() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Like the supervised collector, a cancelled token ends collection as host-cancelled; an optional
    // gate holds the controlled stop so intermediate states can be observed without racing it.
    private static Func<CancellationToken, Task<CollectionSnapshot>> WaitForCancellation(TaskCompletionSource<CancellationToken> collecting,
        Task? stop = null) =>
        async token =>
        {
            var signal = Gate();
            using var registration = token.Register(() => signal.TrySetResult(true));
            collecting.SetResult(token);
            await signal.Task.WaitAsync(Wait);
            if (stop is not null) await stop.WaitAsync(Wait);
            throw new SupervisedCollectionException("host-cancelled");
        };

    // Blocks report preparation, which runs only after output commitment has won; optionally fails it.
    private sealed class GatedRuns(IReadOnlyList<CollectorRun> runs, TaskCompletionSource<bool> preparing, Task release,
        bool failAfterRelease = false) : IReadOnlyList<CollectorRun>
    {
        public CollectorRun this[int index] => runs[index];
        public int Count => runs.Count;
        public IEnumerator<CollectorRun> GetEnumerator()
        {
            preparing.TrySetResult(true);
            Assert.True(release.Wait(Wait));
            if (failAfterRelease) throw new InvalidOperationException("Synthetic failure after output commitment.");
            return runs.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static async Task<ResultViewModel> ScanResult(string fixture)
    {
        var model = new MainViewModel(_ => Task.FromResult(Fixture(fixture)));
        await model.ScanAsync();
        Assert.Equal(ScanState.Result, model.State);
        return Assert.IsType<ResultViewModel>(model.Result);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void DisplayModelRoundTripsTheExactJsonWriterOutput(string fixture)
    {
        var shareable = PrivacyPolicy.Prepare(Fixture(fixture), new(2026, 9, 26));
        var document = ReportDocument.From(shareable);
        Assert.Same(shareable, document.Shareable);
        Assert.Equal(ReportWriter.Json(shareable), JsonSerializer.Serialize(document.Report, ReportWriter.JsonOptions) + "\n");
    }

    [Fact]
    public void DesktopTypesNeverHoldARawCollectionSnapshot()
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var held = typeof(MainViewModel).Assembly.GetTypes().SelectMany(t =>
            t.GetFields(all).Select(f => f.FieldType).Concat(t.GetProperties(all).Select(p => p.PropertyType)));
        Assert.DoesNotContain(typeof(CollectionSnapshot), held);
    }

    [Fact]
    public async Task CompletedScanShowsSummaryCardsFromTheRetainedReport()
    {
        var model = new MainViewModel(_ => Task.FromResult(Fixture("topology")));
        Assert.Equal(ScanState.Welcome, model.State);
        Assert.True(model.ScanCommand.CanExecute(null));
        Assert.False(model.CancelCommand.CanExecute(null));

        await model.ScanAsync();

        Assert.Equal(ScanState.Result, model.State);
        Assert.True(model.ScanCommand.CanExecute(null));
        var result = Assert.IsType<ResultViewModel>(model.Result);
        Assert.False(result.IsIncomplete);
        Assert.Equal(UiText.Get("Summary.Complete"), result.Headline);
        Assert.Equal(new[] { "2", "1" }, result.Summary.Select(l => l.Value));
        // This PC, two adapters, one display path, then findings, report notes, collection and limits.
        Assert.Equal(8, result.Cards.Count);
        Assert.Equal("Example GPU", result.Cards[1].Title);
        Assert.Contains(result.Cards[1].Facts, f => f.Label == UiText.Get("Field.PciVendorId") && f.Value == "10DE");
        var display = result.Cards[3].Facts.ToDictionary(f => f.Label, f => f.Value);
        Assert.Equal("2560 × 1600 pixels", display[UiText.Get("Field.Resolution")]);
        Assert.Equal("165 Hz", display[UiText.Get("Field.RefreshRate")]);
        Assert.Equal("DisplayPort (embedded)", display[UiText.Get("Field.OutputTechnology")]);
        Assert.Equal("gpu-2 (Example GPU)", display[UiText.Get("Field.SourceAdapter")]);
        Assert.Equal("gpu-2 (Example GPU)", display[UiText.Get("Field.TargetAdapter")]);
        Assert.Equal(UiText.Get("Card.Limits.Title"), result.Cards[7].Title);
    }

    [Fact]
    public async Task UnavailableValuesUseFriendlyStateTextWithAnIcon()
    {
        var redacted = await ScanResult("redacted");
        var name = redacted.Cards[1].Facts[0];
        Assert.Equal(UiText.Get("Card.Adapter.Title"), redacted.Cards[1].Title);
        Assert.False(name.IsAvailable);
        Assert.Equal(UiText.Get("State.Redacted"), name.Value);
        Assert.NotEmpty(name.StateGlyph);
        Assert.Contains(redacted.Summary, l => l.Label == UiText.Get("Summary.Redacted") && l.Value == "1");

        var unmatched = await ScanResult("unmatched");
        var association = unmatched.Cards[3].Facts.Single(f => f.Label == UiText.Get("Field.SourceAdapter"));
        Assert.False(association.IsAvailable);
        Assert.Equal(UiText.Get("Display.Association.Unresolved"), association.Value);

        var incomplete = await ScanResult("incomplete");
        Assert.True(incomplete.IsIncomplete);
        Assert.Equal(UiText.Get("Summary.Incomplete"), incomplete.Headline);
    }

    [Fact]
    public async Task CancelIsSingleUseAndStopsWithoutAReport()
    {
        var collecting = Collecting();
        var stop = Gate();
        var model = new MainViewModel(WaitForCancellation(collecting, stop.Task));
        var scan = model.ScanAsync();
        var token = await collecting.Task.WaitAsync(Wait);
        Assert.Equal(ScanState.Scanning, model.State);
        Assert.False(model.ScanCommand.CanExecute(null));
        Assert.True(model.CancelCommand.CanExecute(null));

        var request = model.CancelAsync();
        Assert.False(model.CancelCommand.CanExecute(null)); // Disabled at once.
        await request.WaitAsync(Wait);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(ScanState.Cancelling, model.State); // Controlled.
        Assert.Equal(UiText.Get("Scan.Stopping"), model.BusyText);
        await model.CancelAsync(); // The second cancel is a no-op.
        Assert.Equal(ScanState.Cancelling, model.State);

        stop.SetResult(true);
        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.Stopped, model.State);
        Assert.Null(model.Result);
        Assert.True(model.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task CollectionFailureNeedsRestartAndIsNeverRetried()
    {
        var calls = 0;
        var model = new MainViewModel(_ =>
        {
            calls++;
            throw new SupervisedCollectionException("cleanup-unconfirmed");
        });
        await model.ScanAsync();
        Assert.Equal(ScanState.NeedsRestart, model.State);
        Assert.Null(model.Result);
        Assert.False(model.ScanCommand.CanExecute(null));
        await model.ScanAsync();
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReportPreparationFailureIsHandledAtTheOutermostLevel()
    {
        var model = new MainViewModel(_ => Task.FromResult(new CollectionSnapshot(null!, [])));
        await model.ScanAsync();
        Assert.Equal(ScanState.NeedsRestart, model.State);
        Assert.Null(model.Result);
    }

    [Fact]
    public async Task EachScanUsesAFreshSessionAndDiscardsThePreviousReport()
    {
        var second = Gate();
        var scans = 0;
        var model = new MainViewModel(async _ =>
        {
            if (Interlocked.Increment(ref scans) == 2) await second.Task.WaitAsync(Wait);
            return Fixture("single");
        });
        await model.ScanAsync();
        var first = Assert.IsType<ResultViewModel>(model.Result);

        var running = model.ScanAsync();
        Assert.Equal(ScanState.Scanning, model.State);
        Assert.Null(model.Result);
        await model.ScanAsync(); // Ignored while a scan runs.
        second.SetResult(true);
        await running.WaitAsync(Wait);

        // A reused session would throw and need a restart.
        Assert.Equal(ScanState.Result, model.State);
        Assert.NotSame(first, model.Result);
        Assert.Equal(2, Volatile.Read(ref scans));
    }

    [Fact]
    public async Task CancelAfterOutputCommitmentKeepsTheResult()
    {
        var preparing = Gate();
        var release = Gate();
        var single = Fixture("single");
        var model = new MainViewModel(_ => Task.FromResult(single with { Collection = new GatedRuns(single.Collection, preparing, release.Task) }));
        var scan = model.ScanAsync();
        await preparing.Task.WaitAsync(Wait); // Output commitment has won; the report is being prepared.

        await model.CancelAsync().WaitAsync(Wait);
        Assert.Equal(ScanState.Scanning, model.State); // Forced: not shown as stopping.
        Assert.Equal(UiText.Get("Scan.Finishing"), model.BusyText);
        Assert.False(model.CancelCommand.CanExecute(null)); // Still single-use.
        release.SetResult(true);
        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.Result, model.State);
        Assert.NotNull(model.Result);
    }

    [Fact]
    public async Task ForcedCancelThenFailureShowsNeutralTextAndNeedsRestart()
    {
        var preparing = Gate();
        var release = Gate();
        var single = Fixture("single");
        var model = new MainViewModel(_ => Task.FromResult(single with
        {
            Collection = new GatedRuns(single.Collection, preparing, release.Task, failAfterRelease: true)
        }));
        var scan = model.ScanAsync();
        await preparing.Task.WaitAsync(Wait); // Forced from here on; no report exists yet.

        await model.CancelAsync().WaitAsync(Wait);
        Assert.Equal(ScanState.Scanning, model.State);
        Assert.Equal(UiText.Get("Scan.Finishing"), model.BusyText); // Neutral: promises no result.
        Assert.DoesNotContain("result", model.BusyText, StringComparison.OrdinalIgnoreCase);
        release.SetResult(true);
        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.NeedsRestart, model.State);
        Assert.Null(model.Result);
    }

    [Fact]
    public async Task ClosingDuringAScanRequestsCancellationAndClosesOnceStopped()
    {
        Assert.True(new MainViewModel(_ => Task.FromResult(Fixture("single"))).RequestClose());
        var clock = new DeadlineTests.ManualClock();
        var collecting = Collecting();
        var stop = Gate();
        var model = new MainViewModel(WaitForCancellation(collecting, stop.Task), clock);
        var closeReady = 0;
        model.CloseReady += (_, _) => Interlocked.Increment(ref closeReady);
        var scan = model.ScanAsync();
        var token = await collecting.Task.WaitAsync(Wait);

        Assert.False(model.RequestClose());
        await WaitUntil(() => model.State == ScanState.Cancelling); // Controlled.
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(UiText.Get("Scan.StoppingToClose"), model.BusyText);
        Assert.False(model.RequestClose()); // Still waiting for the controlled stop.
        Assert.Equal(0, Volatile.Read(ref closeReady));

        stop.SetResult(true);
        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.Stopped, model.State);
        Assert.Equal(1, closeReady);
        clock.Advance((long)MainViewModel.DefaultCloseWait.TotalMilliseconds); // The bound no longer applies.
        Assert.Equal(1, closeReady);
        Assert.True(model.RequestClose());
    }

    [Fact]
    public async Task ClosingAllowsExitAfterTheBoundWhenTheScanIgnoresCancellation()
    {
        var clock = new DeadlineTests.ManualClock();
        var collecting = Collecting();
        var never = Gate();
        var model = new MainViewModel(async token =>
        {
            collecting.SetResult(token);
            await never.Task; // A collector that does not respond to cancellation.
            return Fixture("single");
        }, clock);
        var closed = Gate();
        var closeReady = 0;
        model.CloseReady += (_, _) => { Interlocked.Increment(ref closeReady); closed.TrySetResult(true); };
        var scan = model.ScanAsync();
        var token = await collecting.Task.WaitAsync(Wait);

        Assert.False(model.RequestClose());
        await WaitUntil(() => model.State == ScanState.Cancelling);
        Assert.True(token.IsCancellationRequested);
        clock.Advance((long)MainViewModel.DefaultCloseWait.TotalMilliseconds - 1);
        Assert.False(closed.Task.IsCompleted);
        clock.Advance(1);
        await closed.Task.WaitAsync(Wait);
        Assert.Equal(ScanState.Cancelling, model.State); // The window may close; the Job is the backstop.

        never.SetResult(true);
        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.Stopped, model.State); // Cancellation won, so the late snapshot is discarded.
        Assert.Equal(1, Volatile.Read(ref closeReady));
    }

    [Fact]
    public async Task ABlockingCancellationCallbackNeverHoldsTheCallerAndTheCloseStillFires()
    {
        var clock = new DeadlineTests.ManualClock();
        var collecting = Collecting();
        var callbackEntered = Gate();
        using var unblock = new ManualResetEventSlim();
        var stop = Gate();
        var model = new MainViewModel(async token =>
        {
            // Token callbacks run inside CancellationTokenSource.Cancel(); this one blocks that thread.
            using var registration = token.Register(() => { callbackEntered.TrySetResult(true); unblock.Wait(3 * Wait); });
            collecting.SetResult(token);
            await stop.Task.WaitAsync(Wait);
            throw new SupervisedCollectionException("host-cancelled");
        }, clock);
        var closed = Gate();
        var closeReady = 0;
        model.CloseReady += (_, _) => { Interlocked.Increment(ref closeReady); closed.TrySetResult(true); };
        var scan = model.ScanAsync();
        await collecting.Task.WaitAsync(Wait);

        // RequestClose returns while the request is still stuck in the callback (a synchronous
        // request would hang here and fail the wait).
        Assert.False(await Task.Run(model.RequestClose).WaitAsync(Wait));
        await callbackEntered.Task.WaitAsync(Wait);
        Assert.Equal(ScanState.Scanning, model.State);
        Assert.Equal(UiText.Get("Scan.RequestingStop"), model.BusyText);
        Assert.False(model.CancelCommand.CanExecute(null));

        // The deadline started before the request, so it fires although the request never returned.
        clock.Advance((long)MainViewModel.DefaultCloseWait.TotalMilliseconds);
        await closed.Task.WaitAsync(Wait);
        Assert.False(unblock.IsSet);

        // Once the request returns, the Controlled result is still shown as a controlled stop.
        unblock.Set();
        await WaitUntil(() => model.State == ScanState.Cancelling);
        stop.SetResult(true);
        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.Stopped, model.State);
        Assert.Equal(1, Volatile.Read(ref closeReady));
    }

    [Fact]
    public void CloseWaitCoversTheSupervisorsOverallBudget()
    {
        Assert.True(MainViewModel.DefaultCloseWait >=
            CollectionTimingPolicy.CalibratedProduction.OverallBudget + TimeSpan.FromSeconds(10));
    }
}
