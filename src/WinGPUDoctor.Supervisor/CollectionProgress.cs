namespace WinGPUDoctor.Supervisor;

// Opt-in development observation only. No provider text or hardware identity.
internal enum CollectionProgressKind { WorkerStarted, AttemptValidated, CancellationObserved, CleanupConfirmed }
internal readonly record struct CollectionProgress(CollectionProgressKind Kind, string Operation, int WorkerPid, int Attempt = 0);
