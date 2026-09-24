using System.Text.Json;
using System.Reflection;
using WinGPUDoctor.Core;
using WinGPUDoctor.Windows;
using Xunit;

namespace WinGPUDoctor.Tests;

public class ModelAndPrivacyTests
{
    internal static Observation<string> Known(string value) => Observation<string>.Known(value, DataSource.WmiVideoController);
    internal static CollectionSnapshot Sample(string name = "Example GPU", int count = 1)
    {
        var gpu = new GpuFacts("PRIVATE-INSTANCE", Known(name), Known("10DE"), Known("1234"), Known("unverified"),
            new(Known("Example Vendor"), Known("1.2.3.4"), Known("2026-09-01")));
        return new(new(new(Known("10.0.26200"), Known("26200"), Known("Example OEM"), Known("Example Model")),
            Observation<IReadOnlyList<GpuFacts>>.Known(Enumerable.Repeat(gpu, count).ToArray(), DataSource.WmiVideoController),
            Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Unsupported, DataSource.NotCollected, ReasonCode.NotImplemented)),
            [new(DataSource.WmiVideoController, CollectorStatus.Succeeded, ReasonCode.None)]);
    }

    [Fact] public void ObservationRejectsContradictoryStates()
    {
        Assert.Throws<ArgumentException>(() => new Observation<string>(DataState.Available, null, DataSource.WmiVideoController, ReasonCode.None));
        Assert.Throws<ArgumentException>(() => new Observation<string>(DataState.Failed, "guess", DataSource.WmiVideoController, ReasonCode.QueryFailed));
        Assert.Throws<ArgumentException>(() => new Observation<string>(DataState.Unknown, null, DataSource.WmiVideoController, ReasonCode.None));
        Assert.Throws<ArgumentException>(() => new Observation<string>((DataState)99, null, DataSource.WmiVideoController, ReasonCode.None));
    }

    [Theory]
    [InlineData(DataState.Unknown, ReasonCode.MissingValue)]
    [InlineData(DataState.Unsupported, ReasonCode.NotImplemented)]
    [InlineData(DataState.Failed, ReasonCode.AccessDenied)]
    [InlineData(DataState.Redacted, ReasonCode.SensitiveValue)]
    public void UnavailableStatesRoundTrip(DataState state, ReasonCode reason)
    {
        var field = Observation<string>.Absent(state, DataSource.WmiVideoController, reason);
        var json = JsonSerializer.Serialize(field, ReportWriter.JsonOptions);
        Assert.Equal(field, JsonSerializer.Deserialize<Observation<string>>(json, ReportWriter.JsonOptions));
        Assert.Contains("\"value\": null", json);
    }

    [Fact] public void JsonRoundTripKeepsSchemaStatesAndProvenance()
    {
        var json = ReportWriter.Json(PrivacyPolicy.Prepare(Sample(count: 2), new(2026, 9, 10)));
        var report = JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;
        Assert.Equal("0.2.0", report.SchemaVersion);
        Assert.Equal("0.1.0", report.ToolVersion);
        Assert.Equal(ToolIdentity.Version, report.ToolVersion);
        Assert.Equal(typeof(ToolIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            report.ToolVersion);
        Assert.Equal(2, report.Facts.Gpus.Value!.Count);
        Assert.Equal(DataState.Unsupported, report.Facts.Displays.State);
        Assert.Single(report.Findings);
        Assert.Equal(DataSource.WmiVideoController, report.Facts.Gpus.Value[0].Name.Source);
        Assert.Equal(json.TrimEnd(), JsonSerializer.Serialize(report, ReportWriter.JsonOptions));
        Assert.DoesNotContain("PRIVATE-INSTANCE", json);
        Assert.DoesNotContain("unverified", json);
    }

    [Theory]
    [InlineData(@"C:\Users\Alice\private.txt")]
    [InlineData(@"\\PRIVATE-HOST\share")]
    [InlineData("alice@example.test")]
    [InlineData("10.20.30.40")]
    [InlineData("2001:db8::1")]
    [InlineData("AA-BB-CC-DD-EE-FF")]
    [InlineData("token=super-secret")]
    [InlineData("Serial ABC123")]
    [InlineData("ghp_abcdefghijklmnopqrstuvwxyz")]
    [InlineData("Example\u001b[31mGPU")]
    [InlineData("Example\u202eGPU")]
    [InlineData("<script>alert(1)</script>")]
    public void SuspiciousProviderTextNeverReachesEitherExporter(string privateText)
    {
        var safe = PrivacyPolicy.Prepare(Sample(privateText), new(2026, 9, 10));
        var json = ReportWriter.Json(safe);
        var markdown = ReportWriter.Markdown(safe);
        Assert.DoesNotContain(privateText, json);
        Assert.DoesNotContain(privateText, markdown);
        var r = JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;
        Assert.Equal(DataState.Redacted, r.Facts.Gpus.Value![0].Name.State);
        Assert.Equal(1, r.Privacy.RedactedFields);
    }

    [Fact] public void NumericVersionIsNotMistakenForIpAddress()
    {
        var json = ReportWriter.Json(PrivacyPolicy.Prepare(Sample(), new(2026, 9, 10)));
        Assert.Contains("1.2.3.4", json);
        Assert.Equal(0, JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!.Privacy.RedactedFields);
    }
    [Fact] public void PrivacyDoesNotMutateRawFacts()
    {
        var snapshot = Sample("token=private");
        PrivacyPolicy.Prepare(snapshot, new(2026, 9, 10));
        Assert.Equal("token=private", snapshot.Facts.Gpus.Value![0].Name.Value);
    }
    [Fact] public void MarkdownEscapesProviderFormatting()
    {
        var md = ReportWriter.Markdown(PrivacyPolicy.Prepare(Sample("**GPU** #1"), new(2026, 9, 10)));
        Assert.Contains(@"\*\*GPU\*\* \#1", md);
    }
    [Fact] public void FailedInventoryCannotProduceNoGpuFinding()
    {
        var sample = Sample();
        var facts = sample.Facts with { Gpus = Observation<IReadOnlyList<GpuFacts>>.Absent(DataState.Failed, DataSource.WmiVideoController, ReasonCode.QueryFailed) };
        Assert.Empty(DiagnosticRules.Evaluate(facts));
        var empty = DiagnosticRules.Evaluate(Sample(count: 0).Facts);
        Assert.Equal("inventory.empty", Assert.Single(empty).Id);
    }
    [Fact] public void MultipleAdaptersDoNotProveHybridMode()
    {
        var finding = Assert.Single(DiagnosticRules.Evaluate(Sample(count: 2).Facts));
        Assert.Contains("does not establish hybrid mode", finding.Message);
        Assert.Equal(["facts.gpus"], finding.Evidence);
    }
}
