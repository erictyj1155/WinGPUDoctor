using System.Diagnostics;
using WinGPUDoctor.Protocol;
namespace WinGPUDoctor.Supervisor;
internal interface IWorkerSession : IDisposable
{
    int ProcessId => 0; // Synthetic sessions have no OS process.
    bool IsInCreationTimeJob { get; }
    bool IsElevated { get; }
    WorkerBuildIdentity ExpectedIdentity { get; }
    ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token);
    ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token);
    ValueTask<bool> CleanupAsync(Deadline deadline);
}
internal interface IWorkerSessionFactory
{
    ValueTask<IWorkerSession> CreateAsync(WorkerOperation operation, WorkerLaunchOptions? options, Deadline connectDeadline,
        Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission, CancellationToken token);
}
internal static class DeadlineFraming
{
    internal static async ValueTask<ProtocolFrame> ReadAsync(Func<Memory<byte>, Deadline, CancellationToken, ValueTask<int>> read,
        Deadline deadline, CancellationToken token)
    {
        async ValueTask Fill(Memory<byte> buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                deadline.Check(token);
                var count = await read(buffer[offset..], deadline, token).ConfigureAwait(false);
                deadline.Check(token);
                if (count <= 0 || count > buffer.Length - offset) throw new EndOfStreamException("Incomplete worker frame.");
                offset += count;
            }
        }
        var prefix = new byte[4]; await Fill(prefix).ConfigureAwait(false);
        var length = ProtocolFraming.ReadLength(prefix);
        if (length == 0 || length > ProtocolConstants.MaxDataFrameBytes) throw ProtocolCodec.Limit();
        var payload = new byte[length]; await Fill(payload).ConfigureAwait(false);
        var frame = ProtocolCodec.Decode(payload); deadline.Check(token); return frame;
    }
}
internal sealed class NativeWorkerSession(WorkerProcess process, WorkerBuildIdentity identity,
    ICollectionTimingSink? timing = null, WorkerOperation operation = WorkerOperation.WmiOperatingSystem) : IWorkerSession
{
    private readonly ICollectionTimingSink _timing = timing ?? NullCollectionTimingSink.Instance;
    private bool _cleaned;
    private bool _firstReceive = true;
    public int ProcessId => process.ProcessId;
    public bool IsInCreationTimeJob => process.IsInCreationTimeJob;
    public bool IsElevated => process.IsElevated;
    public WorkerBuildIdentity ExpectedIdentity => identity;
    public async ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token)
    {
        deadline.Check(token); var bytes = ProtocolFraming.Encode(frame); deadline.Check(token);
        await process.Pipe.WriteAsync(bytes, deadline, token).ConfigureAwait(false);
        deadline.Check(token);
    }
    public async ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token)
    {
        if (!_firstReceive) return await DeadlineFraming.ReadAsync(process.Pipe.ReadAsync, deadline, token).ConfigureAwait(false);
        _firstReceive = false;
        var watch = Stopwatch.StartNew();
        var frame = await DeadlineFraming.ReadAsync(process.Pipe.ReadAsync, deadline, token).ConfigureAwait(false);
        _timing.Record(CollectionTimingStage.WorkerStartupToReady, operation.ToWire(), watch.Elapsed);
        return frame;
    }
    internal bool HasExited => process.WaitForExit(TimeSpan.Zero, out _);
    public async ValueTask<bool> CleanupAsync(Deadline deadline)
    {
        var total = Stopwatch.StartNew();
        process.RequestTermination(); process.Pipe.CancelPending();
        // Same absolute deadline for both phases, including cancellation callbacks.
        var ioWatch = Stopwatch.StartNew();
        var io = await process.Pipe.DrainAsync(deadline).ConfigureAwait(false);
        _timing.Record(CollectionTimingStage.PendingIoCancellation, operation.ToWire(), ioWatch.Elapsed);
        var exitWatch = Stopwatch.StartNew();
        var exit = await process.ConfirmExitAsync(deadline).ConfigureAwait(false);
        _timing.Record(CollectionTimingStage.WorkerExitConfirmation, operation.ToWire(), exitWatch.Elapsed);
        _cleaned = io && exit && !deadline.Expired;
        _timing.Record(CollectionTimingStage.CleanupTotal, operation.ToWire(), total.Elapsed);
        return _cleaned;
    }
    public void Dispose()
    {
        if (!_cleaned) throw new InvalidOperationException("Session requires confirmed cleanup or retained ownership.");
        process.Dispose();
    }
}
internal sealed class NativeWorkerSessionFactory(ICollectionTimingSink? timing = null) : IWorkerSessionFactory
{
    private readonly ICollectionTimingSink _timing = timing ?? NullCollectionTimingSink.Instance;
    public async ValueTask<IWorkerSession> CreateAsync(WorkerOperation operation, WorkerLaunchOptions? options, Deadline connectDeadline,
        Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission, CancellationToken token)
    {
        connectDeadline.Check(token);
        _ = ParentSecurityContext.BeforeLaunch(ParentSecurityContext.Read, () => true);
        WorkerDeploymentDescriptor descriptor;
        var deploymentWatch = Stopwatch.StartNew();
        try { descriptor = WorkerDeployment.Resolve(); }
        catch { throw new WorkerAdmissionException(AdmissionFailure.Deployment); }
        _timing.Record(CollectionTimingStage.DeploymentPreparation, operation.ToWire(), deploymentWatch.Elapsed);
        connectDeadline.Check(token);
        try
        {
            var processWatch = Stopwatch.StartNew();
            var process = await NativeWorkerLauncher.LaunchAsync(descriptor, options, connectDeadline, cleanupLimit, cleanupAllowance, admission, token).ConfigureAwait(false);
            _timing.Record(CollectionTimingStage.ProcessCreation, operation.ToWire(), processWatch.Elapsed);
            return new NativeWorkerSession(process, descriptor.Identity, _timing, operation);
        }
        catch (Exception ex) when (ex is not (WorkerAdmissionException or OperationCanceledException or TimeoutException))
        { throw new WorkerAdmissionException(AdmissionFailure.Containment); }
    }
}
