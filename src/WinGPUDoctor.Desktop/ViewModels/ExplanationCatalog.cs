using WinGPUDoctor.Core;

namespace WinGPUDoctor.Desktop.ViewModels;

public sealed record CatalogEntry(string Title, string Meaning, string? NotMeaning, string? NextStep);

// Plain-language copy keyed by stable finding IDs and enum values. The text lives in
// Resources/Strings.resx; tests require an entry for every ID and enum value.
public static class ExplanationCatalog
{
    // The finding IDs produced by Core's DiagnosticRules (see docs/REPORT-SCHEMA.md).
    public static IReadOnlyList<string> FindingIds { get; } =
    [
        "inventory.multiple-adapters", "inventory.empty", "topology.no-active-paths",
        "topology.endpoint-adapter-association", "topology.correlation-unresolved", "topology.active-path-target-unavailable"
    ];

    // Report terms explained in card details; only facts that the report actually contains.
    public static IReadOnlyList<string> GlossaryTerms { get; } =
        ["PciVendorId", "DriverVersion", "DriverDate", "MonitorName", "Resolution", "RefreshRate", "OutputTechnology",
            "SourceAdapter", "TargetAdapter"];

    public static CatalogEntry? Finding(string id) => FindingIds.Contains(id, StringComparer.Ordinal) ? Entry("Finding." + id) : null;
    public static CatalogEntry Warning(WarningCode code) => Entry("Warning." + Defined(code));
    public static CatalogEntry State(DataState state) => Entry("State." + Defined(state));
    public static CatalogEntry Reason(ReasonCode reason) => Entry("Reason." + Defined(reason));
    public static CatalogEntry Collector(CollectorStatus status) => Entry("Collector." + Defined(status));
    public static string Source(DataSource source) => UiText.Get("Source." + Defined(source));
    public static string Glossary(string term) => UiText.Get("Glossary." + term);

    private static T Defined<T>(T value) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));

    private static CatalogEntry Entry(string key) => new(UiText.Get(key), UiText.Get(key + ".Meaning"),
        UiText.Find(key + ".NotMeaning"), UiText.Find(key + ".NextStep"));
}
