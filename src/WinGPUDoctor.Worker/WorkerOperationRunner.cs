using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;

namespace WinGPUDoctor.Worker;

internal static class WorkerOperationRunner
{
    // The same dispatch/encoding path is used by the worker entry point and deterministic wire tests.
    internal static void Execute(WorkerOperationDispatcher dispatcher, WorkerOperation operation,
        WorkerRequestPayload input, Action<ProtocolFrame, byte[]> send)
    {
        var outcome = dispatcher.Dispatch(operation, input, attempt =>
        {
            var marker = new AttemptStartedFrame(operation, attempt);
            send(marker, ProtocolFraming.Encode(marker));
        });
        ProtocolFrame terminal = outcome.Frame;
        byte[] bytes;
        try { bytes = ProtocolFraming.Encode(terminal); }
        catch (ProtocolValidationException ex) when (ex.Reason == ReasonCode.ResourceLimit)
        {
            // A finite operation enum and fixed literals only. Never retry the offending payload.
            terminal = new ResourceLimitFrame(operation);
            bytes = ProtocolFraming.Encode(terminal);
        }
        // Transport failure is outside the encoding catch: no fallback/retry after a broken write.
        send(terminal, bytes);
    }
}
