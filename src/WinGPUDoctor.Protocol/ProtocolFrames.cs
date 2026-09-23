using WinGPUDoctor.Core;
namespace WinGPUDoctor.Protocol;
internal abstract record ProtocolFrame(WorkerOperation Operation) { internal abstract ProtocolFrameKind Kind { get; } }
internal sealed record RequestFrame(WorkerOperation Operation) : ProtocolFrame(Operation)
{ internal override ProtocolFrameKind Kind => ProtocolFrameKind.Request; }
internal sealed record ReadyFrame(WorkerOperation Operation, WorkerBuildIdentity Identity) : ProtocolFrame(Operation)
{ internal override ProtocolFrameKind Kind => ProtocolFrameKind.Ready; }
internal sealed record StartFrame(WorkerOperation Operation, WorkerRequestPayload Payload) : ProtocolFrame(Operation)
{ internal override ProtocolFrameKind Kind => ProtocolFrameKind.Start; }
internal sealed record AttemptStartedFrame(WorkerOperation Operation, int Attempt = 1) : ProtocolFrame(Operation)
{ internal override ProtocolFrameKind Kind => ProtocolFrameKind.AttemptStarted; }
internal sealed record ResultFrame(WorkerOperation Operation, WorkerResultState State, ReasonCode Reason, WorkerResultPayload? Payload) : ProtocolFrame(Operation)
{ internal override ProtocolFrameKind Kind => ProtocolFrameKind.Result; }
// Only the fixed output-bound failure is allowed; no provider content or native diagnostics.
internal sealed record ResourceLimitFrame(WorkerOperation Operation) : ProtocolFrame(Operation)
{ internal override ProtocolFrameKind Kind => ProtocolFrameKind.Failure; }
