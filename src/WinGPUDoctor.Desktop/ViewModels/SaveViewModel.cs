using System.Globalization;
using System.IO;
using WinGPUDoctor.Core;
using WinGPUDoctor.Host;

namespace WinGPUDoctor.Desktop.ViewModels;

public enum ReportFormat { Markdown, Json }

public enum SaveOutcome { Saved, AlreadyExists, NotLocal, InvalidName, WriteFailed }

// ADR 0008 D3: every preview and the save come from the scan's one retained report. Save writes
// exactly the previewed writer output through the shared Host rules: local fixed drives only,
// CreateNew, never overwrite. A format change produces a new preview before anything is saved.
public sealed class SaveViewModel : ObservableObject
{
    private readonly ReportDocument _document;
    private ReportFormat _format;
    private string _preview = "";
    private string? _status;
    private bool _statusIsError;

    public SaveViewModel(ReportDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        Contents = Summarize(document.Report);
        Refresh();
    }

    // A short overview of the fields the exact text below contains, from the same retained report.
    // It names fields, never values: any value may be unavailable, redacted or unresolved.
    public IReadOnlyList<FactLine> Contents { get; }
    public string ContentsNote { get; } = UiText.Get("Save.ContainsNote");

    private static IReadOnlyList<FactLine> Summarize(DiagnosticReport report)
    {
        static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);
        var facts = report.Facts;
        return
        [
            FactLine.Text("Card.System.Title", UiText.Get("Save.Contains.System")),
            facts.Gpus.State == DataState.Available
                ? FactLine.Text("Card.Adapters.Title", UiText.Format("Save.Contains.Adapters", Count(facts.Gpus.Value!.Count)))
                : FactLine.Unavailable("Card.Adapters.Title", facts.Gpus.State),
            facts.Displays.State == DataState.Available
                ? FactLine.Text("Card.Displays.Title", UiText.Format("Save.Contains.Displays", Count(facts.Displays.Value!.Count)))
                : FactLine.Unavailable("Card.Displays.Title", facts.Displays.State),
            FactLine.Text("Card.Findings.Title", Count(report.Findings.Count)),
            FactLine.Text("Card.Warnings.Title", Count(report.Warnings.Count)),
            FactLine.Text("Card.Collection.Title", UiText.Format("Save.Contains.Steps", Count(report.Collection.Count))),
            FactLine.Text("Summary.Redacted", Count(report.Privacy.RedactedFields)) with { Help = UiText.Get("Save.RedactedHint") }
        ];
    }

    public ReportFormat Format
    {
        get => _format;
        set
        {
            if (!Set(ref _format, value)) return;
            OnPropertyChanged(nameof(IsMarkdown));
            OnPropertyChanged(nameof(IsJson));
            OnPropertyChanged(nameof(FileExtension));
            OnPropertyChanged(nameof(DefaultFileName));
            OnPropertyChanged(nameof(FileFilter));
            Refresh();
        }
    }

    // Radio-button bindings.
    public bool IsMarkdown { get => Format == ReportFormat.Markdown; set { if (value) Format = ReportFormat.Markdown; } }
    public bool IsJson { get => Format == ReportFormat.Json; set { if (value) Format = ReportFormat.Json; } }

    public string Preview { get => _preview; private set => Set(ref _preview, value); }
    public string FileExtension => Format == ReportFormat.Json ? ".json" : ".md";
    public string DefaultFileName => "wingpudoctor-report" + FileExtension;
    public string FileFilter => UiText.Get(Format == ReportFormat.Json ? "Save.FilterJson" : "Save.FilterMarkdown");
    public string ReviewTitle { get; } = ExplanationCatalog.Warning(WarningCode.ReviewBeforeSharing).Title;
    public string ReviewText { get; } = UiText.Get("Save.ReviewShort");

    public string? StatusMessage { get => _status; private set { if (Set(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => StatusMessage is not null;
    public bool StatusIsError { get => _statusIsError; private set => Set(ref _statusIsError, value); }

    public SaveOutcome Save(string path)
    {
        var text = Preview; // Exactly what the user is looking at.
        var destination = HostExport.ResolveDestination(path);
        var outcome = destination.Status switch
        {
            ExportDestinationStatus.Accepted when File.Exists(path) => SaveOutcome.AlreadyExists,
            ExportDestinationStatus.Accepted => HostExport.TryWriteNew(destination, text) ? SaveOutcome.Saved : SaveOutcome.WriteFailed,
            ExportDestinationStatus.InvalidLocalDestination => SaveOutcome.InvalidName,
            _ => SaveOutcome.NotLocal
        };
        StatusIsError = outcome != SaveOutcome.Saved;
        StatusMessage = UiText.Get("Save.Outcome." + outcome);
        return outcome;
    }

    private void Refresh()
    {
        Preview = Format == ReportFormat.Json ? ReportWriter.Json(_document.Shareable) : ReportWriter.Markdown(_document.Shareable);
        StatusMessage = null;
    }
}
