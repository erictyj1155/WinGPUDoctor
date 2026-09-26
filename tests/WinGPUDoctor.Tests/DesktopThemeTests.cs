using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WinGPUDoctor.Desktop.Views;
using Xunit;

namespace WinGPUDoctor.Tests;

public class DesktopThemeTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static string Desktop => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,
        "../../../../..", "src", "WinGPUDoctor.Desktop"));

    private static IEnumerable<string> Sources(string pattern) => Directory.EnumerateFiles(Desktop, pattern, SearchOption.AllDirectories)
        .Where(f => !Regex.IsMatch(f, @"[\\/](bin|obj)[\\/]"));

    private static Dictionary<string, string> Palette(string kind) =>
        XDocument.Load(System.IO.Path.Combine(Desktop, "Themes", $"Palette.{kind}.xaml")).Root!.Elements()
            .ToDictionary(e => (string)e.Attribute(X + "Key")!, e => (string)e.Attribute("Color")!, StringComparer.Ordinal);

    // #RRGGBB or #AARRGGBB as (alpha, red, green, blue) in 0..1.
    private static (double A, double R, double G, double B) Parse(string hex)
    {
        var digits = hex.TrimStart('#');
        if (digits.Length == 6) digits = "FF" + digits;
        Assert.Equal(8, digits.Length);
        double Part(int i) => int.Parse(digits.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return (Part(0), Part(1), Part(2), Part(3));
    }

    // A translucent tint as it appears over an opaque surface.
    private static (double R, double G, double B) Over(string top, string bottom)
    {
        var t = Parse(top);
        var b = Parse(bottom);
        Assert.Equal(1.0, b.A);
        return (t.R * t.A + b.R * (1 - t.A), t.G * t.A + b.G * (1 - t.A), t.B * t.A + b.B * (1 - t.A));
    }

    private static double Luminance((double R, double G, double B) c)
    {
        static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }

    private static double Contrast((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    [Fact]
    public void AllPalettesDefineTheSameKeys()
    {
        var dark = Palette("Dark").Keys.Order().ToArray();
        Assert.NotEmpty(dark);
        Assert.Equal(dark, Palette("Light").Keys.Order());
        Assert.Equal(dark, Palette("HighContrast").Keys.Order());
        // High contrast uses the user's system colors instead of fixed ones.
        Assert.All(Palette("HighContrast").Values, v => Assert.StartsWith("{x:Static SystemColors.", v));
    }

    [Fact]
    public void DarkPaletteUsesTheChosenDirectionColors()
    {
        var dark = Palette("Dark");
        Assert.Equal("#171C22", dark["Wgd.Background"]);
        Assert.Equal("#1E252D", dark["Wgd.Panel"]);
        Assert.Equal("#2E3844", dark["Wgd.Line"]);
        Assert.Equal("#E4EAF0", dark["Wgd.Text"]);
        Assert.Equal("#8795A3", dark["Wgd.Muted"]);
        Assert.Equal("#F2A541", dark["Wgd.Accent"]);
        Assert.Equal("#7CC4E0", dark["Wgd.Data"]);
    }

    [Fact]
    public void LightPaletteDarkensAmberAndCyan()
    {
        var (dark, light) = (Palette("Dark"), Palette("Light"));
        foreach (var key in new[] { "Wgd.Accent", "Wgd.Data" })
            Assert.True(Luminance(Over(light[key], light["Wgd.Panel"])) < Luminance(Over(dark[key], dark["Wgd.Panel"])) / 2, key);
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void TextAndControlsMeetWcagAaContrast(string kind)
    {
        var p = Palette(kind);
        var surfaces = new Dictionary<string, (double R, double G, double B)>
        {
            ["background"] = Over(p["Wgd.Background"], p["Wgd.Background"]),
            ["panel"] = Over(p["Wgd.Panel"], p["Wgd.Panel"]),
            ["note on background"] = Over(p["Wgd.AccentTint"], p["Wgd.Background"]),
            ["note on panel"] = Over(p["Wgd.AccentTint"], p["Wgd.Panel"])
        };
        // Body, secondary, accent and data text on every surface: AA normal text (4.5:1).
        foreach (var text in new[] { "Wgd.Text", "Wgd.Muted", "Wgd.Accent", "Wgd.Data" })
            foreach (var (surface, color) in surfaces)
                Assert.True(Contrast(Over(p[text], p["Wgd.Panel"]), color) >= 4.5, $"{kind}: {text} on {surface}");
        // Hovered buttons and (i) tips switch their text to Wgd.Text.
        foreach (var surface in new[] { "Wgd.Background", "Wgd.Panel" })
            Assert.True(Contrast(Over(p["Wgd.Text"], p[surface]), Over(p["Wgd.Hover"], p[surface])) >= 4.5, $"{kind}: text on hover");
        (string Text, string Surface)[] pairs =
            [("Wgd.OnAccent", "Wgd.Accent"), ("Wgd.OnAccent", "Wgd.AccentHover"), ("Wgd.PreviewText", "Wgd.Preview"), ("Wgd.TipText", "Wgd.Tip")];
        foreach (var (text, surface) in pairs)
            Assert.True(Contrast(Over(p[text], p[surface]), Over(p[surface], p[surface])) >= 4.5, $"{kind}: {text} on {surface}");
        // Focus rings and badge icons: AA non-text contrast (3:1).
        Assert.True(Contrast(Over(p["Wgd.Focus"], p["Wgd.Background"]), surfaces["background"]) >= 3, $"{kind}: focus");
        Assert.True(Contrast(Over(p["Wgd.Focus"], p["Wgd.Panel"]), surfaces["panel"]) >= 3, $"{kind}: focus on panel");
        Assert.True(Contrast(Over(p["Wgd.Data"], p["Wgd.Panel"]), Over(p["Wgd.DataTint"], p["Wgd.Panel"])) >= 3, $"{kind}: data badge");
        Assert.True(Contrast(Over(p["Wgd.Accent"], p["Wgd.Panel"]), Over(p["Wgd.AccentTint"], p["Wgd.Panel"])) >= 3, $"{kind}: accent badge");
    }

    [Fact]
    public void ColorsAreDefinedOnlyInThePalettes()
    {
        var xaml = Sources("*.xaml").Where(f => !System.IO.Path.GetFileName(f).StartsWith("Palette.", StringComparison.Ordinal)).ToArray();
        Assert.Contains(xaml, f => f.EndsWith("MainWindow.xaml", StringComparison.Ordinal));
        foreach (var file in xaml)
            Assert.False(Regex.IsMatch(File.ReadAllText(file), @"=""#[0-9A-Fa-f]{3,8}""|>\s*#[0-9A-Fa-f]{3,8}\s*<"), file);
        foreach (var file in Sources("*.cs"))
            Assert.False(Regex.IsMatch(File.ReadAllText(file), @"\b(Colors|Brushes|SystemColors)\.|Color\.FromRgb\("), file);
    }

    // Palette brushes are swapped at run time, so they are referenced only dynamically, and no style or
    // template key may reuse a palette key (a clash made StaticResource return a brush for a Style).
    [Fact]
    public void PaletteKeysAreSeparateAndReferencedOnlyDynamically()
    {
        var palette = Palette("Dark").Keys.ToHashSet(StringComparer.Ordinal);
        var texts = Sources("*.xaml").Where(f => !System.IO.Path.GetFileName(f).StartsWith("Palette.", StringComparison.Ordinal))
            .Select(File.ReadAllText).ToArray();
        var defined = texts.SelectMany(t => Regex.Matches(t, @"x:Key=""([^""]+)""").Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("Wgd.PreviewBox", defined);
        foreach (var key in defined) Assert.DoesNotContain(key, palette);
        foreach (var text in texts)
        {
            foreach (Match m in Regex.Matches(text, @"\{StaticResource ([^}\s]+)\}"))
                Assert.Contains(m.Groups[1].Value, defined);
            foreach (Match m in Regex.Matches(text, @"\{DynamicResource ([^}\s]+)\}"))
                Assert.Contains(m.Groups[1].Value, palette);
        }
    }

    [Fact]
    public void OnlyBuiltInWindowsFontsAreUsedAndNoneIsBundled()
    {
        Assert.DoesNotContain(Sources("*.*"), f => Regex.IsMatch(f, @"\.(ttf|otf|ttc|woff2?)\z", RegexOptions.IgnoreCase));
        string[] allowed = ["Segoe UI Variable Text", "Segoe UI Variable Display", "Segoe UI", "Cascadia Mono", "Consolas",
            "Segoe Fluent Icons", "Segoe MDL2 Assets"];
        var families = Sources("*.xaml").SelectMany(f => Regex.Matches(File.ReadAllText(f),
                @"<FontFamily x:Key=""[^""]+"">([^<]+)</FontFamily>|FontFamily=""([^""{][^""]*)""")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)).ToArray();
        Assert.NotEmpty(families);
        Assert.All(families.SelectMany(f => f.Split(',')).Select(f => f.Trim()), family => Assert.Contains(family, allowed));
    }

    [Fact]
    public void PaletteFollowsHighContrastFirstThenTheWindowsAppMode()
    {
        Assert.Equal(PaletteKind.Dark, AppTheme.Resolve(highContrast: false, appsUseLightTheme: 0));
        Assert.Equal(PaletteKind.Light, AppTheme.Resolve(highContrast: false, appsUseLightTheme: 1));
        Assert.Equal(PaletteKind.Light, AppTheme.Resolve(highContrast: false, appsUseLightTheme: null)); // Windows default
        Assert.Equal(PaletteKind.HighContrast, AppTheme.Resolve(highContrast: true, appsUseLightTheme: 0));
        Assert.Equal(PaletteKind.HighContrast, AppTheme.Resolve(highContrast: true, appsUseLightTheme: 1));
    }
}
