namespace WinGPUDoctor.Supervisor;
internal sealed record WorkerDeploymentEntry(string RelativePath, long Length, string Sha256,
    string? AssemblyName = null, string? AssemblyVersion = null, string? Mvid = null);
internal sealed record WorkerDeploymentManifest(IReadOnlyList<WorkerDeploymentEntry> Entries);
