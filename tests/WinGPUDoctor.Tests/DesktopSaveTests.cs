using System.Text;
using WinGPUDoctor.Core;
using WinGPUDoctor.Desktop;
using WinGPUDoctor.Desktop.ViewModels;
using Xunit;

namespace WinGPUDoctor.Tests;

public class DesktopSaveTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private sealed class TempFolder : IDisposable
    {
        public string FullPath { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wgd-save-" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(FullPath);
        public string File(string name) => System.IO.Path.Combine(FullPath, name);
        public void Dispose() => Directory.Delete(FullPath, recursive: true);
    }

    private static async Task<MainViewModel> Scanned(string fixture)
    {
        var model = new MainViewModel(_ => Task.FromResult(DesktopTests.Fixture(fixture)));
        Assert.False(model.OpenSaveCommand.CanExecute(null));
        await model.ScanAsync();
        model.OpenSaveCommand.Execute(null);
        Assert.True(model.IsSaving);
        Assert.False(model.IsViewingResult);
        return model;
    }

    [Fact]
    public async Task PreviewIsTheExactWriterOutputOfTheRetainedReport()
    {
        var model = await Scanned("redacted");
        var save = Assert.IsType<SaveViewModel>(model.Save);
        var shareable = model.Result!.Document.Shareable;
        Assert.Equal(ReportFormat.Markdown, save.Format);
        Assert.Equal(ReportWriter.Markdown(shareable), save.Preview);
        Assert.Equal("wingpudoctor-report.md", save.DefaultFileName);

        save.IsJson = true;
        Assert.Equal(ReportFormat.Json, save.Format);
        Assert.Equal(ReportWriter.Json(shareable), save.Preview);
        Assert.Equal(".json", save.FileExtension);
        Assert.Equal(ExplanationCatalog.Warning(WarningCode.ReviewBeforeSharing).Title, save.ReviewTitle);
        Assert.Equal(UiText.Get("Save.ReviewShort"), save.ReviewText);
        // The summary of what the exact text contains comes from the same retained report.
        var contents = save.Contents.ToDictionary(l => l.Label);
        Assert.Equal(UiText.Format("Save.Contains.Adapters", "1"), contents[UiText.Get("Card.Adapters.Title")].Value);
        Assert.Equal(UiText.Get("State.Unsupported"), contents[UiText.Get("Card.Displays.Title")].Value);
        Assert.Equal(UiText.Format("Save.Contains.Steps", "4"), contents[UiText.Get("Card.Collection.Title")].Value);
        Assert.False(contents[UiText.Get("Card.Adapters.Title")].IsData); // A description of fields, not a reported value.
        // The preview header counts hidden values; how they appear is behind its (i) tip.
        Assert.Equal(UiText.Format("Save.Hidden", "1"), save.HiddenLabel);
        Assert.Equal(UiText.Get("Save.RedactedHint"), save.HiddenHint);
        Assert.Equal(UiText.Get("Save.HiddenName"), save.HiddenHintName);

        model.CloseSaveCommand.Execute(null);
        Assert.Null(model.Save);
        Assert.True(model.IsViewingResult);
    }

    [Fact]
    public async Task ContentsSummaryNamesFieldsWithoutClaimingTheirValues()
    {
        var model = await Scanned("redacted");
        var save = model.Save!;
        // The fixture's adapter name is hidden, so the summary must not promise a name.
        Assert.Equal(DataState.Redacted, model.Result!.Document.Report.Facts.Gpus.Value![0].Name.State);
        var adapters = save.Contents.Single(l => l.Label == UiText.Get("Card.Adapters.Title"));
        Assert.Equal(UiText.Format("Save.Contains.Adapters", "1"), adapters.Value);
        Assert.Contains("fields", adapters.Value);
        Assert.DoesNotContain("token=private", adapters.Value);
        Assert.All(save.Contents, l => Assert.DoesNotContain("each with", l.Value));
        Assert.Equal(UiText.Get("Save.ContainsNote"), save.ContentsNote);
        Assert.Contains("hidden by the privacy filter", save.ContentsNote);
        Assert.Contains("unresolved", save.ContentsNote);
    }

    [Fact]
    public async Task SaveWritesExactlyThePreviewAndAFormatChangeProducesANewPreview()
    {
        using var folder = new TempFolder();
        var model = await Scanned("topology");
        var save = model.Save!;
        var markdown = save.Preview;
        Assert.Equal(SaveOutcome.Saved, save.Save(folder.File("report.md")));
        Assert.Equal(new UTF8Encoding(false).GetBytes(markdown), File.ReadAllBytes(folder.File("report.md")));
        Assert.Equal(UiText.Get("Save.Outcome.Saved"), save.StatusMessage);
        Assert.False(save.StatusIsError);

        save.Format = ReportFormat.Json;
        Assert.NotEqual(markdown, save.Preview);
        Assert.Null(save.StatusMessage); // The new preview has not been saved yet.
        Assert.Equal(SaveOutcome.Saved, save.Save(folder.File("report.json")));
        Assert.Equal(new UTF8Encoding(false).GetBytes(save.Preview), File.ReadAllBytes(folder.File("report.json")));
        Assert.Equal(ReportWriter.Json(model.Result!.Document.Shareable), File.ReadAllText(folder.File("report.json")));
    }

    [Fact]
    public async Task SaveNeverOverwritesAndRejectsNonLocalOrInvalidDestinations()
    {
        using var folder = new TempFolder();
        var save = (await Scanned("single")).Save!;
        File.WriteAllText(folder.File("existing.md"), "keep");

        Assert.Equal(SaveOutcome.AlreadyExists, save.Save(folder.File("existing.md")));
        Assert.Equal("keep", File.ReadAllText(folder.File("existing.md")));
        Assert.True(save.StatusIsError);
        Assert.Equal(SaveOutcome.NotLocal, save.Save(@"\\server\share\report.md"));
        Assert.Equal(SaveOutcome.NotLocal, save.Save(folder.File("report.md") + ":stream"));
        Assert.Equal(SaveOutcome.InvalidName, save.Save("bad\0path.md"));
        Assert.DoesNotContain(folder.FullPath, save.StatusMessage);
        Assert.Equal(new[] { "existing.md" }, Directory.GetFiles(folder.FullPath).Select(System.IO.Path.GetFileName));
    }

    [Fact]
    public async Task ANewScanDiscardsTheReportAndItsPreview()
    {
        var second = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scans = 0;
        var model = new MainViewModel(async _ =>
        {
            if (Interlocked.Increment(ref scans) == 2) await second.Task.WaitAsync(Wait);
            return DesktopTests.Fixture("single");
        });
        await model.ScanAsync();
        model.OpenSaveCommand.Execute(null);
        Assert.NotNull(model.Save);

        var running = model.ScanAsync();
        Assert.Null(model.Save);
        Assert.Null(model.Result);
        Assert.False(model.IsSaving);
        Assert.False(model.OpenSaveCommand.CanExecute(null));
        second.SetResult(true);
        await running.WaitAsync(Wait);

        Assert.Null(model.Save); // Nothing from the previous preview carries over.
        Assert.True(model.OpenSaveCommand.CanExecute(null));
    }
}
