using System.Text;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using Xunit;

namespace WinGPUDoctor.Tests;

public class ResourceLimitFrameTests
{
    [Fact]
    public void EveryFixedOperationHasSmallContentFreeFailureFrame()
    {
        foreach (var operation in Enum.GetValues<WorkerOperation>())
        {
            var bytes = ProtocolCodec.Encode(new ResourceLimitFrame(operation));
            Assert.True(bytes.Length < 256);
            Assert.Equal(new ResourceLimitFrame(operation), ProtocolCodec.Decode(bytes));
        }
    }

    [Theory]
    [InlineData("\"reason\":\"resourceLimit\"", "\"reason\":\"queryFailed\"")]
    [InlineData("\"state\":\"failed\"", "\"state\":\"succeeded\"")]
    [InlineData("\"payload\":{}", "\"payload\":{\"message\":\"PRIVATE\"}")]
    [InlineData("\"payload\":{}", "\"payload\":null")]
    public void FailureDoesNotRelaxAnyResultOrPayloadContract(string original, string replacement)
    {
        var json = Encoding.UTF8.GetString(ProtocolCodec.Encode(new ResourceLimitFrame(WorkerOperation.WmiVideoControllers)));
        Assert.Contains(original, json);
        Assert.Throws<ProtocolValidationException>(() => ProtocolCodec.Decode(Encoding.UTF8.GetBytes(json.Replace(original, replacement))));
    }

    [Fact]
    public void FailureRequiresStartedAttemptAndIsTerminal()
    {
        var op = WorkerOperation.WmiOperatingSystem;
        var sequence = new ProtocolSequenceValidator();
        Assert.Throws<ProtocolValidationException>(() => sequence.Record(new ResourceLimitFrame(op)));
        sequence.Record(new RequestFrame(op)); sequence.Record(new ReadyFrame(op, ProtocolTests.Identity));
        sequence.Record(new StartFrame(op, new(null)));
        Assert.Throws<ProtocolValidationException>(() => sequence.Record(new ResourceLimitFrame(op)));
        sequence.Record(new AttemptStartedFrame(op));
        Assert.Throws<ProtocolValidationException>(() => sequence.Record(new ResourceLimitFrame(WorkerOperation.WmiComputerSystem)));
        sequence.Record(new ResourceLimitFrame(op));
        Assert.Equal(ProtocolSequenceState.Complete, sequence.State);
        Assert.Throws<ProtocolValidationException>(() => sequence.Record(new ResourceLimitFrame(op)));
    }
}
