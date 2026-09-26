using System.Globalization;
using System.Text.RegularExpressions;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Desktop.ViewModels;

// Cards over the round-tripped report. Copy states reported facts and limits only:
// no health verdict, driver freshness, rendering GPU, physical connection or inferred vendor.
public sealed class ResultViewModel
{
    private static readonly Regex DisplayEvidence = new(@"\Afacts\.displays\.value\[(\d+)\]", RegexOptions.CultureInvariant);

    public ResultViewModel(ReportDocument document)
    {
        Document = document;
        var report = document.Report;
        IsIncomplete = report.Warnings.Contains(WarningCode.CollectionIncomplete);
        Headline = UiText.Get(IsIncomplete ? "Summary.Incomplete" : "Summary.Complete");
        Summary = BuildSummary(report);
        Cards = [SystemCard(report.Facts.System), .. AdapterCards(report.Facts.Gpus),
            .. DisplayCards(report.Facts.Displays, report.Facts.Gpus), FindingsCard(report.Findings),
            WarningsCard(report.Warnings), CollectionCard(report.Collection), LimitsCard()];
    }

    public ReportDocument Document { get; }
    public bool IsIncomplete { get; }
    public string Headline { get; }
    public IReadOnlyList<FactLine> Summary { get; }
    public IReadOnlyList<CardViewModel> Cards { get; }

    private static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static IReadOnlyList<FactLine> BuildSummary(DiagnosticReport report)
    {
        var lines = new List<FactLine>
        {
            report.Facts.Gpus.State == DataState.Available
                ? FactLine.Text("Summary.Adapters", Number(report.Facts.Gpus.Value!.Count))
                : FactLine.Unavailable("Summary.Adapters", report.Facts.Gpus.State),
            report.Facts.Displays.State == DataState.Available
                ? FactLine.Text("Summary.DisplayPaths", Number(report.Facts.Displays.Value!.Count))
                : FactLine.Unavailable("Summary.DisplayPaths", report.Facts.Displays.State)
        };
        if (report.Privacy.RedactedFields > 0)
            lines.Add(FactLine.Text("Summary.Redacted", Number(report.Privacy.RedactedFields)));
        return lines;
    }

    private static CardViewModel SystemCard(SystemFacts system) => new(UiText.Get("Card.System.Title"), null,
    [
        FactLine.Of("Field.WindowsVersion", system.WindowsVersion), FactLine.Of("Field.WindowsBuild", system.WindowsBuild),
        FactLine.Of("Field.Manufacturer", system.Manufacturer), FactLine.Of("Field.Model", system.Model)
    ], [], technical:
    [
        TechnicalLine.Of("Field.WindowsVersion", system.WindowsVersion), TechnicalLine.Of("Field.WindowsBuild", system.WindowsBuild),
        TechnicalLine.Of("Field.Manufacturer", system.Manufacturer), TechnicalLine.Of("Field.Model", system.Model)
    ]);

    private static IEnumerable<CardViewModel> AdapterCards(Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        if (gpus.State != DataState.Available)
        {
            yield return new(UiText.Get("Card.Adapters.Title"), null, [FactLine.Unavailable("Summary.Adapters", gpus.State)], [],
                technical: [TechnicalLine.Of("Summary.Adapters", gpus, list => Number(list.Count))]);
            yield break;
        }
        if (gpus.Value!.Count == 0)
            yield return new(UiText.Get("Card.Adapters.Title"), null, [], [UiText.Get("Card.Adapters.Empty")]);
        foreach (var gpu in gpus.Value)
        {
            var title = gpu.Name.State == DataState.Available ? gpu.Name.Value! : UiText.Get("Card.Adapter.Title");
            yield return new(title, UiText.Format("Card.ReportLabel", gpu.Id),
            [
                FactLine.Of("Field.AdapterName", gpu.Name), FactLine.Of("Field.PciVendorId", gpu.PciVendorId).WithGlossary("PciVendorId"),
                FactLine.Of("Field.DriverProvider", gpu.Driver.Provider),
                FactLine.Of("Field.DriverVersion", gpu.Driver.Version).WithGlossary("DriverVersion"),
                FactLine.Of("Field.DriverDate", gpu.Driver.Date).WithGlossary("DriverDate")
            ], [], technical:
            [
                TechnicalLine.Of("Field.AdapterName", gpu.Name), TechnicalLine.Of("Field.PciVendorId", gpu.PciVendorId),
                TechnicalLine.Of("Field.PciDeviceId", gpu.PciDeviceId), TechnicalLine.Of("Field.Classification", gpu.Classification),
                TechnicalLine.Of("Field.DriverProvider", gpu.Driver.Provider), TechnicalLine.Of("Field.DriverVersion", gpu.Driver.Version),
                TechnicalLine.Of("Field.DriverDate", gpu.Driver.Date)
            ]);
        }
    }

    private static IEnumerable<CardViewModel> DisplayCards(Observation<IReadOnlyList<DisplayFacts>> displays,
        Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        if (displays.State != DataState.Available)
        {
            yield return new(UiText.Get("Card.Displays.Title"), null, [FactLine.Unavailable("Summary.DisplayPaths", displays.State)], [],
                technical: [TechnicalLine.Of("Summary.DisplayPaths", displays, list => Number(list.Count))]);
            yield break;
        }
        if (displays.Value!.Count == 0)
            yield return new(UiText.Get("Card.Displays.Title"), null, [], [UiText.Get("Card.Displays.Empty")]);
        for (var i = 0; i < displays.Value.Count; i++)
        {
            var d = displays.Value[i];
            yield return new(DisplayTitle(i), UiText.Format("Card.ReportLabel", d.Id),
            [
                FactLine.Of("Field.MonitorName", d.Name).WithGlossary("MonitorName"),
                (d.SourceResolution.State == DataState.Available
                    ? FactLine.Text("Field.Resolution", DisplayFormat.Resolution(d.SourceResolution.Value!))
                    : FactLine.Unavailable("Field.Resolution", d.SourceResolution.State)).WithGlossary("Resolution"),
                (d.PathRefreshRate.State == DataState.Available
                    ? FactLine.Text("Field.RefreshRate", DisplayFormat.Rate(d.PathRefreshRate.Value!))
                    : FactLine.Unavailable("Field.RefreshRate", d.PathRefreshRate.State)).WithGlossary("RefreshRate"),
                (d.OutputTechnology.State == DataState.Available
                    ? FactLine.Text("Field.OutputTechnology", DisplayFormat.OutputTechnology(d.OutputTechnology.Value!))
                    : FactLine.Unavailable("Field.OutputTechnology", d.OutputTechnology.State)).WithGlossary("OutputTechnology"),
                Association("Field.SourceAdapter", d.SourceAdapter, gpus).WithGlossary("SourceAdapter"),
                Association("Field.TargetAdapter", d.TargetAdapter, gpus).WithGlossary("TargetAdapter")
            ], [], technical: DisplayTechnical(d));
        }
    }

    private static string DisplayTitle(int index) => UiText.Format("Card.Display.Title", Number(index + 1));

    private static IReadOnlyList<TechnicalLine> DisplayTechnical(DisplayFacts d) =>
    [
        TechnicalLine.Of("Field.MonitorName", d.Name),
        TechnicalLine.Plain("Field.SourceTarget", UiText.Format("Technical.SourceTarget", d.SourceId, d.TargetId)),
        TechnicalLine.Of("Field.SourceGdiName", d.SourceGdiName),
        TechnicalLine.Of("Field.SourceAdapter", d.SourceAdapter, m => UiText.Format("Technical.Match", d.SourceAdapterId, m.GpuId, m.Evidence, m.Confidence)),
        TechnicalLine.Of("Field.TargetAdapter", d.TargetAdapter, m => UiText.Format("Technical.Match", d.TargetAdapterId, m.GpuId, m.Evidence, m.Confidence)),
        TechnicalLine.Of("Field.OutputTechnology", d.OutputTechnology),
        TechnicalLine.Of("Field.Resolution", d.SourceResolution, DisplayFormat.Resolution),
        TechnicalLine.Of("Field.RefreshRate", d.PathRefreshRate, DisplayFormat.ExactRate),
        TechnicalLine.Of("Field.SignalRate", d.SignalRefreshRate, DisplayFormat.ExactRate),
        TechnicalLine.Of("Field.ScanLineOrdering", d.ScanLineOrdering),
        TechnicalLine.Of("Field.Rotation", d.Rotation),
        TechnicalLine.Plain("Field.PathActive", DisplayFormat.YesNo(d.PathActive)),
        TechnicalLine.Plain("Field.TargetAvailable", DisplayFormat.YesNo(d.TargetAvailable)),
        TechnicalLine.Of("Field.RefreshBoost", d.RefreshRateBoost, flag => DisplayFormat.YesNo(flag.Enabled)),
        TechnicalLine.Of("Field.CloneGroup", d.CloneGroupId),
        TechnicalLine.Plain("Field.QueryMode", d.QueryMode.ToString())
    ];

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

    // Findings in report order. Unknown IDs fall back to Core's own message, never to invented copy.
    private static CardViewModel FindingsCard(IReadOnlyList<DiagnosticFinding> findings)
    {
        var explanations = findings.Select(f =>
        {
            var match = f.Evidence.Select(e => DisplayEvidence.Match(e)).FirstOrDefault(m => m.Success);
            var context = match is null ? null : DisplayTitle(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            return ExplanationCatalog.Finding(f.Id) is { } entry
                ? Explanation.From(entry, context)
                : new Explanation(context, f.Id, f.Message, null, null);
        }).ToArray();
        return new(UiText.Get("Card.Findings.Title"), UiText.Get("Card.Findings.Intro"), [],
            findings.Count == 0 ? [UiText.Get("Card.Findings.None")] : [], explanations,
            technical: findings.Select(f => new TechnicalLine(f.Id, f.Message, string.Join(", ", f.Evidence))).ToArray());
    }

    private static CardViewModel WarningsCard(IReadOnlyList<WarningCode> warnings) =>
        new(UiText.Get("Card.Warnings.Title"), null, [], [],
            warnings.Select(w => Explanation.From(ExplanationCatalog.Warning(w))).ToArray(),
            technical: warnings.Select(w => new TechnicalLine(w.ToString(), ReportWriter.WarningText(w), "")).ToArray());

    private static CardViewModel CollectionCard(IReadOnlyList<CollectorRun> runs) =>
        new(UiText.Get("Card.Collection.Title"), UiText.Get("Card.Collection.Intro"),
            runs.Select(r => new FactLine(ExplanationCatalog.Source(r.Source), ExplanationCatalog.Collector(r.Status).Title,
                r.Status == CollectorStatus.Succeeded, r.Status switch
                {
                    CollectorStatus.Succeeded => "",
                    CollectorStatus.Unsupported => "\uE946",
                    _ => "\uE7BA"
                }) { Help = ExplanationCatalog.Collector(r.Status).Meaning }).ToArray(), [],
            technical: runs.Select(r => new TechnicalLine(ExplanationCatalog.Source(r.Source),
                UiText.Format("Technical.Run", r.Status, r.Reason, Number(r.Attempts), r.QueryMode),
                r.Issues.Count == 0 ? UiText.Get("Technical.NoIssues")
                    : string.Join("; ", r.Issues.Select(i => i.NativeErrorCode is { } code
                        ? UiText.Format("Technical.IssueCode", i.Operation, i.Reason, code.ToString(CultureInfo.InvariantCulture))
                        : UiText.Format("Technical.Issue", i.Operation, i.Reason))))).ToArray());

    private static CardViewModel LimitsCard() => new(UiText.Get("Card.Limits.Title"), UiText.Get("Card.Limits.Intro"), [],
        new[] { "Card.Limits.RenderingGpu", "Card.Limits.Utilization", "Card.Limits.HybridMode", "Card.Limits.DriverFreshness",
            "Card.Limits.Health", "Card.Limits.Connection" }.Select(UiText.Get).ToArray());
}
