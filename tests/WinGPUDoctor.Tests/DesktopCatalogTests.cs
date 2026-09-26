using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using WinGPUDoctor.Core;
using WinGPUDoctor.Desktop;
using WinGPUDoctor.Desktop.ViewModels;
using Xunit;

namespace WinGPUDoctor.Tests;

public class DesktopCatalogTests
{
    private static string Root => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    // String literals compiled into Core, so a new finding ID or connector token without copy fails here.
    private static IReadOnlyList<string> CoreLiterals()
    {
        using var stream = File.OpenRead(typeof(DiagnosticRules).Assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var literals = new List<string>();
        var size = reader.GetHeapSize(HeapIndex.UserString);
        for (var handle = MetadataTokens.UserStringHandle(1); !handle.IsNil && MetadataTokens.GetHeapOffset(handle) < size;
             handle = reader.GetNextHandle(handle))
            literals.Add(reader.GetUserString(handle));
        return literals;
    }

    private static ResultViewModel Result(CollectionSnapshot snapshot) =>
        new(ReportDocument.From(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 26))));

    [Fact]
    public void EveryEnumValueHasCatalogCopy()
    {
        foreach (var code in Enum.GetValues<WarningCode>())
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.Warning(code).NotMeaning));
        foreach (var state in Enum.GetValues<DataState>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.State(state).NotMeaning));
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.State(state).Title));
        }
        foreach (var reason in Enum.GetValues<ReasonCode>())
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.Reason(reason).Meaning));
        foreach (var status in Enum.GetValues<CollectorStatus>())
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.Collector(status).Meaning));
        foreach (var source in Enum.GetValues<DataSource>())
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.Source(source)));
        foreach (var term in ExplanationCatalog.GlossaryTerms)
            Assert.False(string.IsNullOrWhiteSpace(ExplanationCatalog.Glossary(term)));
    }

    [Fact]
    public void EveryCoreFindingIdHasCatalogCopy()
    {
        var coreIds = CoreLiterals().Where(s => Regex.IsMatch(s, @"\A(?!facts\.)[a-z]+\.[a-z]+(?:-[a-z]+)*\z")).ToHashSet();
        Assert.Equal(ExplanationCatalog.FindingIds.Order(), coreIds.Order());
        foreach (var id in ExplanationCatalog.FindingIds)
        {
            var entry = Assert.IsType<CatalogEntry>(ExplanationCatalog.Finding(id));
            Assert.False(string.IsNullOrWhiteSpace(entry.Title));
            Assert.False(string.IsNullOrWhiteSpace(entry.Meaning));
            Assert.False(string.IsNullOrWhiteSpace(entry.NotMeaning));
        }
        Assert.Null(ExplanationCatalog.Finding("inventory.not-a-rule"));
    }

    [Fact]
    public void RulesProduceOnlyCataloguedFindingsAcrossAllBranches()
    {
        var topology = DesktopTests.Fixture("topology");
        var displays = topology.Facts.Displays.Value!;
        var unavailableTarget = topology with
        {
            Facts = topology.Facts with
            {
                Displays = Observation<IReadOnlyList<DisplayFacts>>.Known([displays[0] with { TargetAvailable = false }], DataSource.DisplayConfig)
            }
        };
        var noPaths = topology with
        {
            Facts = topology.Facts with { Displays = Observation<IReadOnlyList<DisplayFacts>>.Known([], DataSource.DisplayConfig) }
        };
        var produced = new[] { DesktopTests.Fixture("multiple"), DesktopTests.Fixture("empty"), topology,
                DesktopTests.Fixture("unmatched"), unavailableTarget, noPaths }
            .SelectMany(s => ReportDocument.From(PrivacyPolicy.Prepare(s, new(2026, 9, 26))).Report.Findings)
            .Select(f => f.Id).ToHashSet();
        Assert.Equal(ExplanationCatalog.FindingIds.Order(), produced.Order());
    }

    [Fact]
    public void EveryReportedOutputTechnologyTokenHasFriendlyText()
    {
        var pattern = CoreLiterals().Single(s => s.StartsWith(@"\A(?:hd15|", StringComparison.Ordinal));
        var tokens = pattern[@"\A(?:".Length..^@")\z".Length].Split('|');
        Assert.Equal(20, tokens.Length);
        Assert.All(tokens, token => Assert.NotNull(UiText.Find("OutputTechnology." + token)));
    }

    [Fact]
    public void EveryXamlTextKeyExists()
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(Root, "src", "WinGPUDoctor.Desktop", "MainWindow.xaml"));
        var keys = Regex.Matches(xaml, @"\{views:Text ([^}\s]+)\}|<views:Text Key=""([^""]+)""")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Distinct().ToArray();
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.NotNull(UiText.Find(key)));
    }

    // Reviewed list (ADR 0008, docs/GUI_PLAN.md): copy must not add claims that Core does not make.
    private static readonly string[] NeverAllowed =
    [
        @"\b(nvidia|amd|intel|radeon|geforce|qualcomm)\b",           // vendor inferred from IDs or labels
        @"\bwindows (10|11)\b",                                       // OS name inferred from a build number
        @"\b(re)?install\b", @"\bdownload\b", @"\bupdate (your|the) driver", // no driver advice
        @"\b(enable|disable|turn on|turn off|switch to)\b", @"\bbios\b", @"\bregistry\b", @"\boverclock", @"\boptimi[sz]"
    ];

    // Diagnosis, rendering and connection terms may appear only as a stated limit.
    private static readonly string[] LimitOnly =
    [
        @"\bhealth(y)?\b", @"\bfaulty\b", @"\bbroken\b", @"\bdefective\b", @"\boutdated\b", @"\bup to date\b",
        @"\bworking correctly\b", @"\bproblem\b", @"\bwrong\b", @"\bcause\b", @"\brender\w*", @"\bhybrid\b",
        @"\bmux\b", @"\bpower state\b", @"\bbusy\b", @"\bphysical\b", @"\bport\b", @"\bcable\b", @"\bplugged\b", @"\bdisconnected\b"
    ];

    [Fact]
    public void CopyAddsNoClaimsBeyondCore()
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var entries = UiText.NeutralEntries();
        Assert.True(entries.Count > 200);
        foreach (var (key, text) in entries)
        {
            foreach (var pattern in NeverAllowed)
                Assert.False(Regex.IsMatch(text, pattern, options), $"{key} matches {pattern}");
            // The limits card items all follow "It can't tell you:".
            if (key.StartsWith("Card.Limits.", StringComparison.Ordinal)) continue;
            foreach (var sentence in Regex.Split(text, @"(?<=[.!?])\s+"))
                if (LimitOnly.Any(p => Regex.IsMatch(sentence, p, options)))
                    Assert.True(Regex.IsMatch(sentence, @"\b(not|no|cannot|never)\b|n't\b", options), $"{key}: {sentence}");
        }
    }

    [Fact]
    public void FindingsWarningsAndCollectionUseCatalogCopy()
    {
        var result = Result(DesktopTests.Fixture("unmatched"));
        var findings = result.Cards.Single(c => c.Title == UiText.Get("Card.Findings.Title"));
        Assert.Equal(new[] { "Finding.inventory.multiple-adapters", "Finding.topology.correlation-unresolved" }.Select(UiText.Get),
            findings.Explanations.Select(e => e.Title));
        Assert.Null(findings.Explanations[0].Context);
        Assert.Equal(UiText.Format("Card.Display.Title", "1"), findings.Explanations[1].Context);
        // "What this does not mean" and the next step move into the (i) tip, both labelled.
        var unresolved = ExplanationCatalog.Finding("topology.correlation-unresolved")!;
        Assert.Equal(UiText.Format("Explanation.NotMeaning", unresolved.NotMeaning!) + "\n\n" +
            UiText.Format("Explanation.NextStep", unresolved.NextStep!), findings.Explanations[1].Tip);
        Assert.Equal(UiText.Format("Info.More", findings.Explanations[1].Title), findings.Explanations[1].TipName);
        // Core's own message stays available as a technical detail.
        Assert.StartsWith("For display-1: source endpoint association was not established", findings.Technical[1].Value);

        var warnings = result.Cards.Single(c => c.Title == UiText.Get("Card.Warnings.Title"));
        Assert.Equal(ExplanationCatalog.Warning(WarningCode.ReviewBeforeSharing).Title, warnings.Explanations[3].Title);
        Assert.All(warnings.Explanations, e => Assert.True(e.HasTip));

        var incomplete = Result(DesktopTests.Fixture("incomplete"));
        var collection = incomplete.Cards.Single(c => c.Title == UiText.Get("Card.Collection.Title"));
        var run = Assert.Single(collection.Facts);
        Assert.False(run.IsAvailable);
        Assert.Equal(ExplanationCatalog.Collector(CollectorStatus.Failed).Title, run.Value);
        Assert.NotEmpty(run.StateGlyph);
        Assert.Equal(ExplanationCatalog.Collector(CollectorStatus.Failed).Meaning, run.Help);

        var none = Result(DesktopTests.Fixture("single")).Cards.Single(c => c.Title == UiText.Get("Card.Findings.Title"));
        Assert.Empty(none.Explanations);
        Assert.Equal(UiText.Get("Card.Findings.None"), Assert.Single(none.Notes));
    }

    [Fact]
    public void DetailsExplainFieldsAndShowExactValuesWithProvenance()
    {
        var display = Result(DesktopTests.Fixture("topology")).MainCards[2];
        Assert.All(display.Facts, f => Assert.True(f.HasHelp)); // Every display field has a glossary tip.
        // Resolution and refresh rate form the card's sentence; their glossary is the title's (i) tip.
        Assert.Contains(ExplanationCatalog.Glossary("RefreshRate"), display.TitleTip);
        Assert.Contains(ExplanationCatalog.Glossary("Resolution"), display.TitleTip);
        Assert.Equal(UiText.Format("Info.About", display.Title), display.TitleTipName);
        var output = display.Facts.Single(f => f.Label == UiText.Get("Field.OutputTechnology"));
        Assert.Equal(ExplanationCatalog.Glossary("OutputTechnology"), output.Help);
        Assert.Equal(UiText.Format("Info.About", output.Label), output.HelpName);
        var technical = display.Technical.ToDictionary(t => t.Label);
        Assert.Equal("165/1 (165 Hz)", technical[UiText.Get("Field.RefreshRate")].Value);
        Assert.Equal("165000/1000 (165 Hz)", technical[UiText.Get("Field.SignalRate")].Value);
        Assert.Contains(ExplanationCatalog.Source(DataSource.DisplayConfig), technical[UiText.Get("Field.RefreshRate")].Provenance);

        var hidden = Result(DesktopTests.Fixture("redacted"));
        var redacted = hidden.DriverCards[0].Technical[0];
        Assert.Equal(UiText.Get("Technical.NoValue"), redacted.Value);
        Assert.Contains(ExplanationCatalog.Reason(ReasonCode.SensitiveValue).Title, redacted.Provenance);
        Assert.Contains(ExplanationCatalog.Reason(ReasonCode.SensitiveValue).Meaning, redacted.Provenance);
        // An unavailable value explains its state in the tip.
        var state = ExplanationCatalog.State(DataState.Redacted);
        Assert.Equal(state.Meaning + " " + state.NotMeaning, hidden.MainCards[1].Facts[0].Help);
    }

    [Fact]
    public void InfoTipIsAKeyboardStopWithScreenReaderText()
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var tip = new WinGPUDoctor.Desktop.Views.InfoTip { Text = "Explanation", Label = "About Resolution" };
                Assert.True(tip.Focusable);
                Assert.True(tip.IsTabStop);
                Assert.Equal("About Resolution", System.Windows.Automation.AutomationProperties.GetName(tip));
                Assert.Equal("Explanation", System.Windows.Automation.AutomationProperties.GetHelpText(tip));
                // The service's own keyboard-focus opening is off; the control opens only for Tab (below).
                Assert.False(System.Windows.Controls.ToolTipService.GetShowsToolTipOnKeyboardFocus(tip));
                var toolTip = Assert.IsType<System.Windows.Controls.ToolTip>(tip.ToolTip);
                Assert.Equal("Explanation", Assert.IsType<System.Windows.Controls.TextBlock>(toolTip.Content).Text);
            }
            catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); // WPF controls need an STA thread.
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    // A LabeledBy on a choice control replaces its own text as the screen-reader name, so both format
    // options were announced as "Format" (found in the live check of the visual redesign).
    [Fact]
    public void ChoiceControlsKeepTheirOwnTextAsTheirAccessibleName()
    {
        var xaml = File.ReadAllText(System.IO.Path.Combine(Root, "src", "WinGPUDoctor.Desktop", "MainWindow.xaml"));
        var choices = Regex.Matches(xaml, @"<(RadioButton|CheckBox)\b[^>]*>").Select(m => m.Value).ToArray();
        Assert.Equal(3, choices.Length); // Markdown, JSON and "Show technical details".
        Assert.All(choices, c =>
        {
            Assert.Matches(@"Content=""\{views:Text [^}]+\}""", c);
            Assert.DoesNotContain("AutomationProperties.LabeledBy", c);
            Assert.DoesNotContain("AutomationProperties.Name", c);
        });
    }

    [Fact]
    public void InfoTipOpensOnKeyboardFocusChangeFromAnotherElement()
    {
        // Keyboard navigation such as Tab or Shift+Tab; the rule does not identify the key.
        Assert.True(WinGPUDoctor.Desktop.Views.InfoTip.ShowsOnFocusChange(keyboardInput: true, fromAnotherElement: true));
        // Window reactivation (for example Alt+Tab back) restores focus from no element.
        Assert.False(WinGPUDoctor.Desktop.Views.InfoTip.ShowsOnFocusChange(keyboardInput: true, fromAnotherElement: false));
        Assert.False(WinGPUDoctor.Desktop.Views.InfoTip.ShowsOnFocusChange(keyboardInput: false, fromAnotherElement: true)); // Mouse
    }
}
