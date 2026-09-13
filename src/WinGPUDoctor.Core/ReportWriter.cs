using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

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
        b.AppendLine("## Active display paths").AppendLine();
        b.AppendLine("These facts describe Windows display paths, not application rendering, utilization, or power state.").AppendLine();
        if (r.Facts.Displays.State != DataState.Available) b.AppendLine($"Display topology: {Format(r.Facts.Displays)}").AppendLine();
        else
        {
            b.AppendLine($"Active paths reported: {r.Facts.Displays.Value!.Count}").AppendLine();
            string Adapter(Observation<AdapterMatch> field)
            {
                if (field.State != DataState.Available) return Format(field);
                var match = field.Value!;
                var gpu = r.Facts.Gpus.Value?.FirstOrDefault(g => g.Id == match.GpuId);
                return $"{match.GpuId}" + (gpu is null ? "" : $" ({Format(gpu.Name)})") + $"; evidence: {match.Evidence}; confidence: {match.Confidence}";
            }
            static string Rate(Observation<RationalRate> field) => field.State == DataState.Available
                ? $"{((double)field.Value!.Numerator / field.Value.Denominator).ToString("0.###", CultureInfo.InvariantCulture)} Hz ({field.Value.Numerator}/{field.Value.Denominator})"
                : Format(field);
            foreach (var d in r.Facts.Displays.Value)
            {
                b.AppendLine($"### {d.Id}").AppendLine();
                Field("Monitor friendly name", d.Name);
                b.AppendLine($"- Source / target: {d.SourceId} / {d.TargetId}");
                Field("Source GDI name (not an internal-panel label)", d.SourceGdiName);
                b.AppendLine($"- Display source adapter: {d.SourceAdapterId}; {Adapter(d.SourceAdapter)}");
                b.AppendLine($"- Display target adapter: {d.TargetAdapterId}; {Adapter(d.TargetAdapter)}");
                Field("Output technology", d.OutputTechnology);
                b.AppendLine($"- Source resolution: {(d.SourceResolution.State == DataState.Available ? $"{d.SourceResolution.Value!.WidthPixels} × {d.SourceResolution.Value.HeightPixels} pixels" : Format(d.SourceResolution))}");
                b.AppendLine($"- Path refresh{(d.QueryMode == DisplayQueryMode.VirtualModeAndRefreshAware ? " (virtual-aware)" : " (legacy query semantics)")}: {Rate(d.PathRefreshRate)}");
                b.AppendLine($"- Target signal vertical-sync rate: {Rate(d.SignalRefreshRate)}");
                Field("Scan-line ordering", d.ScanLineOrdering); Field("Target rotation", d.Rotation);
                b.AppendLine($"- Path active: {d.PathActive}; target available: {d.TargetAvailable}");
                b.AppendLine($"- Refresh boost flag: {(d.RefreshRateBoost.State == DataState.Available ? d.RefreshRateBoost.Value!.Enabled.ToString() : Format(d.RefreshRateBoost))}");
                Field("Reported clone group (when source mode is absent)", d.CloneGroupId);
                b.AppendLine($"- Query mode: {d.QueryMode}").AppendLine();
            }
        }
        b.AppendLine("## Interpreted findings").AppendLine();
        if (r.Findings.Count == 0) b.AppendLine("No rules produced a finding; this is not a health verdict.");
        foreach (var f in r.Findings) b.AppendLine($"- **{f.Id}** ({f.Severity}): {f.Message} Evidence: {string.Join(", ", f.Evidence)}");
        b.AppendLine().AppendLine("## Warnings").AppendLine();
        foreach (var w in r.Warnings) b.AppendLine($"- {w}: {WarningText(w)}");
        b.AppendLine().AppendLine("## Collection metadata").AppendLine();
        foreach (var c in r.Collection)
        {
            b.AppendLine($"- {c.Source}: {c.Status} ({c.Reason}); attempts: {c.Attempts}; query mode: {c.QueryMode}");
            foreach (var issue in c.Issues) b.AppendLine($"  - {issue.Operation}: {issue.Reason}" + (issue.NativeErrorCode is { } code ? $"; native error: {code}" : ""));
        }
        b.AppendLine().AppendLine($"Privacy policy: {r.Privacy.PolicyVersion}; redacted fields: {r.Privacy.RedactedFields}.");
        return b.ToString().Replace("\r\n", "\n");
    }

    public static string WarningText(WarningCode code) => code switch
    {
        WarningCode.InventoryOnly => "Inventory does not measure application GPU use, power state, or prove hybrid mode.",
        WarningCode.ProviderReportedValues => "Firmware and WMI values may be missing, stale, virtual, or inaccurate; driver dates do not establish driver freshness.",
        WarningCode.TopologyNotCollected => "Active display topology was not available; inspect its state and collection diagnostics.",
        WarningCode.TopologyIsNotRendering => "DisplayConfig describes active paths and configured timing, not application GPU selection, utilization, power state, or a confirmed graphics mode. Friendly names are queried after the path snapshot and may race with device changes.",
        WarningCode.ReviewBeforeSharing => "Review before sharing. Model and device descriptions can be distinctive or customized; automatic filtering cannot guarantee anonymity.",
        WarningCode.CollectionIncomplete => "Some requested data could not be collected; inspect field states and collector results.",
        WarningCode.ValuesRedacted => "Suspicious text was removed by the privacy filter.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}
