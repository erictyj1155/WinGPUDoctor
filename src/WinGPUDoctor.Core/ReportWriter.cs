using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinGPUDoctor.Core;

public static class ReportWriter
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
    public static string Json(ShareableReport report) => JsonSerializer.Serialize(report.Report, JsonOptions) + "\n";

    private static string Escape(string text)
    {
        var result = new StringBuilder();
        foreach (var c in text)
        {
            if ("\\`*_{}[]<>()#+-.!|".Contains(c)) result.Append('\\');
            result.Append(c);
        }
        return result.ToString();
    }
    private static string Format<T>(Observation<T> field) where T : class => field.State == DataState.Available
        ? Escape(field.Value!.ToString()!) : $"{field.State} ({field.Reason})";

    public static string Markdown(ShareableReport shareable)
    {
        var r = shareable.Report;
        var b = new StringBuilder();
        b.AppendLine("# WinGPUDoctor diagnostic report").AppendLine();
        b.AppendLine($"Schema: {r.SchemaVersion} | Tool: {r.ToolVersion} | UTC date: {r.CollectedOnUtc:yyyy-MM-dd}").AppendLine();
        b.AppendLine("## Collected facts").AppendLine();
        void Field(string name, Observation<string> field) => b.AppendLine($"- {name}: {Format(field)} — source: {field.Source}");
        Field("Windows version", r.Facts.System.WindowsVersion); Field("Windows build", r.Facts.System.WindowsBuild);
        Field("Manufacturer", r.Facts.System.Manufacturer); Field("Model", r.Facts.System.Model);
        b.AppendLine();
        if (r.Facts.Gpus.State != DataState.Available) b.AppendLine($"GPU inventory: {Format(r.Facts.Gpus)}");
        else
        {
            b.AppendLine($"Reported video controllers: {r.Facts.Gpus.Value!.Count}").AppendLine();
            foreach (var g in r.Facts.Gpus.Value)
            {
                b.AppendLine($"### {g.Id}").AppendLine();
                Field("Name", g.Name); Field("PCI vendor ID", g.PciVendorId); Field("PCI device ID", g.PciDeviceId);
                Field("Classification", g.Classification); Field("Driver provider", g.Driver.Provider);
                Field("Driver version", g.Driver.Version); Field("Driver date (provider-reported, not installation date)", g.Driver.Date);
                b.AppendLine();
            }
        }
        b.AppendLine($"Displays and topology: {Format(r.Facts.Displays)}").AppendLine();
        b.AppendLine("## Interpreted findings").AppendLine();
        if (r.Findings.Count == 0) b.AppendLine("No rules produced a finding; this is not a health verdict.");
        foreach (var f in r.Findings) b.AppendLine($"- **{f.Id}** ({f.Severity}): {f.Message} Evidence: {string.Join(", ", f.Evidence)}");
        b.AppendLine().AppendLine("## Warnings").AppendLine();
        foreach (var w in r.Warnings) b.AppendLine($"- {w}: {WarningText(w)}");
        b.AppendLine().AppendLine("## Collection metadata").AppendLine();
        foreach (var c in r.Collection) b.AppendLine($"- {c.Source}: {c.Status} ({c.Reason})");
        b.AppendLine().AppendLine($"Privacy policy: {r.Privacy.PolicyVersion}; redacted fields: {r.Privacy.RedactedFields}.");
        return b.ToString().Replace("\r\n", "\n");
    }

    public static string WarningText(WarningCode code) => code switch
    {
        WarningCode.InventoryOnly => "Inventory does not measure application GPU use, power state, or prove hybrid mode.",
        WarningCode.ProviderReportedValues => "Firmware and WMI values may be missing, stale, virtual, or inaccurate; driver dates do not establish driver freshness.",
        WarningCode.TopologyNotCollected => "Display enumeration, modes, and routing are deferred in this proof of concept.",
        WarningCode.ReviewBeforeSharing => "Review before sharing. Model and device descriptions can be distinctive or customized; automatic filtering cannot guarantee anonymity.",
        WarningCode.CollectionIncomplete => "Some requested data could not be collected; inspect field states and collector results.",
        WarningCode.ValuesRedacted => "Suspicious text was removed by the privacy filter.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}
