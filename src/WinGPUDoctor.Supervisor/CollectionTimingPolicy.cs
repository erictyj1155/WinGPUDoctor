namespace WinGPUDoctor.Supervisor;

// Calibrated on one available laptop (2026-09-21): healthy six-run maxima were
// 4.30 s for the signed-driver operation, 4.00 s for result transfer/validation,
// 0.111 s for worker startup to Ready and 0.028 s for cleanup. Values below add
// explicit margins; this is engineering calibration, not population percentiles.
internal sealed record CollectionTimingPolicy(
    TimeSpan OverallBudget,
    TimeSpan OperationBudget,
    TimeSpan CleanupAllowance,
    TimeSpan LaterOperationReservation,
    TimeSpan FinalBookkeepingReserve,
    TimeSpan ConnectBudget,
    TimeSpan FrameBudget)
{
    internal static CollectionTimingPolicy CalibratedProduction { get; } = new(
        OverallBudget: TimeSpan.FromSeconds(60),
        OperationBudget: TimeSpan.FromSeconds(10),
        CleanupAllowance: TimeSpan.FromSeconds(2),
        LaterOperationReservation: TimeSpan.FromSeconds(10),
        FinalBookkeepingReserve: TimeSpan.FromMilliseconds(500),
        ConnectBudget: TimeSpan.FromSeconds(2),
        FrameBudget: TimeSpan.FromSeconds(8));

    internal void Validate()
    {
        foreach (var value in new[] { OverallBudget, OperationBudget, CleanupAllowance, LaterOperationReservation,
            FinalBookkeepingReserve, ConnectBudget, FrameBudget })
        {
            if (value <= TimeSpan.Zero || value > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(value), "Timing budgets must be positive and at most one day.");
        }
    }
}
