using System.Globalization;
using System.Text.RegularExpressions;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Desktop.ViewModels;

// Cards over the round-tripped report, in the guided layout: a summary sentence, three main cards
// (this PC, graphics adapters, one per active display path), one driver card per adapter, the limits
// note and further notes about the scan. Copy states reported facts and limits only: no health
// verdict, driver freshness, rendering GPU, physical connection or inferred vendor.
public sealed class ResultViewModel
{
    private static readonly Regex DisplayEvidence = new(@"\Afacts\.displays\.value\[(\d+)\]", RegexOptions.CultureInvariant);

    public ResultViewModel(ReportDocument document)
    {
        Document = document;
        var report = document.Report;
        IsIncomplete = report.Warnings.Contains(WarningCode.CollectionIncomplete);
        Headline = UiText.Get("Result.Title");
        Summary = BuildSummary(report, IsIncomplete);
        MainCards = [SystemCard(report.Facts.System), AdaptersCard(report.Facts.Gpus), .. DisplayCards(report.Facts.Displays, report.Facts.Gpus)];
        DriverCards = [.. DriverCardsFor(report.Facts.Gpus)];
        MoreCards = [FindingsCard(report.Findings), WarningsCard(report.Warnings), CollectionCard(report.Collection)];
        Limits = new[] { "Card.Limits.RenderingGpu", "Card.Limits.Utilization", "Card.Limits.HybridMode", "Card.Limits.DriverFreshness",
            "Card.Limits.Health", "Card.Limits.Connection" }.Select(UiText.Get).ToArray();
        Cards = [.. MainCards, .. DriverCards, .. MoreCards];
    }

    public ReportDocument Document { get; }
    public bool IsIncomplete { get; }
    public string Headline { get; }
    public string Summary { get; }
    public IReadOnlyList<CardViewModel> MainCards { get; }
    public IReadOnlyList<CardViewModel> DriverCards { get; }
    public IReadOnlyList<CardViewModel> MoreCards { get; }
    public bool HasDriverCards => DriverCards.Count > 0;
    public IReadOnlyList<string> Limits { get; }

    // Every card in display order.
    public IReadOnlyList<CardViewModel> Cards { get; }

    private static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static string Count(string key, int value) =>
        value == 1 ? UiText.Get(key + ".One") : UiText.Format(key + ".Many", Number(value));

    // What Windows reported, whether reading was incomplete, and how many values were hidden. It never
    // says that everything was read: a completed step can still leave individual values missing.
    private static string BuildSummary(DiagnosticReport report, bool incomplete)
    {
        var facts = report.Facts;
        var adapters = facts.Gpus.State == DataState.Available ? Count("Count.Adapters", facts.Gpus.Value!.Count) : null;
        var paths = facts.Displays.State == DataState.Available ? Count("Count.Paths", facts.Displays.Value!.Count) : null;
        var sentences = new List<string?>
        {
            (adapters, paths) switch
            {
                ({ } a, { } p) => UiText.Format("Summary.ReportsBoth", a, p),
                ({ } a, null) => UiText.Format("Summary.ReportsOne", a),
                (null, { } p) => UiText.Format("Summary.ReportsOne", p),
                _ => null
            },
            adapters is null ? UiText.Get("Summary.AdaptersUnavailable") : null,
            paths is null ? UiText.Get("Summary.PathsUnavailable") : null,
            incomplete ? UiText.Get("Summary.Incomplete") : null,
            report.Privacy.RedactedFields switch
            {
                0 => UiText.Get("Summary.HiddenNone"),
                1 => UiText.Get("Summary.HiddenOne"),
                var hidden => UiText.Format("Summary.HiddenMany", Number(hidden))
            }
        };
        return string.Join(" ", sentences.OfType<string>());
    }

    // Manufacturer and model form the sentence; the details list Windows and any value that is unavailable.
    private static CardViewModel SystemCard(SystemFacts system)
    {
        var names = new[] { system.Manufacturer, system.Model }.Where(f => f.State == DataState.Available).Select(f => f.Value!).ToArray();
        var facts = new List<FactLine> { FactLine.Of("Field.WindowsVersion", system.WindowsVersion), FactLine.Of("Field.WindowsBuild", system.WindowsBuild) };
        if (system.Manufacturer.State != DataState.Available) facts.Add(FactLine.Of("Field.Manufacturer", system.Manufacturer));
        if (system.Model.State != DataState.Available) facts.Add(FactLine.Of("Field.Model", system.Model));
        return new(UiText.Get("Card.System.Title"), null, facts, [], technical:
        [
            TechnicalLine.Of("Field.WindowsVersion", system.WindowsVersion), TechnicalLine.Of("Field.WindowsBuild", system.WindowsBuild),
            TechnicalLine.Of("Field.Manufacturer", system.Manufacturer), TechnicalLine.Of("Field.Model", system.Model)
        ])
        {
            Glyph = "", Tone = CardTone.Data, TitleTip = ExplanationCatalog.Glossary("System"),
            Say = names.Length > 0 ? string.Join(" ", names) : UiText.Get("Card.System.NoName")
        };
    }

    // The inventory as Windows reports it: one line per adapter with its report label and name.
    private static CardViewModel AdaptersCard(Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        var title = UiText.Get("Card.Adapters.Title");
        TechnicalLine[] technical = [TechnicalLine.Of("Summary.Adapters", gpus, list => Number(list.Count))];
        var tip = ExplanationCatalog.Glossary("Adapters");
        if (gpus.State != DataState.Available)
            return new(title, null, [FactLine.Unavailable("Summary.Adapters", gpus.State)], [], technical: technical)
            {
                Glyph = "", Tone = CardTone.Accent, TitleTip = tip, Say = UiText.Get("Card.Adapters.Unavailable")
            };
        var list = gpus.Value!;
        return new(title, null, list.Select(g => FactLine.Labelled(g.Id, g.Name)).ToArray(),
            list.Count == 0 ? [UiText.Get("Card.Adapters.Empty")] : [], technical: technical)
        {
            Glyph = "", Tone = CardTone.Accent, TitleTip = tip,
            Say = list.Count == 0 ? UiText.Get("Card.Adapters.SayNone") : UiText.Format("Card.Adapters.Say", Count("Count.Adapters", list.Count))
        };
    }

    // One card per active display path: the mode as a sentence, then output technology and both adapter links.
    private static IEnumerable<CardViewModel> DisplayCards(Observation<IReadOnlyList<DisplayFacts>> displays,
        Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        TechnicalLine[] count = [TechnicalLine.Of("Summary.DisplayPaths", displays, list => Number(list.Count))];
        if (displays.State != DataState.Available)
        {
            yield return new(UiText.Get("Card.Displays.Title"), null, [FactLine.Unavailable("Summary.DisplayPaths", displays.State)], [],
                technical: count) { Glyph = "", Tone = CardTone.Data, Say = UiText.Get("Card.Displays.Unavailable") };
            yield break;
        }
        if (displays.Value!.Count == 0)
            yield return new(UiText.Get("Card.Displays.Title"), null, [], [UiText.Get("Card.Displays.Empty")], technical: count)
            {
                Glyph = "", Tone = CardTone.Data, Say = UiText.Get("Card.Displays.SayNone")
            };
        for (var i = 0; i < displays.Value.Count; i++)
        {
            var d = displays.Value[i];
            var facts = new List<FactLine>();
            if (d.SourceResolution.State != DataState.Available)
                facts.Add(FactLine.Unavailable("Field.Resolution", d.SourceResolution.State).WithGlossary("Resolution"));
            if (d.PathRefreshRate.State != DataState.Available)
                facts.Add(FactLine.Unavailable("Field.RefreshRate", d.PathRefreshRate.State).WithGlossary("RefreshRate"));
            facts.Add(FactLine.Of("Field.MonitorName", d.Name).WithGlossary("MonitorName"));
            facts.Add((d.OutputTechnology.State == DataState.Available
                ? FactLine.Text("Field.OutputTechnology", DisplayFormat.OutputTechnology(d.OutputTechnology.Value!))
                : FactLine.Unavailable("Field.OutputTechnology", d.OutputTechnology.State)).WithGlossary("OutputTechnology"));
            facts.Add(Association("Field.SourceAdapter", d.SourceAdapter, gpus).WithGlossary("SourceAdapter"));
            facts.Add(Association("Field.TargetAdapter", d.TargetAdapter, gpus).WithGlossary("TargetAdapter"));
            yield return new(DisplayTitle(i), UiText.Format("Card.ReportLabel", d.Id), facts, [], technical: DisplayTechnical(d))
            {
                Glyph = "", Tone = CardTone.Data, Say = DisplayMode(d),
                TitleTip = ExplanationCatalog.Glossary("Resolution") + "\n\n" + ExplanationCatalog.Glossary("RefreshRate")
            };
        }
    }

    // Resolution and refresh rate as people say them, for example "2560 × 1600 at 165 Hz".
    private static string DisplayMode(DisplayFacts d) => (d.SourceResolution.State, d.PathRefreshRate.State) switch
    {
        (DataState.Available, DataState.Available) => UiText.Format("Display.Mode",
            DisplayFormat.ShortResolution(d.SourceResolution.Value!), DisplayFormat.Rate(d.PathRefreshRate.Value!)),
        (DataState.Available, _) => DisplayFormat.ShortResolution(d.SourceResolution.Value!),
        (_, DataState.Available) => DisplayFormat.Rate(d.PathRefreshRate.Value!),
        _ => UiText.Get("Display.ModeUnavailable")
    };

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

    // One card per adapter: provider and version as the sentence, then date and PCI vendor ID.
    private static IEnumerable<CardViewModel> DriverCardsFor(Observation<IReadOnlyList<GpuFacts>> gpus)
    {
        if (gpus.State != DataState.Available) yield break;
        foreach (var gpu in gpus.Value!)
        {
            var driver = gpu.Driver;
            var facts = new List<FactLine>();
            if (driver.Provider.State != DataState.Available) facts.Add(FactLine.Of("Field.DriverProvider", driver.Provider));
            if (driver.Version.State != DataState.Available) facts.Add(FactLine.Of("Field.DriverVersion", driver.Version).WithGlossary("DriverVersion"));
            facts.Add(FactLine.Of("Field.DriverDate", driver.Date).WithGlossary("DriverDate"));
            facts.Add(FactLine.Of("Field.PciVendorId", gpu.PciVendorId).WithGlossary("PciVendorId"));
            var say = (driver.Provider.State, driver.Version.State) switch
            {
                (DataState.Available, DataState.Available) => UiText.Format("Driver.Say", driver.Provider.Value!, driver.Version.Value!),
                (_, DataState.Available) => driver.Version.Value!,
                _ => UiText.Get("Driver.SayUnavailable")
            };
            yield return new(UiText.Format("Card.Driver.Title", gpu.Id), gpu.Name.State == DataState.Available ? gpu.Name.Value : null,
                facts, [], technical:
            [
                TechnicalLine.Of("Field.AdapterName", gpu.Name), TechnicalLine.Of("Field.PciVendorId", gpu.PciVendorId),
                TechnicalLine.Of("Field.PciDeviceId", gpu.PciDeviceId), TechnicalLine.Of("Field.Classification", gpu.Classification),
                TechnicalLine.Of("Field.DriverProvider", driver.Provider), TechnicalLine.Of("Field.DriverVersion", driver.Version),
                TechnicalLine.Of("Field.DriverDate", driver.Date)
            ])
            {
                Glyph = "", Tone = CardTone.Accent, Say = say, TitleTip = ExplanationCatalog.Glossary("Driver")
            };
        }
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
                    CollectorStatus.Unsupported => "",
                    _ => ""
                }) { Help = ExplanationCatalog.Collector(r.Status).Meaning, IsData = false }).ToArray(), [],
            technical: runs.Select(r => new TechnicalLine(ExplanationCatalog.Source(r.Source),
                UiText.Format("Technical.Run", r.Status, r.Reason, Number(r.Attempts), r.QueryMode),
                r.Issues.Count == 0 ? UiText.Get("Technical.NoIssues")
                    : string.Join("; ", r.Issues.Select(i => i.NativeErrorCode is { } code
                        ? UiText.Format("Technical.IssueCode", i.Operation, i.Reason, code.ToString(CultureInfo.InvariantCulture))
                        : UiText.Format("Technical.Issue", i.Operation, i.Reason))))).ToArray());
}
