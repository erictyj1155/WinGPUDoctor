using System.Globalization;
using System.Resources;

namespace WinGPUDoctor.Desktop;

// All UI copy lives in Resources/Strings.resx, so a translation can follow as a satellite assembly.
public static class UiText
{
    private static readonly ResourceManager Resources =
        new("WinGPUDoctor.Desktop.Resources.Strings", typeof(UiText).Assembly);

    public static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? throw new KeyNotFoundException("Missing UI text.");

    public static string? Find(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture);

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    // Neutral-language keys, used by completeness and copy-review tests.
    public static IReadOnlyDictionary<string, string> NeutralEntries()
    {
        var set = Resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)
            ?? throw new MissingManifestResourceException("UI text resources are missing.");
        return set.Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!, StringComparer.Ordinal);
    }
}
