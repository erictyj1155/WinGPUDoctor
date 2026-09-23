using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;

namespace WinGPUDoctor.Supervisor;

internal sealed record WorkerDeploymentDescriptor(string HostPath, string WorkerPath, string WorkingDirectory,
    WorkerDeploymentManifest Manifest, string RuntimeVersion, WorkerBuildIdentity Identity);
internal sealed class WorkerDeploymentException(ReasonCode reason, string message) : Exception(message)
{ internal ReasonCode Reason { get; } = reason; }

internal static class WorkerDeployment
{
    internal const string WorkerDirectoryName = "worker";
    internal const string WorkerAssemblyFileName = "wingpudoctor-worker.dll";
    internal static WorkerDeploymentDescriptor Resolve() => Resolve(AppContext.BaseDirectory, EmbeddedWorkerDeploymentManifest.Value);
    internal static WorkerDeploymentDescriptor Resolve(string baseDirectory, WorkerDeploymentManifest manifest)
    {
        var root = Path.GetFullPath(Path.Combine(baseDirectory, WorkerDirectoryName));
        Validate(root, manifest);
        foreach (var name in new[] { "WinGPUDoctor.Core", "WinGPUDoctor.Protocol", "WinGPUDoctor.Windows" })
        {
            var loaded = Assembly.Load(new AssemblyName(name));
            var entry = manifest.Entries.Single(e => e.RelativePath.Equals(name + ".dll", StringComparison.OrdinalIgnoreCase));
            ValidateLoadedIdentity(loaded, entry);
        }
        var runtime = ResolveRuntime(RuntimeEnvironment.GetRuntimeDirectory(), typeof(object).Assembly.Location, Environment.Version.ToString());
        string Module(string name) => manifest.Entries.Single(e => e.RelativePath.Equals(name, StringComparison.OrdinalIgnoreCase)).Mvid!;
        var identity = new WorkerBuildIdentity(ProtocolConstants.Version, runtime.Version,
            Module(WorkerAssemblyFileName), Module("WinGPUDoctor.Protocol.dll"), Module("WinGPUDoctor.Core.dll"), Module("WinGPUDoctor.Windows.dll"));
        return new(runtime.Host, Path.Combine(root, WorkerAssemblyFileName), root, manifest, runtime.Version, identity);
    }
    internal static string Normalize(string path)
    {
        var value = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Any(c => c < 32 || ":<>\"|?*".Contains(c)) ||
            value.Split('/').Any(p => p is "" or "." or ".." || p.EndsWith('.') || p.EndsWith(' '))) Reject("Unsafe worker path.");
        return value;
    }
    internal static void ValidateLoadedIdentity(Assembly loaded, WorkerDeploymentEntry entry)
    {
        if (loaded.GetName().Name != entry.AssemblyName || loaded.GetName().Version?.ToString() != entry.AssemblyVersion ||
            loaded.ManifestModule.ModuleVersionId.ToString("D") != entry.Mvid ||
            !string.Equals(Hash(loaded.Location), entry.Sha256, StringComparison.OrdinalIgnoreCase)) Reject("Parent/worker shared build mismatch.");
    }
    internal static void Validate(string workerRoot, WorkerDeploymentManifest manifest)
    {
        var root = Path.GetFullPath(workerRoot);
        if (!Directory.Exists(root) || manifest.Entries.Count == 0) Reject("Worker deployment is missing.");
        // Inspect one level at a time; reject links before descending or hashing a target.
        var actualFiles = new List<string>();
        var directories = new Stack<string>(); directories.Push(root);
        while (directories.TryPop(out var directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) Reject("Reparse point in worker deployment.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) Reject("Reparse point in worker deployment.");
                if ((attributes & FileAttributes.Directory) != 0) directories.Push(path); else actualFiles.Add(path);
            }
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Entries)
        {
            var relative = Normalize(entry.RelativePath);
            if (!seen.Add(relative)) Reject("Duplicate normalized worker path.");
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Reject("Escaping worker path.");
            var file = new FileInfo(path);
            if (!file.Exists || file.Length != entry.Length || !string.Equals(Hash(path), entry.Sha256, StringComparison.OrdinalIgnoreCase)) Reject("Missing or changed worker asset.");
            if (entry.AssemblyName is not null)
            {
                var identity = AssemblyName.GetAssemblyName(path);
                if (identity.Name != entry.AssemblyName || identity.Version?.ToString() != entry.AssemblyVersion ||
                    ReadMvid(path) != entry.Mvid) Reject("Worker assembly identity mismatch.");
            }
        }
        foreach (var path in actualFiles)
        {
            if (!path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) &&
                !seen.Contains(Normalize(Path.GetRelativePath(root, path)))) Reject("Unexpected worker asset.");
        }
        foreach (var name in new[] { WorkerAssemblyFileName, "WinGPUDoctor.Core.dll", "WinGPUDoctor.Windows.dll", "WinGPUDoctor.Protocol.dll" })
        {
            var entry = manifest.Entries.SingleOrDefault(e => Normalize(e.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry is null || entry.AssemblyName != Path.GetFileNameWithoutExtension(name) || !Guid.TryParseExact(entry.Mvid, "D", out _)) Reject("Required worker assembly missing.");
        }
        foreach (var name in new[] { "wingpudoctor-worker.deps.json", "wingpudoctor-worker.runtimeconfig.json" })
            if (!seen.Contains(name)) Reject("Required runtime metadata missing.");
    }
    internal static (string Host, string Version) ResolveRuntime(string directory, string coreLibrary, string version)
    {
        var runtime = new DirectoryInfo(Path.GetFullPath(directory));
        if (runtime.Name != version || !Version.TryParse(version, out _) || runtime.Parent?.Name != "Microsoft.NETCore.App" ||
            runtime.Parent.Parent?.Name != "shared" ||
            !Path.GetFullPath(coreLibrary).Equals(Path.Combine(runtime.FullName, "System.Private.CoreLib.dll"), StringComparison.OrdinalIgnoreCase))
            throw new WorkerDeploymentException(ReasonCode.ApiUnavailable, "Unrecognized parent runtime layout.");
        var host = Path.Combine(runtime.Parent.Parent.Parent!.FullName, "dotnet.exe");
        if (!File.Exists(host) || !File.Exists(coreLibrary)) Reject("Matching runtime host missing.");
        return (host, version);
    }
    internal static string ReadMvid(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("D");
    }
    private static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static void Reject(string message) => throw new WorkerDeploymentException(ReasonCode.InvalidValue, message);
}
