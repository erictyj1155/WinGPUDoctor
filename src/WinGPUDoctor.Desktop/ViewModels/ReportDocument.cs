using System.IO;
using System.Text.Json;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Desktop.ViewModels;

// ADR 0008 D3: the one privacy-projected report retained for a completed scan. Display data is
// read back from the exact JSON writer output; the GUI never renders a raw CollectionSnapshot.
public sealed class ReportDocument
{
    private ReportDocument(ShareableReport shareable, DiagnosticReport report)
    {
        Shareable = shareable;
        Report = report;
    }

    public ShareableReport Shareable { get; }
    public DiagnosticReport Report { get; }

    public static ReportDocument From(ShareableReport shareable)
    {
        ArgumentNullException.ThrowIfNull(shareable);
        var report = JsonSerializer.Deserialize<DiagnosticReport>(ReportWriter.Json(shareable), ReportWriter.JsonOptions)
            ?? throw new InvalidDataException("The report could not be read back.");
        return new(shareable, report);
    }
}
