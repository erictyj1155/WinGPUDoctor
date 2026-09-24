using System.Globalization;
using System.Text.RegularExpressions;

namespace WinGPUDoctor.Core;

public static class PrivacyPolicy
{
    private static readonly Regex Sensitive = new(
        @"[\\/:@=<>\[\]`|]|\b(?:serial|s/n|hostname|username|password|passwd|secret|token|bearer|api[ _-]?key)\b|\b(?:\d{1,3}\.){3}\d{1,3}\b|\b(?:[0-9a-f]{2}[-:]){5}[0-9a-f]{2}\b|\b(?:gh[pousr]_|github_pat_|sk-)[a-z0-9_\-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Whole-field removal avoids keeping fragments of suspicious native/provider text.
    public static string? SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length > 160 || value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format)) return null;
        try { return Sensitive.IsMatch(value) ? null : value.Trim(); }
        catch (RegexMatchTimeoutException) { return null; }
    }

    public static ShareableReport Prepare(CollectionSnapshot snapshot, DateOnly collectedOnUtc)
    {
        var removed = 0;
        Observation<string> Clean(Observation<string> field, string? pattern = null)
        {
            if (field.State != DataState.Available) return field;
            var text = pattern is null ? SafeText(field.Value) :
                field.Value!.Length <= 64 && Regex.IsMatch(field.Value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) ? field.Value : null;
            if (text is not null) return Observation<string>.Known(text, field.Source);
            removed++;
            return Observation<string>.Absent(DataState.Redacted, field.Source, ReasonCode.SensitiveValue);
        }
        var s = snapshot.Facts.System;
        const string version = @"\A\d{1,10}(?:\.\d{1,10}){1,3}\z";
        const string hex = @"\A[0-9A-F]{4}\z";
        var system = new SystemFacts(Clean(s.WindowsVersion, version), Clean(s.WindowsBuild, @"\A\d{1,10}\z"), Clean(s.Manufacturer), Clean(s.Model));
        var gpus = snapshot.Facts.Gpus;
        if (gpus.State == DataState.Available)
            gpus = Observation<IReadOnlyList<GpuFacts>>.Known(gpus.Value!.Select((g, i) => new GpuFacts(
                $"gpu-{i + 1}", Clean(g.Name), Clean(g.PciVendorId, hex), Clean(g.PciDeviceId, hex),
                Observation<string>.Absent(DataState.Unsupported, DataSource.NotCollected, ReasonCode.NotImplemented),
                new(Clean(g.Driver.Provider), Clean(g.Driver.Version, version), Clean(g.Driver.Date, @"\A\d{4}-\d{2}-\d{2}\z")))).ToArray(), gpus.Source);
        var displays = TopologyPrivacy.Project(snapshot.Facts, Clean);
        var facts = new CollectedFacts(system, gpus, displays);
        var warnings = new List<WarningCode> { WarningCode.InventoryOnly, WarningCode.ProviderReportedValues,
            displays.State == DataState.Available ? WarningCode.TopologyIsNotRendering : WarningCode.TopologyNotCollected, WarningCode.ReviewBeforeSharing };
        if (snapshot.Collection.Any(c => c.IsIncomplete())) warnings.Add(WarningCode.CollectionIncomplete);
        if (removed > 0) warnings.Add(WarningCode.ValuesRedacted);
        return new(new("0.2.0", ToolIdentity.Version, collectedOnUtc, facts, DiagnosticRules.Evaluate(facts),
            warnings.ToArray(), snapshot.Collection.Select(c => c with { Issues = c.Issues.ToArray() }).ToArray(), new("0.2", removed)));
    }
}
