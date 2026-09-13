namespace WinGPUDoctor.Core;

internal static class TopologyPrivacy
{
    internal static Observation<IReadOnlyList<DisplayFacts>> Project(CollectedFacts facts,
        Func<Observation<string>, string?, Observation<string>> clean)
    {
        var input = facts.Displays;
        if (input.State != DataState.Available) return input;
        var adapters = new Dictionary<string, string>(StringComparer.Ordinal);
        var sources = new Dictionary<(string, string), string>();
        var targets = new Dictionary<(string, string), string>();
        var clones = new Dictionary<(string, string), string>();
        static string Label<K>(Dictionary<K, string> labels, K key, string prefix) where K : notnull
        {
            if (!labels.TryGetValue(key, out var label)) { label = $"{prefix}-{labels.Count + 1}"; labels.Add(key, label); }
            return label;
        }
        Observation<AdapterMatch> Match(Observation<AdapterMatch> match)
        {
            if (match.State != DataState.Available) return match;
            var candidates = facts.Gpus.State == DataState.Available ? facts.Gpus.Value!.Select((gpu, i) => (gpu, i))
                .Where(g => g.gpu.Id == match.Value!.GpuId).ToArray() : [];
            if (candidates.Length != 1) return Observation<AdapterMatch>.Absent(DataState.Unknown, DataSource.SetupApiInstanceJoin,
                candidates.Length == 0 ? ReasonCode.UnmatchedAdapter : ReasonCode.AmbiguousAdapter);
            if (match.Value!.Evidence != AdapterMatchEvidence.ExactSetupApiInstanceId || match.Value.Confidence != AdapterMatchConfidence.Exact)
                return Observation<AdapterMatch>.Absent(DataState.Unknown, DataSource.SetupApiInstanceJoin, ReasonCode.InvalidValue);
            return Observation<AdapterMatch>.Known(new($"gpu-{candidates[0].i + 1}", AdapterMatchEvidence.ExactSetupApiInstanceId, AdapterMatchConfidence.Exact), DataSource.SetupApiInstanceJoin);
        }
        static Observation<T> Validate<T>(Observation<T> field, Func<T, bool> valid) where T : class =>
            field.State != DataState.Available || valid(field.Value!) ? field :
                Observation<T>.Absent(DataState.Unknown, field.Source, ReasonCode.InvalidValue);
        const string connector = @"\A(?:hd15|sVideo|compositeVideo|componentVideo|dvi|hdmi|lvds|dJpn|sdi|displayPortExternal|displayPortEmbedded|udiExternal|udiEmbedded|sdTvDongle|miracast|indirectWired|indirectVirtual|displayPortUsbTunnel|internal|other)\z";
        var output = input.Value!.Select((d, i) => new DisplayFacts($"display-{i + 1}",
            Label(sources, (d.SourceAdapterId, d.SourceId), "source"), Label(targets, (d.TargetAdapterId, d.TargetId), "target"),
            Label(adapters, d.SourceAdapterId, "adapter"), Label(adapters, d.TargetAdapterId, "adapter"), Match(d.SourceAdapter), Match(d.TargetAdapter),
            // The only path-like text permitted is the documented GDI display alias, strictly delimited.
            clean(d.SourceGdiName, @"\A\\\\\.\\DISPLAY[1-9][0-9]{0,5}\z"), clean(d.Name, null), clean(d.OutputTechnology, connector),
            Validate(d.SourceResolution, s => s.WidthPixels > 0 && s.HeightPixels > 0),
            Validate(d.PathRefreshRate, r => r.Numerator > 0 && r.Denominator > 0),
            Validate(d.SignalRefreshRate, r => r.Numerator > 0 && r.Denominator > 0),
            clean(d.Rotation, @"\A(?:identity|rotate90|rotate180|rotate270)\z"),
            clean(d.ScanLineOrdering, @"\A(?:progressive|interlacedUpperFirst|interlacedLowerFirst)\z"), d.PathActive, d.TargetAvailable,
            d.RefreshRateBoost, d.CloneGroupId.State == DataState.Available
                ? Observation<string>.Known(Label(clones, (d.SourceAdapterId, d.CloneGroupId.Value!), "clone"), DataSource.DisplayConfig) : d.CloneGroupId,
            d.QueryMode)).ToArray();
        return Observation<IReadOnlyList<DisplayFacts>>.Known(output, input.Source);
    }
}
