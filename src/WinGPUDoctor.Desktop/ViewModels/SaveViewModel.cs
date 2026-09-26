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
        Refresh();
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
    public string RedactedSummary => UiText.Format("Save.Redacted", _document.Report.Privacy.RedactedFields);
    public Explanation ReviewWarning { get; } = Explanation.From(ExplanationCatalog.Warning(WarningCode.ReviewBeforeSharing));

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
