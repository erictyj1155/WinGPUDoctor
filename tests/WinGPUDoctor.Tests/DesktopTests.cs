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
    public async Task CompletedScanShowsGuidedCardsFromTheRetainedReport()
    {
        var model = new MainViewModel(_ => Task.FromResult(Fixture("topology")));
        Assert.Equal(ScanState.Welcome, model.State);
        Assert.Equal(UiText.Get("Readout.Idle"), model.ReadoutStatus);
        Assert.True(model.ScanCommand.CanExecute(null));
        Assert.False(model.CancelCommand.CanExecute(null));

        await model.ScanAsync();

        Assert.Equal(ScanState.Result, model.State);
        Assert.True(model.ScanCommand.CanExecute(null));
        var result = Assert.IsType<ResultViewModel>(model.Result);
        Assert.False(result.IsIncomplete);
        Assert.Equal(UiText.Get("Result.Title"), result.Headline);
        Assert.Equal("Windows reports 2 graphics adapters and 1 active display path. Nothing was hidden by the privacy filter.", result.Summary);

        // Three main cards (your PC, graphics adapters, one per display path), each with a badge, a sentence and an (i).
        Assert.Equal(new[] { UiText.Get("Card.System.Title"), UiText.Get("Card.Adapters.Title"), UiText.Format("Card.Display.Title", "1") },
            result.MainCards.Select(c => c.Title));
        Assert.All(result.MainCards, c => Assert.True(c.HasBadge && c.HasSay && c.HasTitleTip));
        Assert.Equal("Example OEM Example Model", result.MainCards[0].Say);
        Assert.Equal("Windows reports 2 graphics adapters.", result.MainCards[1].Say);
        Assert.Equal(new[] { "gpu-1", "gpu-2" }, result.MainCards[1].Facts.Select(f => f.Label));
        var display = result.MainCards[2];
        Assert.Equal("2560 × 1600 at 165 Hz", display.Say);
        var facts = display.Facts.ToDictionary(f => f.Label, f => f.Value);
        Assert.Equal("DisplayPort (embedded)", facts[UiText.Get("Field.OutputTechnology")]);
        Assert.Equal("gpu-2 (Example GPU)", facts[UiText.Get("Field.SourceAdapter")]);
        Assert.Equal("gpu-2 (Example GPU)", facts[UiText.Get("Field.TargetAdapter")]);

        // One driver card per adapter.
        Assert.Equal(new[] { "Driver for gpu-1", "Driver for gpu-2" }, result.DriverCards.Select(c => c.Title));
        Assert.Equal("Example Vendor 1.2.3.4", result.DriverCards[0].Say);
        Assert.Equal("Example GPU", result.DriverCards[0].Subtitle);
        Assert.Contains(result.DriverCards[0].Facts, f => f.Label == UiText.Get("Field.PciVendorId") && f.Value == "10DE");
        Assert.Contains(result.DriverCards[0].Facts, f => f.Label == UiText.Get("Field.DriverDate") && f.Value == "2026-09-01");

        // Then findings, report notes and collection; every card is listed in display order.
        Assert.Equal(new[] { "Card.Findings.Title", "Card.Warnings.Title", "Card.Collection.Title" }.Select(UiText.Get),
            result.MoreCards.Select(c => c.Title));
        Assert.Equal(8, result.Cards.Count);
        Assert.Equal(6, result.Limits.Count);
        Assert.False(model.ShowTechnical); // One switch for the whole page, off by default.
    }

    [Fact]
    public async Task UnavailableValuesUseFriendlyStateTextWithAnIcon()
    {
        var redacted = await ScanResult("redacted");
        var name = redacted.MainCards[1].Facts[0];
        Assert.Equal("gpu-1", name.Label);
        Assert.False(name.IsAvailable);
        Assert.Equal(UiText.Get("State.Redacted"), name.Value);
        Assert.NotEmpty(name.StateGlyph);
        Assert.Null(redacted.DriverCards[0].Subtitle); // A hidden name is never shown.
        Assert.Equal("Windows reports 1 graphics adapter. " + UiText.Get("Summary.PathsUnavailable") + " " + UiText.Get("Summary.HiddenOne"),
            redacted.Summary);
        Assert.Equal(UiText.Get("Card.Displays.Unavailable"), redacted.MainCards[2].Say);

        var unmatched = await ScanResult("unmatched");
        var association = unmatched.MainCards[2].Facts.Single(f => f.Label == UiText.Get("Field.SourceAdapter"));
        Assert.False(association.IsAvailable);
        Assert.Equal(UiText.Get("Display.Association.Unresolved"), association.Value);

        var incomplete = await ScanResult("incomplete");
        Assert.True(incomplete.IsIncomplete);
        Assert.Contains(UiText.Get("Summary.Incomplete"), incomplete.Summary);

        var empty = await ScanResult("empty");
        Assert.Equal(UiText.Get("Card.Adapters.SayNone"), empty.MainCards[1].Say);
        Assert.Empty(empty.DriverCards);
    }

    [Fact]
    public void EveryDisplayPathGetsACardWithItsOwnSourceAndTargetLinks()
    {
        var topology = Fixture("topology");
        var path = topology.Facts.Displays.Value![0];
        static Observation<AdapterMatch> Linked(string gpu) => Observation<AdapterMatch>.Known(
            new(gpu, AdapterMatchEvidence.ExactSetupApiInstanceId, AdapterMatchConfidence.Exact), DataSource.SetupApiInstanceJoin);
        var split = path with { SourceAdapter = Linked("gpu-1"), TargetAdapter = Linked("gpu-2") };
        var unresolved = path with
        {
            TargetAdapter = Observation<AdapterMatch>.Absent(DataState.Unknown, DataSource.SetupApiInstanceJoin, ReasonCode.UnmatchedAdapter)
        };
        var snapshot = topology with
        {
            Facts = topology.Facts with
            {
                Displays = Observation<IReadOnlyList<DisplayFacts>>.Known([path, split, unresolved], DataSource.DisplayConfig)
            }
        };
        var result = new ResultViewModel(ReportDocument.From(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 26))));

        Assert.StartsWith("Windows reports 2 graphics adapters and 3 active display paths.", result.Summary);
        Assert.Equal(new[] { "1", "2", "3" }.Select(n => UiText.Format("Card.Display.Title", n)), result.MainCards.Skip(2).Select(c => c.Title));
        Dictionary<string, FactLine> Links(int card) => result.MainCards[card].Facts.ToDictionary(f => f.Label);
        var source = UiText.Get("Field.SourceAdapter");
        var target = UiText.Get("Field.TargetAdapter");
        Assert.Equal("gpu-1 (Example GPU)", Links(3)[source].Value); // Source and target on different adapters.
        Assert.Equal("gpu-2 (Example GPU)", Links(3)[target].Value);
        Assert.Equal("gpu-2 (Example GPU)", Links(4)[source].Value);
        Assert.False(Links(4)[target].IsAvailable); // An unconfirmed link keeps the neutral wording and an icon.
        Assert.Equal(UiText.Get("Display.Association.Unresolved"), Links(4)[target].Value);
        Assert.NotEmpty(Links(4)[target].StateGlyph);
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
        Assert.Equal(UiText.Get("Scan.Usually"), model.BusyDetail);
        Assert.Equal(UiText.Get("Readout.Reading"), model.ReadoutStatus);

        var request = model.CancelAsync();
        Assert.False(model.CancelCommand.CanExecute(null)); // Disabled at once.
        await request.WaitAsync(Wait);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(ScanState.Cancelling, model.State); // Controlled.
        Assert.Equal(UiText.Get("Scan.Stopping"), model.BusyText);
        Assert.False(model.HasBusyDetail);
        Assert.Equal(UiText.Get("Readout.Stopping"), model.ReadoutStatus);
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
        Assert.Equal(UiText.Get("Scan.FinishingDetail"), model.BusyDetail); // Says plainly why Cancel had no effect.
        Assert.Equal(UiText.Get("Readout.Finishing"), model.ReadoutStatus); // The readout agrees: no longer "reading".
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
        Assert.DoesNotContain("result", model.BusyDetail!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("report", model.BusyDetail!, StringComparison.OrdinalIgnoreCase);
        // Collection may already have closed after a failure: the readout neither reads nor promises a stop.
        Assert.Equal(UiText.Get("Readout.Finishing"), model.ReadoutStatus);
        Assert.NotEqual(UiText.Get("Readout.Reading"), model.ReadoutStatus);
        Assert.NotEqual(UiText.Get("Readout.Stopping"), model.ReadoutStatus);
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
        Assert.Null(model.BusyDetail);
        Assert.Equal(UiText.Get("Readout.Reading"), model.ReadoutStatus); // The request has not been delivered yet.
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
