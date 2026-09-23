using System.Text;
using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using Xunit;

namespace WinGPUDoctor.Tests;

public class ProtocolTests
{
    private static Observation<string> Text(string value, DataSource source) =>
        Observation<string>.Known(value, source);

    internal static readonly WorkerBuildIdentity Identity = new(1, "10.0.0", "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", "33333333-3333-3333-3333-333333333333", "44444444-4444-4444-4444-444444444444");
    private static RequestFrame Request(WorkerOperation operation) => new(operation);
    internal static StartFrame Start(WorkerOperation operation) => new(operation, new WorkerRequestPayload(
        operation == WorkerOperation.DisplayActiveTopology ? new DisplayActiveTopologyRequest([new("gpu-1", "synthetic-instance"), new("gpu-2", null)]) : null));
    internal static CollectorRun Run() => new(DataSource.DisplayConfig, CollectorStatus.Succeeded, ReasonCode.None)
    { QueryMode = DisplayQueryMode.VirtualModeAndRefreshAware, Issues = [new(CollectionOperation.TargetName, ReasonCode.MissingValue, null)] };
    internal static WorkerResultPayload SuccessPayload(WorkerOperation operation) => operation switch
    {
        WorkerOperation.WmiOperatingSystem => new(
            new WmiOperatingSystemResult(Text("10.0", DataSource.WmiOperatingSystem), Text("1", DataSource.WmiOperatingSystem)),
            null, null, null, null),
        WorkerOperation.WmiComputerSystem => new(
            null, new WmiComputerSystemResult(Text("Example", DataSource.WmiComputerSystem), Text("Model", DataSource.WmiComputerSystem)),
            null, null, null),
        WorkerOperation.WmiVideoControllers => new(
            null, null, new WmiVideoControllersResult(Observation<IReadOnlyList<WmiVideoControllerFact>>.Known(
                [new("synthetic-instance", Text("GPU", DataSource.WmiVideoController), Text("10DE", DataSource.WmiVideoController),
                    Text("1234", DataSource.WmiVideoController))], DataSource.WmiVideoController)), null, null),
        WorkerOperation.WmiDisplayDrivers => new(
            null, null, null, new WmiDisplayDriversResult(Observation<IReadOnlyList<WmiDisplayDriverFact>>.Known(
                [new("synthetic-instance", Text("Provider", DataSource.WmiSignedDriver), Text("1.0", DataSource.WmiSignedDriver),
                    Text("2026-09-13", DataSource.WmiSignedDriver))], DataSource.WmiSignedDriver)), null),
        WorkerOperation.DisplayActiveTopology => new(
            null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known([], DataSource.DisplayConfig), Run())),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    internal static ResultFrame SuccessResult(WorkerOperation operation) =>
        new(operation, WorkerResultState.Succeeded, ReasonCode.None, SuccessPayload(operation));

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void AllFrameAndOperationShapesRoundTrip()
    {
        foreach (var operation in Enum.GetValues<WorkerOperation>())
        {
            var frames = new ProtocolFrame[]
            {
                Request(operation),
                new ReadyFrame(operation, Identity),
                Start(operation),
                new AttemptStartedFrame(operation),
                SuccessResult(operation)
            };
            foreach (var frame in frames)
            {
                var decoded = ProtocolCodec.Decode(ProtocolCodec.Encode(frame));
                Assert.Equal(ProtocolFraming.Encode(frame), ProtocolFraming.Encode(decoded));
            }
        }
    }

    [Fact]
    public void MalformedTruncatedAndInvalidUtf8FramesAreRejectedGenericly()
    {
        foreach (var bytes in new[] { Utf8("{"), Utf8("{\"version\":1,"), new byte[] { 0x7b, 0xff } })
        {
            var exception = Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(bytes));
            Assert.Equal(ReasonCode.InvalidValue, exception.Reason);
        }
    }

    [Fact]
    public void DuplicateUnknownAndMissingPropertiesAreRejected()
    {
        var duplicate = """{"version":1,"kind":"ready","operation":"wmi.operatingSystem","state":null,"reason":null,"payload":{},"payload":{}}""";
        var unknown = """{"version":1,"kind":"ready","operation":"wmi.operatingSystem","state":null,"reason":null,"payload":{},"extra":1}""";
        var missing = """{"version":1,"kind":"ready","operation":"wmi.operatingSystem","payload":{}}""";
        foreach (var json in new[] { duplicate, unknown, missing })
            Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(Utf8(json)));
    }

    [Fact]
    public void UnknownVersionKindAndOperationAreRejected()
    {
        var version = """{"version":2,"kind":"ready","operation":"wmi.operatingSystem","state":null,"reason":null,"payload":{}}""";
        var kind = """{"version":1,"kind":"stream","operation":"wmi.operatingSystem","state":null,"reason":null,"payload":{}}""";
        var operation = """{"version":1,"kind":"ready","operation":"wmi.arbitrary","state":null,"reason":null,"payload":{}}""";
        foreach (var json in new[] { version, kind, operation })
            Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(Utf8(json)));
    }

    [Fact]
    public void ResourceLimitsMapToResourceLimit()
    {
        var huge = new string('a', ProtocolConstants.MaxDecodedStringChars + 1);
        var stringFrame = "{\"version\":1,\"kind\":\"request\",\"operation\":\"display.activeTopology\",\"state\":null,\"reason\":null," +
            "\"payload\":{\"displayActiveTopology\":{\"inventory\":[{\"label\":\"gpu-1\",\"transientInstanceId\":\"" + huge + "\"}]}}}";
        var items = string.Join(",", Enumerable.Range(0, ProtocolConstants.MaxCollectionItems + 1)
            .Select(i => "{\"label\":\"gpu-" + (i + 1) + "\",\"transientInstanceId\":\"x\"}"));
        var countFrame = "{\"version\":1,\"kind\":\"request\",\"operation\":\"display.activeTopology\",\"state\":null,\"reason\":null," +
            "\"payload\":{\"displayActiveTopology\":{\"inventory\":[" + items + "]}}}";
        var depth = new string('[', ProtocolConstants.MaxJsonDepth + 2) + "0" + new string(']', ProtocolConstants.MaxJsonDepth + 2);
        var depthFrame = "{\"version\":1,\"kind\":\"ready\",\"operation\":\"wmi.operatingSystem\",\"state\":null,\"reason\":null," +
            "\"payload\":{\"depth\":" + depth + "}}";
        foreach (var json in new[] { stringFrame, countFrame, depthFrame })
        {
            var exception = Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(Utf8(json)));
            Assert.Equal(ReasonCode.ResourceLimit, exception.Reason);
        }
    }

    [Fact]
    public void ContradictoryResultStatesAndPayloadsAreRejected()
    {
        var success = SuccessResult(WorkerOperation.WmiOperatingSystem);
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(
            success with { Reason = ReasonCode.Timeout }));
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(
            new ResultFrame(WorkerOperation.WmiOperatingSystem, WorkerResultState.Failed, ReasonCode.None, null)));
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(
            new ResultFrame(WorkerOperation.WmiOperatingSystem, WorkerResultState.Succeeded, ReasonCode.None,
                SuccessPayload(WorkerOperation.WmiComputerSystem))));
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Encode(
            new ResultFrame(WorkerOperation.WmiOperatingSystem, WorkerResultState.Succeeded, ReasonCode.None,
                new WorkerResultPayload(SuccessPayload(WorkerOperation.WmiOperatingSystem).WmiOperatingSystem,
                    SuccessPayload(WorkerOperation.WmiComputerSystem).WmiComputerSystem, null, null, null))));
    }

    [Fact]
    public void SequenceValidatorRejectsIllegalOrderAndUnknownTopologyReferences()
    {
        var ready = new ReadyFrame(WorkerOperation.DisplayActiveTopology, Identity);
        Assert.Throws<ProtocolValidationException>(() => new ProtocolSequenceValidator().Record(ready));

        var request = Request(WorkerOperation.DisplayActiveTopology);
        var badTopology = new ResultFrame(WorkerOperation.DisplayActiveTopology, WorkerResultState.Succeeded, ReasonCode.None,
            new WorkerResultPayload(null, null, null, null, new DisplayActiveTopologyResult(
                Observation<IReadOnlyList<DisplayFacts>>.Known([Display("gpu-9")], DataSource.DisplayConfig), Run())));
        var validator = new ProtocolSequenceValidator();
        validator.Record(request);
        validator.Record(new ReadyFrame(WorkerOperation.DisplayActiveTopology, Identity));
        validator.Record(Start(WorkerOperation.DisplayActiveTopology));
        validator.Record(new AttemptStartedFrame(WorkerOperation.DisplayActiveTopology));
        Assert.Throws<ProtocolValidationException>(() => validator.Record(badTopology));
    }

    internal static DisplayFacts Display(string gpuId) => new(
        "display-1", "source-1", "target-1", "adapter-1", "adapter-1",
        Observation<AdapterMatch>.Known(new(gpuId, AdapterMatchEvidence.ExactSetupApiInstanceId,
            AdapterMatchConfidence.Exact), DataSource.SetupApiInstanceJoin),
        Observation<AdapterMatch>.Known(new(gpuId, AdapterMatchEvidence.ExactSetupApiInstanceId,
            AdapterMatchConfidence.Exact), DataSource.SetupApiInstanceJoin),
        Observation<string>.Known(@"\\.\DISPLAY1", DataSource.DisplayConfig),
        Observation<string>.Absent(DataState.Unknown, DataSource.DisplayConfig, ReasonCode.MissingValue),
        Observation<string>.Known("internal", DataSource.DisplayConfig),
        Observation<PixelSize>.Known(new(1, 1), DataSource.DisplayConfig),
        Observation<RationalRate>.Known(new(1, 1), DataSource.DisplayConfig),
        Observation<RationalRate>.Known(new(1, 1), DataSource.DisplayConfig),
        Observation<string>.Known("identity", DataSource.DisplayConfig),
        Observation<string>.Known("progressive", DataSource.DisplayConfig),
        true, true,
        Observation<FlagValue>.Known(new(false), DataSource.DisplayConfig),
        Observation<string>.Absent(DataState.Unknown, DataSource.DisplayConfig, ReasonCode.MissingValue),
        DisplayQueryMode.VirtualModeAndRefreshAware);
}
