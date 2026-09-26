using System.Globalization;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Desktop.ViewModels;

// One labelled fact. Unavailable states always carry an icon and text, never color alone.
public sealed record FactLine(string Label, string Value, bool IsAvailable, string StateGlyph)
{
    internal static FactLine Of(string labelKey, Observation<string> field) => field.State == DataState.Available
        ? Text(labelKey, field.Value!)
        : Unavailable(labelKey, field.State);

    internal static FactLine Text(string labelKey, string value) => new(UiText.Get(labelKey), value, true, "");

    internal static FactLine Unavailable(string labelKey, DataState state) =>
        new(UiText.Get(labelKey), UiText.Get("State." + state), false, Glyph(state));

    // Segoe Fluent Icons / MDL2 Assets: Lock, Warning, Info.
    internal static string Glyph(DataState state) => state switch
    {
        DataState.Available => "",
        DataState.Redacted => "",
        DataState.Failed => "",
        _ => ""
    };
}

// Catalog copy for a finding or warning; Context names the display path a finding is about.
public sealed record Explanation(string? Context, string Title, string Meaning, string? NotMeaning, string? NextStep)
{
    public bool HasContext => Context is not null;
    public bool HasNotMeaning => NotMeaning is not null;
    public bool HasNextStep => NextStep is not null;

    internal static Explanation From(CatalogEntry entry, string? context = null) =>
        new(context, entry.Title, entry.Meaning, entry.NotMeaning, entry.NextStep);
}

public sealed record HelpLine(string Term, string Meaning)
{
    internal static HelpLine Glossary(string fieldKey, string term) => new(UiText.Get(fieldKey), ExplanationCatalog.Glossary(term));
}

// Exact report value plus state, source and reason, shown behind "Show technical details".
public sealed record TechnicalLine(string Label, string Value, string Provenance)
{
    internal static TechnicalLine Of<T>(string labelKey, Observation<T> field, Func<T, string>? format = null) where T : class =>
        new(UiText.Get(labelKey),
            field.State == DataState.Available ? format?.Invoke(field.Value!) ?? field.Value!.ToString()! : UiText.Get("Technical.NoValue"),
            Describe(field.State, field.Source, field.Reason));

    internal static TechnicalLine Plain(string labelKey, string value) => new(UiText.Get(labelKey), value, "");

    internal static string Describe(DataState state, DataSource source, ReasonCode reason)
    {
        var stateTitle = ExplanationCatalog.State(state).Title;
        var sourceName = ExplanationCatalog.Source(source);
        if (reason == ReasonCode.None) return UiText.Format("Technical.Provenance", stateTitle, sourceName);
        var why = ExplanationCatalog.Reason(reason);
        return UiText.Format("Technical.ProvenanceReason", stateTitle, sourceName, why.Title, why.Meaning);
    }
}

public sealed class CardViewModel(string title, string? subtitle, IReadOnlyList<FactLine> facts, IReadOnlyList<string> notes,
    IReadOnlyList<Explanation>? explanations = null, IReadOnlyList<HelpLine>? help = null, IReadOnlyList<TechnicalLine>? technical = null)
{
    public string Title { get; } = title;
    public string? Subtitle { get; } = subtitle;
    public IReadOnlyList<FactLine> Facts { get; } = facts;
    public IReadOnlyList<string> Notes { get; } = notes;
    public IReadOnlyList<Explanation> Explanations { get; } = explanations ?? [];
    public IReadOnlyList<HelpLine> Help { get; } = help ?? [];
    public IReadOnlyList<TechnicalLine> Technical { get; } = technical ?? [];
    public bool HasSubtitle => Subtitle is not null;
    public bool HasDetails => Help.Count > 0 || Technical.Count > 0;
    public bool HasTechnical => Technical.Count > 0;
}

internal static class DisplayFormat
{
    internal static string Rate(RationalRate rate) => UiText.Format("Unit.Hertz",
        Math.Round((double)rate.Numerator / rate.Denominator, 2).ToString("0.##", CultureInfo.CurrentCulture));

    // Technical details keep the exact rational value next to the rounded one.
    internal static string ExactRate(RationalRate rate) => UiText.Format("Unit.ExactRate",
        rate.Numerator.ToString(CultureInfo.InvariantCulture), rate.Denominator.ToString(CultureInfo.InvariantCulture), Rate(rate));

    internal static string Resolution(PixelSize size) => UiText.Format("Unit.Resolution",
        size.WidthPixels.ToString(CultureInfo.CurrentCulture), size.HeightPixels.ToString(CultureInfo.CurrentCulture));

    // Output technology is Windows' reported value; unknown tokens are shown as reported.
    internal static string OutputTechnology(string token) => UiText.Find("OutputTechnology." + token) ?? token;

    internal static string YesNo(bool value) => UiText.Get(value ? "Value.Yes" : "Value.No");
}
