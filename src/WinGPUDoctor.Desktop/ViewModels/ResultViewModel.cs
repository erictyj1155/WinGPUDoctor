using System.Globalization;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Desktop.ViewModels;

// Summary cards over the round-tripped report. Copy states reported facts and limits only:
// no health verdict, driver freshness, rendering GPU, physical connection or inferred vendor.
public sealed class ResultViewModel
{
    public ResultViewModel(ReportDocument document)
    {
        Document = document;
        var report = document.Report;
        IsIncomplete = report.Warnings.Contains(WarningCode.CollectionIncomplete);
        Headline = UiText.Get(IsIncomplete ? "Summary.Incomplete" : "Summary.Complete");
        Summary = BuildSummary(report);
        Cards = [SystemCard(report.Facts.System), .. AdapterCards(report.Facts.Gpus),
            .. DisplayCards(report.Facts.Displays, report.Facts.Gpus), LimitsCard()];
    }

    public ReportDocument Document { get; }
    public bool IsIncomplete { get; }
    public string Headline { get; }
    public IReadOnlyList<FactLine> Summary { get; }
    public IReadOnlyList<CardViewModel> Cards { get; }

    private static string Count<T>(IReadOnlyList<T> items) => items.Count.ToString(CultureInfo.CurrentCulture);

    private static IReadOnlyList<FactLine> BuildSummary(DiagnosticReport report)
    {
        var lines = new List<FactLine>
        {
            report.Facts.Gpus.State == DataState.Available
                ? FactLine.Text("Summary.Adapters", Count(report.Facts.Gpus.Value!))
                : FactLine.Unavailable("Summary.Adapters", report.Facts.Gpus.State),
            report.Facts.Displays.State == DataState.Available
                ? FactLine.Text("Summary.DisplayPaths", Count(report.Facts.Displays.Value!))
                : FactLine.Unavailable("Summary.DisplayPaths", report.Facts.Displays.State)
        };
        if (report.Privacy.RedactedFields > 0)
            lines.Add(FactLine.Text("Summary.Redacted", report.Privacy.RedactedFields.ToString(CultureInfo.CurrentCulture)));
        return lines;
    }

    private static CardViewModel SystemCard(SystemFacts system) => new(UiText.Get("Card.System.Title"), null,
    [
        FactLine.Of("Field.WindowsVersion", system.WindowsVersion), FactLine.Of("Field.WindowsBuild", system.WindowsBuild),
        FactLine.Of("Field.Manufacturer", system.Manufacturer), FactLine.Of("Field.Model", system.Model)
    ], []);

    private static IEnumerable<CardViewModel> AdapterCards(Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        if (gpus.State != DataState.Available)
        {
            yield return new(UiText.Get("Card.Adapters.Title"), null, [FactLine.Unavailable("Summary.Adapters", gpus.State)], []);
            yield break;
        }
        if (gpus.Value!.Count == 0)
            yield return new(UiText.Get("Card.Adapters.Title"), null, [], [UiText.Get("Card.Adapters.Empty")]);
        foreach (var gpu in gpus.Value)
        {
            var title = gpu.Name.State == DataState.Available ? gpu.Name.Value! : UiText.Get("Card.Adapter.Title");
            yield return new(title, UiText.Format("Card.ReportLabel", gpu.Id),
            [
                FactLine.Of("Field.AdapterName", gpu.Name), FactLine.Of("Field.PciVendorId", gpu.PciVendorId),
                FactLine.Of("Field.DriverProvider", gpu.Driver.Provider), FactLine.Of("Field.DriverVersion", gpu.Driver.Version),
                FactLine.Of("Field.DriverDate", gpu.Driver.Date)
            ], []);
        }
    }

    private static IEnumerable<CardViewModel> DisplayCards(Observation<IReadOnlyList<DisplayFacts>> displays,
        Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        if (displays.State != DataState.Available)
        {
            yield return new(UiText.Get("Card.Displays.Title"), null, [FactLine.Unavailable("Summary.DisplayPaths", displays.State)], []);
            yield break;
        }
        if (displays.Value!.Count == 0)
            yield return new(UiText.Get("Card.Displays.Title"), null, [], [UiText.Get("Card.Displays.Empty")]);
        for (var i = 0; i < displays.Value.Count; i++)
        {
            var d = displays.Value[i];
            yield return new(UiText.Format("Card.Display.Title", (i + 1).ToString(CultureInfo.CurrentCulture)),
                UiText.Format("Card.ReportLabel", d.Id),
            [
                FactLine.Of("Field.MonitorName", d.Name),
                d.SourceResolution.State == DataState.Available
                    ? FactLine.Text("Field.Resolution", DisplayFormat.Resolution(d.SourceResolution.Value!))
                    : FactLine.Unavailable("Field.Resolution", d.SourceResolution.State),
                d.PathRefreshRate.State == DataState.Available
                    ? FactLine.Text("Field.RefreshRate", DisplayFormat.Rate(d.PathRefreshRate.Value!))
                    : FactLine.Unavailable("Field.RefreshRate", d.PathRefreshRate.State),
                d.OutputTechnology.State == DataState.Available
                    ? FactLine.Text("Field.OutputTechnology", DisplayFormat.OutputTechnology(d.OutputTechnology.Value!))
                    : FactLine.Unavailable("Field.OutputTechnology", d.OutputTechnology.State),
                Association("Field.SourceAdapter", d.SourceAdapter, gpus),
                Association("Field.TargetAdapter", d.TargetAdapter, gpus)
            ], []);
        }
    }

    // Names an association only when the report has an exact match; otherwise says it is unresolved.
    private static FactLine Association(string labelKey, Observation<AdapterMatch> match, Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        if (match.State != DataState.Available)
            return new(UiText.Get(labelKey), UiText.Get("Display.Association.Unresolved"), false, FactLine.Glyph(match.State));
        var id = match.Value!.GpuId;
        var gpu = gpus.Value?.FirstOrDefault(g => g.Id == id);
        return FactLine.Text(labelKey, gpu?.Name.State == DataState.Available
            ? UiText.Format("Display.Association.Named", id, gpu.Name.Value!)
            : id);
    }

    private static CardViewModel LimitsCard() => new(UiText.Get("Card.Limits.Title"), UiText.Get("Card.Limits.Intro"), [],
        new[] { "Card.Limits.RenderingGpu", "Card.Limits.Utilization", "Card.Limits.HybridMode", "Card.Limits.DriverFreshness",
            "Card.Limits.Health", "Card.Limits.Connection" }.Select(UiText.Get).ToArray());
}
