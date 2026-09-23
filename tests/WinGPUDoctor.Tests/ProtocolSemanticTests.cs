using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using Xunit;
namespace WinGPUDoctor.Tests;
public class ProtocolSemanticTests
{
    [Fact]
    public void MissingOrAmbiguousInventoryIdentityCannotProduceExactMatch()
    {
        var op = WorkerOperation.DisplayActiveTopology;
        foreach (var identities in new IReadOnlyList<DisplayTopologyIdentity>[]
        {
            [new("gpu-1", null)],
            [new("gpu-1", "synthetic-instance"), new("gpu-2", "SYNTHETIC-INSTANCE")]
        })
        {
            var sequence = new ProtocolSequenceValidator(); sequence.Record(new RequestFrame(op));
            sequence.Record(new ReadyFrame(op, ProtocolTests.Identity));
            sequence.Record(new StartFrame(op, new(new DisplayActiveTopologyRequest(identities))));
            sequence.Record(new AttemptStartedFrame(op));
            Assert.Throws<ProtocolValidationException>(() => sequence.Record(Topology(ProtocolTests.Run(), ProtocolTests.Display("gpu-1"))));
        }
    }
    private static ResultFrame Topology(CollectorRun run, params DisplayFacts[] displays) => new(WorkerOperation.DisplayActiveTopology,
        (WorkerResultState)run.Status, run.Reason, new(null, null, null, null, new(Observation<IReadOnlyList<DisplayFacts>>.Known(displays, DataSource.DisplayConfig), run)));
    private static DisplayFacts Named(DataState state, ReasonCode reason) =>
        ProtocolTests.Display("gpu-1") with { Name = Observation<string>.Absent(state, DataSource.DisplayConfig, reason) };
    private static string Wire<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    [Fact]
    public void OptionalMonitorNameAcceptsOnlyUnknownMissingValue()
    {
        var frame = Topology(ProtocolTests.Run(), ProtocolTests.Display("gpu-1"));
        var decoded = Assert.IsType<ResultFrame>(ProtocolCodec.Decode(ProtocolCodec.Encode(frame)));
        var name = decoded.Payload!.DisplayActiveTopology!.Displays.Value![0].Name;
        Assert.Equal(DataState.Unknown, name.State);
        Assert.Equal(ReasonCode.MissingValue, name.Reason);
    }

    [Theory]
    [InlineData(DataState.Failed, ReasonCode.MissingValue)]
    [InlineData(DataState.Unsupported, ReasonCode.MissingValue)]
    [InlineData(DataState.Unknown, ReasonCode.NotSupported)]
    [InlineData(DataState.Failed, ReasonCode.NotSupported)]
    public void ContradictoryOptionalMonitorNameStatesAreRejectedBySemanticValidation(DataState state, ReasonCode reason)
    {
        var frame = Topology(ProtocolTests.Run(), Named(state, reason));
        var direct = Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(frame));
        Assert.Equal(ReasonCode.InvalidValue, direct.Reason);

        var validBytes = ProtocolCodec.Encode(Topology(ProtocolTests.Run(), ProtocolTests.Display("gpu-1")));
        var json = JsonNode.Parse(validBytes)!;
        var name = json["payload"]!["displayActiveTopology"]!["displays"]!["value"]![0]!["name"]!;
        name["state"] = Wire(state);
        name["reason"] = Wire(reason);
        var decoded = Assert.Throws<ProtocolValidationException>(() =>
            ProtocolCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())));
        Assert.Equal(ReasonCode.InvalidValue, decoded.Reason);
    }

    [Theory]
    [InlineData(DataState.Failed, ReasonCode.SessionAccessDenied, 5)]
    [InlineData(DataState.Unsupported, ReasonCode.NotSupported, 50)]
    [InlineData(DataState.Failed, ReasonCode.NativeError, 31)]
    public void GenuineTargetNameFailuresRemainAccepted(DataState state, ReasonCode reason, int native)
    {
        var run = ProtocolTests.Run() with
        {
            Status = CollectorStatus.Partial,
            Reason = reason,
            Issues = [new(CollectionOperation.TargetName, reason, native)]
        };
        var frame = Topology(run, Named(state, reason));
        Assert.Equal(ProtocolCodec.Encode(frame), ProtocolCodec.Encode(ProtocolCodec.Decode(ProtocolCodec.Encode(frame))));
    }

    [Theory]
    [InlineData(DataState.Failed, ReasonCode.MissingValue)]
    [InlineData(DataState.Unsupported, ReasonCode.MissingValue)]
    public void SequenceRejectsContradictoryOptionalMonitorNameStates(DataState state, ReasonCode reason)
    {
        var operation = WorkerOperation.DisplayActiveTopology;
        var sequence = new ProtocolSequenceValidator();
        sequence.Record(new RequestFrame(operation));
        sequence.Record(new ReadyFrame(operation, ProtocolTests.Identity));
        sequence.Record(ProtocolTests.Start(operation));
        sequence.Record(new AttemptStartedFrame(operation));
        var rejected = Assert.Throws<ProtocolValidationException>(() =>
            sequence.Record(Topology(ProtocolTests.Run(), Named(state, reason))));
        Assert.Equal(ReasonCode.InvalidValue, rejected.Reason);
    }
    [Theory]
    [InlineData(0, "wmiOperatingSystem", "windowsVersion")]
    [InlineData(1, "wmiComputerSystem", "manufacturer")]
    [InlineData(2, "wmiVideoControllers", "controllers")]
    [InlineData(3, "wmiDisplayDrivers", "drivers")]
    [InlineData(4, "displayActiveTopology", "displays")]
    public void RequiredObservationsCannotBeNullMissingOrWrongProvenance(int operation, string payload, string field)
    {
        var bytes = ProtocolCodec.Encode(ProtocolTests.SuccessResult((WorkerOperation)operation));
        foreach (var mutation in new[] { "null", "missing", "source" })
        {
            var json = JsonNode.Parse(bytes)!; var obj = json["payload"]![payload]!.AsObject();
            if (mutation == "null") obj[field] = null;
            else if (mutation == "missing") obj.Remove(field);
            else obj[field]!["source"] = "notCollected";
            Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())));
        }
        if (operation == 0)
        {
            var json = JsonNode.Parse(bytes)!; json["payload"]![payload]!["windowsVersion"] = null; json["payload"]![payload]!["windowsBuild"] = null;
            Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(Encoding.UTF8.GetBytes(json.ToJsonString())));
        }
    }
    [Fact]
    public void ContradictoryTopologyStatusAttemptsIssuesAndFieldsAreRejected()
    {
        var run = ProtocolTests.Run(); var display = ProtocolTests.Display("gpu-1");
        foreach (var bad in new[]
        {
            Topology(run with { Attempts = 4 }, display), Topology(run with { Attempts = 0 }, display),
            Topology(run with { Attempts = 2 }, display),
            Topology(run with { Source = DataSource.WmiOperatingSystem }, display),
            Topology(run with { Issues = [] }, display),
            Topology(run with { Issues = [new(CollectionOperation.TargetName, ReasonCode.MissingValue, 5)] }, display),
            Topology(run with { Status = CollectorStatus.Partial, Reason = ReasonCode.MissingValue }, display),
            Topology(run, display with { SourceResolution = null! }),
            Topology(run, display with { SourceResolution = Observation<PixelSize>.Absent(DataState.Unknown, DataSource.DisplayConfig, ReasonCode.InvalidModeIndex) }),
            Topology(run, display with { PathActive = false }),
            Topology(run, display with { QueryMode = DisplayQueryMode.ActivePaths }),
            Topology(run, display with { PathRefreshRate = Observation<RationalRate>.Known(new(1, 0), DataSource.DisplayConfig) })
        }) Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(bad));
    }
    [Fact]
    public void RetryHistoryOptionalNamePartialAndExactReferencesRoundTripLosslessly()
    {
        var op = WorkerOperation.DisplayActiveTopology;
        var run = ProtocolTests.Run() with { Attempts = 3, Issues = [new(CollectionOperation.QueryPaths, ReasonCode.TopologyChanged, 122),
            new(CollectionOperation.QueryPaths, ReasonCode.TopologyChanged, 122), new(CollectionOperation.TargetName, ReasonCode.MissingValue, null)] };
        var frame = Topology(run, ProtocolTests.Display("gpu-1"));
        var sequence = new ProtocolSequenceValidator();
        foreach (var control in new ProtocolFrame[] { new RequestFrame(op), new ReadyFrame(op, ProtocolTests.Identity), ProtocolTests.Start(op),
            new AttemptStartedFrame(op, 1), new AttemptStartedFrame(op, 2), new AttemptStartedFrame(op, 3) }) sequence.Record(ProtocolCodec.Decode(ProtocolCodec.Encode(control)));
        var decoded = Assert.IsType<ResultFrame>(ProtocolCodec.Decode(ProtocolCodec.Encode(frame)));
        sequence.Record(decoded); Assert.Equal(ProtocolSequenceState.Complete, sequence.State);
        Assert.Equal(3, decoded.Payload!.DisplayActiveTopology!.Run.Attempts);
        Assert.Equal(run.Issues, decoded.Payload.DisplayActiveTopology.Run.Issues);
        var partial = Topology(run with { Status = CollectorStatus.Partial, Reason = ReasonCode.InvalidModeIndex,
            Issues = [.. run.Issues, new(CollectionOperation.DecodeMode, ReasonCode.InvalidModeIndex, null)] },
            ProtocolTests.Display("gpu-1") with { SourceResolution = Observation<PixelSize>.Absent(DataState.Unknown, DataSource.DisplayConfig, ReasonCode.InvalidModeIndex) });
        Assert.Equal(ProtocolCodec.Encode(partial), ProtocolCodec.Encode(ProtocolCodec.Decode(ProtocolCodec.Encode(partial))));
    }
    [Fact]
    public void ZeroAttemptPreQueryFailureAndMissingInventoryIdentityAreRepresentable()
    {
        var op = WorkerOperation.DisplayActiveTopology; var sequence = new ProtocolSequenceValidator();
        foreach (var control in new ProtocolFrame[] { new RequestFrame(op), new ReadyFrame(op, ProtocolTests.Identity), ProtocolTests.Start(op) }) sequence.Record(control);
        var run = new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Unsupported, ReasonCode.ApiUnavailable)
        { Attempts = 0, Issues = [new(CollectionOperation.QueryPaths, ReasonCode.ApiUnavailable, null)] };
        var result = new ResultFrame(op, WorkerResultState.Unsupported, ReasonCode.ApiUnavailable, new(null, null, null, null,
            new(Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Unsupported, DataSource.DisplayConfig, ReasonCode.ApiUnavailable), run)));
        sequence.Record(ProtocolCodec.Decode(ProtocolCodec.Encode(result)));
        Assert.Equal(ProtocolSequenceState.Complete, sequence.State);
        var contradiction = result with { State = WorkerResultState.Failed, Payload = new(null, null, null, null,
            new(Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Failed, DataSource.DisplayConfig, ReasonCode.ApiUnavailable), run with { Status = CollectorStatus.Failed })) };
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(contradiction));
        var missingHistory = result with { Payload = result.Payload! with { DisplayActiveTopology = result.Payload!.DisplayActiveTopology! with { Run = run with { Attempts = 3 } } } };
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(missingHistory));
        var start = Assert.IsType<StartFrame>(ProtocolCodec.Decode(ProtocolCodec.Encode(ProtocolTests.Start(op))));
        Assert.Null(start.Payload.DisplayActiveTopology!.Inventory[1].TransientInstanceId);
        Assert.DoesNotContain("synthetic-instance", Encoding.UTF8.GetString(ProtocolCodec.Encode(new RequestFrame(op))));
    }
    [Fact]
    public void AttemptMarkersCannotSkipRepeatExceedLimitOrDisagreeWithResult()
    {
        var op = WorkerOperation.DisplayActiveTopology;
        ProtocolSequenceValidator Started()
        {
            var s = new ProtocolSequenceValidator(); s.Record(new RequestFrame(op)); s.Record(new ReadyFrame(op, ProtocolTests.Identity)); s.Record(ProtocolTests.Start(op)); return s;
        }
        Assert.Throws<ProtocolValidationException>(() => Started().Record(new AttemptStartedFrame(op, 2)));
        var sequence = Started(); sequence.Record(new AttemptStartedFrame(op));
        Assert.Throws<ProtocolValidationException>(() => sequence.Record(new AttemptStartedFrame(op)));
        Assert.Throws<ProtocolValidationException>(() => sequence.Record(Topology(ProtocolTests.Run() with { Attempts = 2 })));
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(new AttemptStartedFrame(op, 4)));
    }
}
