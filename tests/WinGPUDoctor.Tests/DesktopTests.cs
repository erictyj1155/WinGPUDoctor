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

    // Like the supervised collector, a cancelled token ends collection as host-cancelled.
    private static Func<CancellationToken, Task<CollectionSnapshot>> WaitForCancellation(TaskCompletionSource<bool> collecting) =>
        async token =>
        {
            var signal = Gate();
            using var registration = token.Register(() => signal.TrySetResult(true));
            collecting.SetResult(true);
            await signal.Task.WaitAsync(Wait);
            throw new SupervisedCollectionException("host-cancelled");
        };

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
        // This PC, two adapters, one display path and the limits card.
        Assert.Equal(5, result.Cards.Count);
        Assert.Equal("Example GPU", result.Cards[1].Title);
        Assert.Contains(result.Cards[1].Facts, f => f.Label == UiText.Get("Field.PciVendorId") && f.Value == "10DE");
        var display = result.Cards[3].Facts.ToDictionary(f => f.Label, f => f.Value);
        Assert.Equal("2560 × 1600 pixels", display[UiText.Get("Field.Resolution")]);
        Assert.Equal("165 Hz", display[UiText.Get("Field.RefreshRate")]);
        Assert.Equal("DisplayPort (embedded)", display[UiText.Get("Field.OutputTechnology")]);
        Assert.Equal("gpu-2 (Example GPU)", display[UiText.Get("Field.SourceAdapter")]);
        Assert.Equal("gpu-2 (Example GPU)", display[UiText.Get("Field.TargetAdapter")]);
        Assert.Equal(UiText.Get("Card.Limits.Title"), result.Cards[4].Title);
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
        var collecting = Gate();
        var model = new MainViewModel(WaitForCancellation(collecting));
        var scan = model.ScanAsync();
        await collecting.Task.WaitAsync(Wait);
        Assert.Equal(ScanState.Scanning, model.State);
        Assert.False(model.ScanCommand.CanExecute(null));
        Assert.True(model.CancelCommand.CanExecute(null));

        model.CancelCommand.Execute(null);
        Assert.Equal(ScanState.Cancelling, model.State);
        Assert.False(model.CancelCommand.CanExecute(null));
        model.Cancel(); // The second cancel is a no-op.
        Assert.Equal(ScanState.Cancelling, model.State);

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
    public async Task ClosingDuringAScanRequestsControlledCancellationFirst()
    {
        Assert.True(new MainViewModel(_ => Task.FromResult(Fixture("single"))).RequestClose());
        var collecting = Gate();
        var model = new MainViewModel(WaitForCancellation(collecting));
        var closeReady = 0;
        model.CloseReady += (_, _) => closeReady++;
        var scan = model.ScanAsync();
        await collecting.Task.WaitAsync(Wait);

        Assert.False(model.RequestClose());
        Assert.Equal(ScanState.Cancelling, model.State);
        Assert.False(model.RequestClose()); // Still waiting for the controlled stop.

        await scan.WaitAsync(Wait);
        Assert.Equal(ScanState.Stopped, model.State);
        Assert.Equal(1, closeReady);
        Assert.True(model.RequestClose());
    }
}
