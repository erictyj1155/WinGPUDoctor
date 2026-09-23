using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class ProductionTimingPolicyTests
{
    private const long Frequency = 1000;
    private static readonly CollectionTimingPolicy Policy = CollectionTimingPolicy.CalibratedProduction;

    private static CollectionSupervisorStateMachine Started(int count = 1, long now = 0)
    {
        var state = new CollectionSupervisorStateMachine(Policy, Frequency);
        state.Begin(count, now);
        Assert.True(state.TryStartOperation(WorkerOperation.WmiOperatingSystem, now, out _));
        return state;
    }

    private static void Handshake(CollectionSupervisorStateMachine state, long now)
    {
        Assert.True(state.TryRecordReady(now));
        Assert.True(state.TryRecordStartSent(now));
        Assert.True(state.TryRecordAttemptStarted(now));
    }

    private static void Complete(CollectionSupervisorStateMachine state, WorkerOperation operation,
        long start, out long deadline)
    {
        Assert.True(state.TryStartOperation(operation, start, out _));
        deadline = state.OperationDeadline!.Value;
        Handshake(state, start);
        Assert.True(state.TryAcceptResult(ProtocolTests.SuccessResult(operation), deadline - 1));
        Assert.True(state.FinalizeCleanup(true, deadline));
    }

    [Fact]
    public void CalibratedProductionPolicyReservesLaterOperationsAndUsesFullBudgets()
    {
        var state = Started(5);
        Assert.Equal(9_500, state.OperationDeadline); // first of five reserves four later operation slots
        Handshake(state, 0);
        Assert.True(state.TryAcceptResult(ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem), 9_499));
        Assert.True(state.FinalizeCleanup(true, 9_500));
        Assert.True(state.TryStartOperation(WorkerOperation.WmiComputerSystem, 9_500, out _));
        Assert.Equal(19_500, state.OperationDeadline);
    }

    [Fact]
    public void CalibratedProductionPolicyBoundaryIsStrict()
    {
        var before = Started(5);
        var beforeDeadline = before.OperationDeadline!.Value;
        Handshake(before, 0);
        Assert.True(before.TryAcceptResult(ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem), beforeDeadline - 1));
        Assert.True(before.FinalizeCleanup(true, beforeDeadline));
        Assert.Equal(SupervisorTerminalKind.ResultAccepted, before.LastOutcome!.Kind);

        var equality = Started(5);
        var equalityDeadline = equality.OperationDeadline!.Value;
        Handshake(equality, 0);
        Assert.False(equality.TryAcceptResult(ProtocolTests.SuccessResult(WorkerOperation.WmiOperatingSystem), equalityDeadline));
        Assert.True(equality.FinalizeCleanup(true, equalityDeadline + 1));
        Assert.Equal(SupervisorTerminalKind.Timeout, equality.LastOutcome!.Kind);
    }

    [Fact]
    public void CalibratedProductionPolicyCompletesAllFiveOperations()
    {
        var state = new CollectionSupervisorStateMachine(Policy, Frequency);
        state.Begin(5, 0);
        var now = 0L;
        foreach (var operation in new[]
        {
            WorkerOperation.WmiOperatingSystem, WorkerOperation.WmiComputerSystem, WorkerOperation.WmiVideoControllers,
            WorkerOperation.WmiDisplayDrivers, WorkerOperation.DisplayActiveTopology
        })
        {
            Complete(state, operation, now, out var deadline);
            now = deadline;
        }
        Assert.Equal(5, state.CompletedResults.Count);
        Assert.Equal(SupervisorPhase.Completed, state.Phase);
    }

    [Fact]
    public void CalibratedProductionPolicyConditionalDriverOmissionReleasesLaterBudget()
    {
        var state = new CollectionSupervisorStateMachine(Policy, Frequency);
        state.Begin(5, 0);
        var now = 0L;
        foreach (var operation in new[]
        {
            WorkerOperation.WmiOperatingSystem, WorkerOperation.WmiComputerSystem, WorkerOperation.WmiVideoControllers
        })
        {
            Complete(state, operation, now, out var deadline);
            now = deadline;
        }
        state.OmitOperation();
        Assert.True(state.TryStartOperation(WorkerOperation.DisplayActiveTopology, now, out _));
        Assert.Equal(now + 10_000, state.OperationDeadline);
        Handshake(state, now);
        Assert.True(state.TryAcceptResult(ProtocolTests.SuccessResult(WorkerOperation.DisplayActiveTopology), now + 10_000 - 1));
        Assert.True(state.FinalizeCleanup(true, now + 10_000));
        Assert.Equal(SupervisorPhase.Completed, state.Phase);
    }

    [Fact]
    public void CalibratedProductionPolicyOverallBudgetExhaustionSkipsLaterStarts()
    {
        var state = new CollectionSupervisorStateMachine(Policy, Frequency);
        state.Begin(1, 0);
        Assert.False(state.TryStartOperation(WorkerOperation.WmiOperatingSystem, 60_000, out var skipped));
        Assert.Equal(SupervisorTerminalKind.Skipped, skipped!.Kind);
        Assert.Equal(ReasonCode.Timeout, skipped.Reason);
        Assert.Equal(0, skipped.Attempts);
    }
}
