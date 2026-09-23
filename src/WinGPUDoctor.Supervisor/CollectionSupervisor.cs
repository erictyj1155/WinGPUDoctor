using System.Diagnostics;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
namespace WinGPUDoctor.Supervisor;
internal sealed class CollectionSupervisor
{
    private readonly CollectionTimingPolicy _policy;
    private readonly TimeProvider _clock;
    private readonly IWorkerSessionFactory _factory;
    private readonly HostAdmission _admission;
    private readonly ICollectionTimingSink _timing;
    private readonly Action<CollectionProgress>? _progress;
    private readonly CollectionSupervisorStateMachine _state;
    private int _running;
    internal CollectionSupervisor(CollectionTimingPolicy policy, TimeProvider? timeProvider = null,
        IWorkerSessionFactory? sessionFactory = null, HostAdmission? admission = null,
        ICollectionTimingSink? timing = null, Action<CollectionProgress>? progress = null)
    {
        policy.Validate(); _policy = policy; _clock = timeProvider ?? TimeProvider.System;
        _factory = sessionFactory ?? new NativeWorkerSessionFactory(); _admission = admission ?? HostAdmission.Process;
        _timing = timing ?? NullCollectionTimingSink.Instance;
        _progress = progress;
        _state = new(policy, _clock.TimestampFrequency);
    }
    internal SupervisorSnapshot Snapshot => _state.Snapshot();
    internal void Begin(int operations) => _state.Begin(operations, _clock.GetTimestamp());
    internal void OmitOperation() => _state.OmitOperation();
    internal async ValueTask<SupervisorOutcome> RunOperationAsync(WorkerOperation operation, WorkerLaunchOptions? options,
        CancellationToken token, WorkerRequestPayload? input = null)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0) return new(SupervisorTerminalKind.HostFailure, ReasonCode.NativeError, null, "busy");
        IDisposable? lease = null;
        IWorkerSession? session = null;
        var operationWatch = Stopwatch.StartNew();
        var operationStarted = false;
        var cancellationRecorded = false;
        void ObserveCancellation()
        {
            if (!token.IsCancellationRequested) return;
            _state.TryCancel(_clock.GetTimestamp());
            if (cancellationRecorded) return;
            cancellationRecorded = true;
            _progress?.Invoke(new(CollectionProgressKind.CancellationObserved, operation.ToWire(), session?.ProcessId ?? 0));
        }
        try
        {
            if (_admission.IsPoisoned)
            {
                _state.HostFailure(AdmissionFailure.HostPoisoned, _clock.GetTimestamp());
                _state.FinalizeCleanup(true, _clock.GetTimestamp(), ownedResources: false); return _state.LastOutcome!;
            }
            if (token.IsCancellationRequested)
            {
                _state.TryCancel(_clock.GetTimestamp());
                _state.FinalizeCleanup(true, _clock.GetTimestamp(), ownedResources: false);
                return _state.LastOutcome!;
            }
            if (!_state.TryStartOperation(operation, _clock.GetTimestamp(), out var skipped))
                return skipped ?? _state.LastOutcome ?? new(SupervisorTerminalKind.HostFailure, ReasonCode.NativeError, null, "not-ready");
            operationStarted = true;
            try
            {
                lease = _admission.Enter(); token.ThrowIfCancellationRequested();
                session = await _factory.CreateAsync(operation, options, FrameDeadline(_policy.ConnectBudget),
                    new Deadline(_clock, _state.OperationCleanupLimit), _policy.CleanupAllowance, _admission, token).ConfigureAwait(false);
                if (!session.IsInCreationTimeJob) throw new WorkerAdmissionException(AdmissionFailure.Containment);
                if (session.IsElevated) throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
                _progress?.Invoke(new(CollectionProgressKind.WorkerStarted, operation.ToWire(), session.ProcessId));
                var sequence = new ProtocolSequenceValidator();
                var request = new RequestFrame(operation);
                var requestWatch = Stopwatch.StartNew();
                await session.SendAsync(request, FrameDeadline(_policy.FrameBudget), token).ConfigureAwait(false); sequence.Record(request);
                _timing.Record(CollectionTimingStage.RequestTransfer, operation.ToWire(), requestWatch.Elapsed);
                var ready = await session.ReceiveAsync(FrameDeadline(_policy.FrameBudget), token).ConfigureAwait(false);
                sequence.Record(ready);
                if (ready is not ReadyFrame readyFrame || readyFrame.Identity != session.ExpectedIdentity)
                    throw new WorkerAdmissionException(AdmissionFailure.Deployment);
                if (!_state.TryRecordReady(_clock.GetTimestamp())) throw new TimeoutException();
                // Identity input is not even serialized until deployment and Ready identity agree.
                var start = new StartFrame(operation, input ?? new WorkerRequestPayload(
                    operation == WorkerOperation.DisplayActiveTopology ? new DisplayActiveTopologyRequest([]) : null));
                var startWatch = Stopwatch.StartNew();
                await session.SendAsync(start, FrameDeadline(_policy.FrameBudget), token).ConfigureAwait(false); sequence.Record(start);
                _timing.Record(CollectionTimingStage.StartTransfer, operation.ToWire(), startWatch.Elapsed);
                if (!_state.TryRecordStartSent(_clock.GetTimestamp())) throw new TimeoutException();
                while (true)
                {
                    var frameWatch = Stopwatch.StartNew();
                    var frame = await session.ReceiveAsync(FrameDeadline(_policy.FrameBudget), token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested(); sequence.Record(frame);
                    if (frame is AttemptStartedFrame attempt)
                    {
                        if (!_state.TryRecordAttemptStarted(_clock.GetTimestamp())) throw new TimeoutException();
                        _progress?.Invoke(new(CollectionProgressKind.AttemptValidated, operation.ToWire(), session.ProcessId, attempt.Attempt));
                        continue;
                    }
                    if (frame is ResourceLimitFrame)
                    {
                        _timing.Record(CollectionTimingStage.ResultTransferValidation, operation.ToWire(), frameWatch.Elapsed);
                        if (!_state.TryProtocolFailure(ReasonCode.ResourceLimit, _clock.GetTimestamp())) throw new TimeoutException();
                        break;
                    }
                    _timing.Record(CollectionTimingStage.ResultTransferValidation, operation.ToWire(), frameWatch.Elapsed);
                    if (frame is not ResultFrame result || !_state.TryAcceptResult(result, _clock.GetTimestamp())) throw new TimeoutException();
                    break;
                }
            }
            catch (OperationCanceledException) { _state.TryCancel(_clock.GetTimestamp()); }
            catch (TimeoutException) { _state.TryTimeout(_clock.GetTimestamp()); }
            catch (WorkerAdmissionException ex)
            {
                _admission.Poison(); _state.HostFailure(ex.Failure, _clock.GetTimestamp());
            }
            catch (WorkerDeploymentException)
            {
                _admission.Poison(); _state.HostFailure(AdmissionFailure.Deployment, _clock.GetTimestamp());
            }
            catch (ProtocolValidationException ex)
            {
                if (_state.Phase == SupervisorPhase.AwaitingReady)
                {
                    // An invalid readiness frame leaves build identity unverifiable.
                    _admission.Poison(); _state.HostFailure(AdmissionFailure.Deployment, _clock.GetTimestamp());
                }
                else _state.TryProtocolFailure(ex.Reason, _clock.GetTimestamp());
            }
            catch { _state.TryProtocolFailure(ReasonCode.QueryFailed, _clock.GetTimestamp()); }
            finally
            {
                var confirmed = true;
                Task<bool>? cleanupTask = null;
                try
                {
                    ObserveCancellation();
                    if (session is not null)
                    {
                        var cleanupDeadline = new Deadline(_clock, _state.CleanupDeadline!.Value);
                        cleanupTask = session.CleanupAsync(cleanupDeadline).AsTask();
                        confirmed = await cleanupDeadline.ObserveAsync(cleanupTask).ConfigureAwait(false) &&
                            await cleanupTask.ConfigureAwait(false);
                        if (confirmed) session.Dispose(); // No new wait allowed here.
                        if (confirmed) _progress?.Invoke(new(CollectionProgressKind.CleanupConfirmed, operation.ToWire(), session.ProcessId));
                    }
                }
                catch { confirmed = false; }
                finally
                {
                    ObserveCancellation();
                    _state.FinalizeCleanup(confirmed, _clock.GetTimestamp(), session is not null);
                    if (!confirmed || _state.AdmissionPoisoned || _admission.IsPoisoned)
                    {
                        _admission.Poison(!confirmed && session is not null ? new object?[] { session, cleanupTask } : null);
                        if (_admission.IsPoisoned && !_state.AdmissionPoisoned)
                        {
                            _state.HostFailure(AdmissionFailure.CleanupUnconfirmed, _clock.GetTimestamp());
                            _state.FinalizeCleanup(false, _clock.GetTimestamp());
                        }
                    }
                }
            }
            return _state.LastOutcome!;
        }
        finally
        {
            if (operationStarted) _timing.Record(CollectionTimingStage.Operation, operation.ToWire(), operationWatch.Elapsed);
            lease?.Dispose();
            Volatile.Write(ref _running, 0);
        }
    }
    private Deadline FrameDeadline(TimeSpan budget) => new(_clock, _state.ClipDeadline(_clock.GetTimestamp(), budget));
}
