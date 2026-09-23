namespace WinGPUDoctor.Supervisor;

internal enum CollectionTimingStage
{
    DeploymentPreparation,
    ProcessCreation,
    WorkerStartupToReady,
    RequestTransfer,
    StartTransfer,
    Operation,
    ResultTransferValidation,
    PendingIoCancellation,
    WorkerExitConfirmation,
    CleanupTotal
}

internal readonly record struct CollectionTimingSample(CollectionTimingStage Stage, string? Operation, TimeSpan Duration);

internal interface ICollectionTimingSink
{
    void Record(CollectionTimingStage stage, string? operation, TimeSpan duration);
}

internal sealed class NullCollectionTimingSink : ICollectionTimingSink
{
    internal static NullCollectionTimingSink Instance { get; } = new();
    public void Record(CollectionTimingStage stage, string? operation, TimeSpan duration) { }
}

internal sealed class CollectionTimingCollector : ICollectionTimingSink
{
    private readonly List<CollectionTimingSample> _samples = [];
    internal IReadOnlyList<CollectionTimingSample> Samples => _samples;
    public void Record(CollectionTimingStage stage, string? operation, TimeSpan duration) =>
        _samples.Add(new(stage, operation, duration));
}
