namespace WinGPUDoctor.Supervisor;

internal enum AdmissionFailure { SecurityContext, Deployment, Containment, Launch, CleanupUnconfirmed, HostPoisoned, Busy }
internal sealed class WorkerAdmissionException(AdmissionFailure failure) : Exception("Worker admission rejected.")
{
    internal AdmissionFailure Failure { get; } = failure;
}

// One process-lifetime owner; no reset API. The lock serializes admission/poisoning,
// not I/O. Quarantine roots whole owners until host exit, including their SafeHandles.
internal sealed class HostAdmission
{
    internal static HostAdmission Process { get; } = new();
    private readonly object _sync = new();
    private readonly List<object> _quarantine = [];
    private bool _active;
    private bool _poisoned;
    internal bool IsPoisoned { get { lock (_sync) return _poisoned; } }
    internal int RetainedOwners { get { lock (_sync) return _quarantine.Count; } }
    internal IDisposable Enter()
    {
        lock (_sync)
        {
            if (_poisoned) throw new WorkerAdmissionException(AdmissionFailure.HostPoisoned);
            if (_active) throw new WorkerAdmissionException(AdmissionFailure.Busy);
            _active = true;
            return new Lease(this);
        }
    }
    internal void Poison(object? owner = null)
    {
        lock (_sync)
        {
            _poisoned = true;
            if (owner is not null && !_quarantine.Contains(owner)) _quarantine.Add(owner);
        }
    }
    private sealed class Lease(HostAdmission owner) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            lock (owner._sync) owner._active = false;
        }
    }
}
