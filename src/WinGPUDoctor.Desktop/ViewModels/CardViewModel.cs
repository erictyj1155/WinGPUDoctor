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

public sealed class CardViewModel(string title, string? subtitle, IReadOnlyList<FactLine> facts, IReadOnlyList<string> notes)
{
    public string Title { get; } = title;
    public string? Subtitle { get; } = subtitle;
    public IReadOnlyList<FactLine> Facts { get; } = facts;
    public IReadOnlyList<string> Notes { get; } = notes;
    public bool HasSubtitle => Subtitle is not null;
}

internal static class DisplayFormat
{
    internal static string Rate(RationalRate rate) => UiText.Format("Unit.Hertz",
        Math.Round((double)rate.Numerator / rate.Denominator, 2).ToString("0.##", CultureInfo.CurrentCulture));

    internal static string Resolution(PixelSize size) => UiText.Format("Unit.Resolution",
        size.WidthPixels.ToString(CultureInfo.CurrentCulture), size.HeightPixels.ToString(CultureInfo.CurrentCulture));

    // Output technology is Windows' reported value; unknown tokens are shown as reported.
    internal static string OutputTechnology(string token) => UiText.Find("OutputTechnology." + token) ?? token;
}
