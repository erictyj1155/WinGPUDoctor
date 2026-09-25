using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using WinGPUDoctor.Core;
using Xunit;

namespace WinGPUDoctor.Tests;

public class M5FindingTests
{
    private static readonly DateOnly Day = new(2026, 9, 23);
    private static CollectionSnapshot Baseline() => TopologyTests.WithTopology(TopologyTests.Collect(TopologyTests.Single()));
    private static DisplayFacts Path() => Baseline().Facts.Displays.Value![0];
    private static Observation<AdapterMatch> Exact(string gpuId) => Observation<AdapterMatch>.Known(
        new(gpuId, AdapterMatchEvidence.ExactSetupApiInstanceId, AdapterMatchConfidence.Exact), DataSource.SetupApiInstanceJoin);
    private static Observation<AdapterMatch> Absent(DataState state, ReasonCode reason, DataSource source = DataSource.SetupApiInstanceJoin) =>
        Observation<AdapterMatch>.Absent(state, source, reason);
    private static CollectionSnapshot WithPaths(params DisplayFacts[] paths)
    {
        var baseline = Baseline();
        return baseline with { Facts = baseline.Facts with {
            Displays = Observation<IReadOnlyList<DisplayFacts>>.Known(paths, DataSource.DisplayConfig) } };
    }
    private static CollectionSnapshot WithTopologyState(DataState state, ReasonCode reason, DataSource source = DataSource.DisplayConfig)
    {
        var baseline = Baseline();
        return baseline with { Facts = baseline.Facts with {
            Displays = Observation<IReadOnlyList<DisplayFacts>>.Absent(state, source, reason) } };
    }
    private static ShareableReport Prepare(CollectionSnapshot snapshot) => PrivacyPolicy.Prepare(snapshot, Day);
    private static DiagnosticReport Read(ShareableReport report) =>
        JsonSerializer.Deserialize<DiagnosticReport>(ReportWriter.Json(report), ReportWriter.JsonOptions)!;
    private static DiagnosticFinding[] Topology(DiagnosticReport report) => report.Findings
        .Where(f => f.Id.StartsWith("topology.", StringComparison.Ordinal)).ToArray();

    [Fact]
    public void ExactAssociationsKeepBothEndpointsDistinctOrRepeatTheSameProjectedLabel()
    {
        var same = Assert.Single(Topology(Read(Prepare(WithPaths(Path())))));
        Assert.Equal("topology.endpoint-adapter-association", same.Id);
        Assert.Contains("source endpoint is exactly associated with gpu-2", same.Message);
        Assert.Contains("target endpoint is exactly associated with gpu-2", same.Message);
        var different = Assert.Single(Topology(Read(Prepare(WithPaths(Path() with { TargetAdapter = Exact("gpu-1") })))));
        Assert.Contains("source endpoint is exactly associated with gpu-2", different.Message);
        Assert.Contains("target endpoint is exactly associated with gpu-1", different.Message);
        Assert.Equal("information", same.Severity);
        Assert.Equal("information", different.Severity);
        Assert.DoesNotContain("rendering device", different.Message);
    }

    [Theory]
    [InlineData(DataState.Unknown, ReasonCode.UnmatchedAdapter)]
    [InlineData(DataState.Unknown, ReasonCode.AmbiguousAdapter)]
    [InlineData(DataState.Unsupported, ReasonCode.NotSupported)]
    [InlineData(DataState.Failed, ReasonCode.QueryFailed)]
    [InlineData(DataState.Redacted, ReasonCode.SensitiveValue)]
    [InlineData(DataState.Unsupported, ReasonCode.NotImplemented)]
    public void UnavailableTargetPreservesItsStateSourceReasonAndExactSource(DataState state, ReasonCode reason)
    {
        var source = reason == ReasonCode.NotImplemented ? DataSource.NotCollected : DataSource.SetupApiInstanceJoin;
        var display = Path() with { TargetAdapter = Absent(state, reason, source) };
        var finding = Assert.Single(Topology(Read(Prepare(WithPaths(display)))));
        Assert.Equal("topology.correlation-unresolved", finding.Id);
        Assert.Contains("source endpoint is exactly associated with gpu-2", finding.Message);
        Assert.Contains($"target endpoint association was not established in this report (state: {Token(state)}; source: {Token(source)}; reason: {Token(reason)})", finding.Message);
        Assert.DoesNotContain("no matching GPU exists", finding.Message);
    }

    [Fact]
    public void SourceOnlyAndBothUnresolvedRetainIndependentEndpointEvidence()
    {
        var source = Absent(DataState.Unknown, ReasonCode.UnmatchedAdapter);
        var target = Absent(DataState.Unknown, ReasonCode.AmbiguousAdapter);
        var sourceOnly = Assert.Single(Topology(Read(Prepare(WithPaths(Path() with { SourceAdapter = source })))));
        Assert.Contains("source endpoint association was not established", sourceOnly.Message);
        Assert.Contains("target endpoint is exactly associated with gpu-2", sourceOnly.Message);
        var both = Assert.Single(Topology(Read(Prepare(WithPaths(Path() with { SourceAdapter = source, TargetAdapter = target })))));
        Assert.Contains("source endpoint association was not established", both.Message);
        Assert.Contains("reason: unmatchedAdapter", both.Message);
        Assert.Contains("target endpoint association was not established", both.Message);
        Assert.Contains("reason: ambiguousAdapter", both.Message);
    }

    [Fact]
    public void TargetUnavailableCoexistsWithEitherAssociationOutcomeAndDoesNotChangePartialStatus()
    {
        var partial = TopologyTests.WithTopology(TopologyTests.Collect(TargetUnavailableFake()));
        var report = Read(Prepare(partial));
        Assert.Equal(["topology.endpoint-adapter-association", "topology.active-path-target-unavailable"],
            Topology(report).Select(f => f.Id));
        Assert.Contains(WarningCode.CollectionIncomplete, report.Warnings);
        Assert.Equal(CollectorStatus.Partial, report.Collection.Single(run => run.Source == DataSource.DisplayConfig).Status);
        var unresolved = Read(Prepare(WithPaths(Path() with {
            TargetAvailable = false, SourceAdapter = Absent(DataState.Unknown, ReasonCode.UnmatchedAdapter) })));
        Assert.Equal(["topology.correlation-unresolved", "topology.active-path-target-unavailable"],
            Topology(unresolved).Select(f => f.Id));
        Assert.All(Topology(report), f => Assert.Equal("information", f.Severity));
        Assert.All(Topology(unresolved), f => Assert.Equal("information", f.Severity));
        Assert.DoesNotContain("cable", Topology(report)[1].Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TopologyTests.FakeApi TargetUnavailableFake()
    {
        var fake = TopologyTests.Single();
        fake.Paths[0].Target.TargetAvailable = 0;
        return fake;
    }

    [Fact]
    public void AvailableEmptyDiffersFromNonactiveEntryAndUnavailableTopology()
    {
        var empty = Read(Prepare(WithPaths()));
        var finding = Assert.Single(Topology(empty));
        Assert.Equal("topology.no-active-paths", finding.Id);
        Assert.Equal(["facts.displays"], finding.Evidence);
        Assert.DoesNotContain("native", finding.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Topology(Read(Prepare(WithPaths(Path() with { PathActive = false, TargetAvailable = false })))));
        var fake = TopologyTests.Single(); fake.Paths[0].Flags = 0;
        Assert.Equal("topology.no-active-paths", Assert.Single(Topology(Read(Prepare(
            TopologyTests.WithTopology(TopologyTests.Collect(fake)))))).Id);
    }

    [Theory]
    [InlineData(DataState.Unknown, ReasonCode.MissingValue, DataSource.DisplayConfig)]
    [InlineData(DataState.Unsupported, ReasonCode.NotSupported, DataSource.DisplayConfig)]
    [InlineData(DataState.Failed, ReasonCode.QueryFailed, DataSource.DisplayConfig)]
    [InlineData(DataState.Redacted, ReasonCode.SensitiveValue, DataSource.DisplayConfig)]
    [InlineData(DataState.Unsupported, ReasonCode.NotImplemented, DataSource.NotCollected)]
    public void NonavailableTopologyEmitsNoM5Finding(DataState state, ReasonCode reason, DataSource source)
    {
        Assert.Empty(Topology(Read(Prepare(WithTopologyState(state, reason, source)))));
    }

    [Fact]
    public void InventoryFindingsKeepOriginalContractAndPrecedeOrderedPathFindings()
    {
        var first = Path() with { TargetAvailable = false };
        var second = Path() with { SourceAdapter = Absent(DataState.Unknown, ReasonCode.UnmatchedAdapter) };
        var third = Path() with { TargetAdapter = Exact("gpu-1") };
        var report = Read(Prepare(WithPaths(first, second, third)));
        Assert.Equal(["inventory.multiple-adapters", "topology.endpoint-adapter-association",
            "topology.active-path-target-unavailable", "topology.correlation-unresolved", "topology.endpoint-adapter-association"],
            report.Findings.Select(f => f.Id));
        var inventory = report.Findings[0];
        Assert.Equal("Windows reports multiple video controllers. This alone does not establish hybrid mode, display routing, GPU power state, or which GPU an application uses.", inventory.Message);
        Assert.Equal("information", inventory.Severity);
        Assert.Equal(["facts.gpus"], inventory.Evidence);
        Assert.Equal(1, report.Findings.Count(f => f.Id == "inventory.multiple-adapters"));
        Assert.Contains("display-1", report.Findings[1].Message);
        Assert.Contains("display-2", report.Findings[3].Message);
        Assert.Contains("display-3", report.Findings[4].Message);

        var emptyInventory = WithPaths();
        emptyInventory = emptyInventory with { Facts = emptyInventory.Facts with {
            Gpus = Observation<IReadOnlyList<GpuFacts>>.Known([], DataSource.WmiVideoController) } };
        var emptyReport = Read(Prepare(emptyInventory));
        Assert.Equal(["inventory.empty", "topology.no-active-paths"], emptyReport.Findings.Select(f => f.Id));
        Assert.Equal("The provider returned no video controllers. This is not proof that the computer has no GPU.", emptyReport.Findings[0].Message);
        Assert.Equal(["facts.gpus"], emptyReport.Findings[0].Evidence);
    }

    [Fact]
    public void EvidenceResolvesInSameExportAndMessagesUseItsProjectedLabels()
    {
        var original = WithPaths(Path() with { TargetAvailable = false }, Path() with {
            TargetAdapter = Absent(DataState.Unknown, ReasonCode.AmbiguousAdapter), TargetAvailable = false }, Path());
        var safe = Prepare(original);
        var json = ReportWriter.Json(safe);
        using var document = JsonDocument.Parse(json);
        var report = Read(safe);
        foreach (var finding in Topology(report)) VerifyClaim(document.RootElement, finding);
        Assert.Equal(["facts.displays.value[0].id", "facts.displays.value[0].pathActive",
            "facts.displays.value[0].sourceAdapter", "facts.displays.value[0].targetAdapter"], Topology(report)[0].Evidence);
        Assert.Equal(["facts.displays.value[1].id", "facts.displays.value[1].pathActive",
            "facts.displays.value[1].targetAvailable"], Topology(report)[3].Evidence);
        Assert.Equal("topology.endpoint-adapter-association", Topology(report)[0].Id);
        Assert.Equal("topology.endpoint-adapter-association", Topology(report)[4].Id);
        Assert.NotEqual(Topology(report)[0].Evidence[0], Topology(report)[4].Evidence[0]);

        var reversed = Read(Prepare(WithPaths(original.Facts.Displays.Value![2], original.Facts.Displays.Value[0])));
        Assert.Contains("display-1", Topology(reversed)[0].Message);
        Assert.Equal("facts.displays.value[1].targetAvailable", Topology(reversed)[2].Evidence[2]);

        var emptySafe = Prepare(WithPaths());
        using var emptyDocument = JsonDocument.Parse(ReportWriter.Json(emptySafe));
        VerifyClaim(emptyDocument.RootElement, Assert.Single(Topology(Read(emptySafe))));
    }

    [Fact]
    public void PrivacyProjectionDoesNotLeakRawLabelsTextOrMutateInput()
    {
        const string secret = "PRIVATE_M5_LOCAL_PATH";
        var raw = Path() with { Id = secret, SourceId = secret, TargetId = secret,
            SourceAdapterId = secret, TargetAdapterId = secret,
            Name = Observation<string>.Known("token=private", DataSource.DisplayConfig),
            SourceAdapter = Absent(DataState.Redacted, ReasonCode.SensitiveValue) };
        var snapshot = WithPaths(raw);
        var safe = Prepare(snapshot);
        var json = ReportWriter.Json(safe);
        var markdown = ReportWriter.Markdown(safe);
        Assert.DoesNotContain(secret, json); Assert.DoesNotContain(secret, markdown);
        Assert.DoesNotContain("token=private", json); Assert.DoesNotContain("token=private", markdown);
        Assert.DoesNotContain("PRIVATE_ERROR_PATH", json); Assert.DoesNotContain("PRIVATE_ERROR_PATH", markdown);
        Assert.Equal(secret, snapshot.Facts.Displays.Value![0].Id);
        Assert.Equal("token=private", snapshot.Facts.Displays.Value[0].Name.Value);
        var report = Read(safe);
        var finding = Assert.Single(Topology(report));
        Assert.Equal("topology.correlation-unresolved", finding.Id);
        Assert.Contains("For display-1", finding.Message);
        Assert.Contains("state: redacted", finding.Message);
        Assert.All(finding.Evidence, evidence => Assert.DoesNotContain(secret, evidence));
    }

    [Fact]
    public void NativeMetadataExceptionTextStaysOutOfM5FindingsAndBothExports()
    {
        var fake = TopologyTests.Single(); fake.ThrowSource = true;
        var report = Prepare(TopologyTests.WithTopology(TopologyTests.Collect(fake)));
        var finding = Assert.Single(Topology(Read(report)));
        Assert.Equal("topology.endpoint-adapter-association", finding.Id);
        Assert.DoesNotContain("PRIVATE_ERROR_PATH", finding.Message);
        Assert.DoesNotContain("PRIVATE_ERROR_PATH", ReportWriter.Json(report));
        Assert.DoesNotContain("PRIVATE_ERROR_PATH", ReportWriter.Markdown(report));
    }

    [Fact]
    public void EmptyFindingsPreserveTheExistingNoHealthVerdictSentence()
    {
        var report = Prepare(ModelAndPrivacyTests.Sample());
        Assert.Empty(Read(report).Findings);
        var markdown = ReportWriter.Markdown(report);
        Assert.Contains("No rules produced a finding; this is not a health verdict.", markdown);
        Assert.Equal(1, markdown.Split("## Interpreted findings").Length - 1);
    }

    [Theory]
    [InlineData("facts.Displays.value[0].id")]
    [InlineData("facts.displays.value[00].id")]
    [InlineData("facts.displays.value[-1].id")]
    [InlineData("facts.displays.value[9].id")]
    [InlineData("facts.displays.value[0].missing")]
    [InlineData("facts.displays.value[*].id")]
    [InlineData("facts.displays.value[0].targetAvailable.extra")]
    [InlineData("facts.displays.value[0].sourceAdapter.value.gpuId")]
    public void EvidenceResolverRejectsUnsupportedOrUnresolvableReferences(string evidence)
    {
        using var document = JsonDocument.Parse(ReportWriter.Json(Prepare(WithPaths(Path()))));
        Assert.ThrowsAny<Exception>(() => Resolve(document.RootElement, evidence));
    }

    [Fact]
    public void EvidenceResolverRejectsWrongTerminalTypeAndMissingObject()
    {
        using var wrongType = JsonDocument.Parse("""{"facts":{"displays":{"state":"available","value":[{"id":7,"pathActive":true}] ,"source":"displayConfig","reason":"none"}}}""");
        Assert.ThrowsAny<Exception>(() => Resolve(wrongType.RootElement, "facts.displays.value[0].id"));
        using var missing = JsonDocument.Parse("""{"facts":{"displays":{"state":"available","value":[{}],"source":"displayConfig","reason":"none"}}}""");
        Assert.ThrowsAny<Exception>(() => Resolve(missing.RootElement, "facts.displays.value[0].sourceAdapter"));
    }

    [Fact]
    public void JsonAndMarkdownShareOrderedFindingsAndEscapeEveryFindingField()
    {
        var safe = Prepare(WithPaths(Path() with { TargetAvailable = false }));
        var report = Read(safe);
        var markdown = ReportWriter.Markdown(safe);
        var lines = markdown.Split('\n').Where(line => line.StartsWith("- **", StringComparison.Ordinal)).ToArray();
        Assert.Equal(report.Findings.Count, lines.Length);
        for (var i = 0; i < report.Findings.Count; i++)
        {
            Assert.Contains(Escaped(report.Findings[i].Id), lines[i]);
            Assert.Contains(Escaped(report.Findings[i].Severity), lines[i]);
            Assert.Contains(Escaped(report.Findings[i].Message), lines[i]);
            foreach (var evidence in report.Findings[i].Evidence) Assert.Contains(Escaped(evidence), lines[i]);
        }
        Assert.Equal(1, markdown.Split("## Interpreted findings").Length - 1);

        var adversarial = report with { Findings = [new("id|\\[x]*_", "inf|o", "see [x] | \\ *", ["facts.displays.value[0].id|\\[]"])] };
        var constructor = typeof(ShareableReport).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(DiagnosticReport)], null)!;
        var fixture = (ShareableReport)constructor.Invoke([adversarial]);
        var writerJson = Read(fixture);
        Assert.Equal("id|\\[x]*_", writerJson.Findings[0].Id);
        Assert.Equal("see [x] | \\ *", writerJson.Findings[0].Message);
        var writerMarkdown = ReportWriter.Markdown(fixture);
        Assert.Contains(@"id\|\\\[x\]\*\_", writerMarkdown);
        Assert.Contains(@"inf\|o", writerMarkdown);
        Assert.Contains(@"see \[x\] \| \\ \*", writerMarkdown);
        Assert.Contains(@"facts\.displays\.value\[0\]\.id\|\\\[\]", writerMarkdown);
        Assert.Equal(1, writerMarkdown.Split("## Interpreted findings").Length - 1);
    }

    [Fact]
    public void SyntheticExamplesMatchTheProductionPrivacyAndWriterPath()
    {
        var report = PrivacyPolicy.Prepare(TopologyTests.WithTopology(TopologyTests.Collect(TopologyTests.Single(true))), new(2026, 9, 10));
        var json = ReportWriter.Json(report).Replace("\r\n", "\n");
        var markdown = ReportWriter.Markdown(report);
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        Assert.Equal(json, NormalizeLineEndings(File.ReadAllText(System.IO.Path.Combine(root, "examples", "report.example.json"))));
        Assert.Equal(markdown, NormalizeLineEndings(File.ReadAllText(System.IO.Path.Combine(root, "examples", "report.example.md"))));
    }

    [Theory]
    [InlineData("first\nsecond\n")]
    [InlineData("first\r\nsecond\r\n")]
    [InlineData("first\rsecond\r")]
    public void ExampleTextComparisonNormalizesLineEndingsOnly(string expected)
    {
        const string generated = "first\nsecond\n";
        Assert.Equal(generated, NormalizeLineEndings(expected));
        Assert.NotEqual(generated, NormalizeLineEndings(expected.Replace("second", "changed", StringComparison.Ordinal)));
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string Token<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
    private static string Escaped(string value)
    {
        var chars = "\\`*_{}[]<>()#+-.!|";
        return string.Concat(value.Select(c => chars.Contains(c) ? "\\" + c : c.ToString()));
    }

    // This test-only parser recognizes the frozen M5 evidence grammar, not arbitrary JSONPath.
    private static JsonElement Resolve(JsonElement report, string reference)
    {
        var match = Regex.Match(reference,
            @"\Afacts\.displays(?:\z|\.value\[(0|[1-9][0-9]*)\]\.(id|pathActive|sourceAdapter|targetAdapter|targetAvailable)\z)",
            RegexOptions.CultureInvariant);
        if (!match.Success) throw new FormatException("Unsupported M5 evidence reference.");
        var displays = report.GetProperty("facts").GetProperty("displays");
        CheckObservation(displays);
        if (!match.Groups[1].Success) return displays;
        if (displays.GetProperty("state").GetString() != "available") throw new FormatException("No available topology array.");
        var array = displays.GetProperty("value");
        if (array.ValueKind != JsonValueKind.Array ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index >= array.GetArrayLength())
            throw new FormatException("Invalid display array index.");
        var node = array[index].GetProperty(match.Groups[2].Value);
        switch (match.Groups[2].Value)
        {
            case "id" when node.ValueKind == JsonValueKind.String: break;
            case "pathActive" or "targetAvailable" when node.ValueKind is JsonValueKind.True or JsonValueKind.False: break;
            case "sourceAdapter" or "targetAdapter": CheckObservation(node); break;
            default: throw new FormatException("Wrong evidence terminal type.");
        }
        return node;
    }

    private static void CheckObservation(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.GetProperty("state").ValueKind != JsonValueKind.String ||
            value.GetProperty("source").ValueKind != JsonValueKind.String ||
            value.GetProperty("reason").ValueKind != JsonValueKind.String ||
            !value.TryGetProperty("value", out _))
            throw new FormatException("Evidence is not an observation.");
    }

    private static void VerifyClaim(JsonElement report, DiagnosticFinding finding)
    {
        var displays = report.GetProperty("facts").GetProperty("displays");
        var array = displays.GetProperty("value");
        foreach (var reference in finding.Evidence) Resolve(report, reference);
        if (finding.Id == "topology.no-active-paths")
        {
            Assert.Equal(["facts.displays"], finding.Evidence);
            Assert.Equal("available", displays.GetProperty("state").GetString());
            Assert.Equal(0, array.GetArrayLength());
            return;
        }
        var index = int.Parse(Regex.Match(finding.Evidence[0], @"\[(\d+)\]").Groups[1].Value, CultureInfo.InvariantCulture);
        var path = array[index];
        var id = path.GetProperty("id").GetString()!;
        Assert.Contains(id, finding.Message);
        Assert.True(path.GetProperty("pathActive").GetBoolean());
        var root = $"facts.displays.value[{index}]";
        if (finding.Id == "topology.active-path-target-unavailable")
        {
            Assert.Equal([root + ".id", root + ".pathActive", root + ".targetAvailable"], finding.Evidence);
            Assert.False(path.GetProperty("targetAvailable").GetBoolean());
            return;
        }
        Assert.Equal([root + ".id", root + ".pathActive", root + ".sourceAdapter", root + ".targetAdapter"], finding.Evidence);
        var source = path.GetProperty("sourceAdapter"); var target = path.GetProperty("targetAdapter");
        var bothExact = source.GetProperty("state").GetString() == "available" && target.GetProperty("state").GetString() == "available";
        Assert.Equal(bothExact ? "topology.endpoint-adapter-association" : "topology.correlation-unresolved", finding.Id);
        foreach (var (endpoint, observation) in new[] { ("source", source), ("target", target) })
        {
            if (observation.GetProperty("state").GetString() == "available")
            {
                var match = observation.GetProperty("value");
                var gpu = match.GetProperty("gpuId").GetString()!;
                Assert.Equal("exactSetupApiInstanceId", match.GetProperty("evidence").GetString());
                Assert.Equal("exact", match.GetProperty("confidence").GetString());
                Assert.Equal(1, report.GetProperty("facts").GetProperty("gpus").GetProperty("value")
                    .EnumerateArray().Count(g => g.GetProperty("id").GetString() == gpu));
                Assert.Contains($"{endpoint} endpoint is exactly associated with {gpu}", finding.Message);
            }
            else
                Assert.Contains($"{endpoint} endpoint association was not established in this report (state: {observation.GetProperty("state").GetString()}; source: {observation.GetProperty("source").GetString()}; reason: {observation.GetProperty("reason").GetString()})", finding.Message);
        }
    }
}
