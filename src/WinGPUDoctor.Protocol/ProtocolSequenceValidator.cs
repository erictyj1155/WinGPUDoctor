using WinGPUDoctor.Core;
namespace WinGPUDoctor.Protocol;
internal enum ProtocolSequenceState { AwaitingRequest, AwaitingReady, AwaitingStart, AwaitingAttemptStarted, AwaitingResult, Complete }
internal sealed class ProtocolSequenceValidator
{
    private WorkerOperation? _operation;
    private WorkerRequestPayload? _input;
    private int _attempts;
    internal ProtocolSequenceState State { get; private set; } = ProtocolSequenceState.AwaitingRequest;
    internal int Attempts => _attempts;
    internal void Record(ProtocolFrame frame)
    {
        if (_operation is { } operation && operation != frame.Operation) throw ProtocolCodec.Invalid();
        switch (frame)
        {
            case RequestFrame when State == ProtocolSequenceState.AwaitingRequest:
                _operation = frame.Operation; State = ProtocolSequenceState.AwaitingReady; break;
            case ReadyFrame when State == ProtocolSequenceState.AwaitingReady:
                State = ProtocolSequenceState.AwaitingStart; break;
            case StartFrame start when State == ProtocolSequenceState.AwaitingStart:
                ProtocolSemantics.Start(frame.Operation, start.Payload); _input = start.Payload; State = ProtocolSequenceState.AwaitingAttemptStarted; break;
            case AttemptStartedFrame attempt when State is ProtocolSequenceState.AwaitingAttemptStarted or ProtocolSequenceState.AwaitingResult:
                if (attempt.Attempt != _attempts + 1 || attempt.Attempt > (frame.Operation == WorkerOperation.DisplayActiveTopology ? 3 : 1)) throw ProtocolCodec.Invalid();
                _attempts++; State = ProtocolSequenceState.AwaitingResult; break;
            case ResourceLimitFrame when State == ProtocolSequenceState.AwaitingResult:
                State = ProtocolSequenceState.Complete; break;
            case ResultFrame result when State == ProtocolSequenceState.AwaitingResult ||
                (State == ProtocolSequenceState.AwaitingAttemptStarted &&
                 result.Payload?.DisplayActiveTopology?.Run is { Attempts: 0, Status: CollectorStatus.Failed or CollectorStatus.Unsupported }):
                ProtocolSemantics.Result(result);
                if (result.Payload?.DisplayActiveTopology is { } topology)
                {
                    if (topology.Run.Attempts != _attempts) throw ProtocolCodec.Invalid();
                    var known = _input!.DisplayActiveTopology!.Inventory.Where(i => i.TransientInstanceId is not null)
                        .GroupBy(i => i.TransientInstanceId!, StringComparer.OrdinalIgnoreCase)
                        .Where(group => group.Count() == 1).Select(group => group.Single().Label).ToHashSet(StringComparer.Ordinal);
                    foreach (var d in topology.Displays.Value ?? [])
                        foreach (var match in new[] { d.SourceAdapter, d.TargetAdapter })
                            if (match.State == DataState.Available && !known.Contains(match.Value!.GpuId)) throw ProtocolCodec.Invalid();
                }
                State = ProtocolSequenceState.Complete; break;
            default: throw ProtocolCodec.Invalid();
        }
    }
}
