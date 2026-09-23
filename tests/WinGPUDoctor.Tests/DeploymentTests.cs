using System.Runtime.InteropServices;
using WinGPUDoctor.Supervisor;
using Xunit;
namespace WinGPUDoctor.Tests;
public class DeploymentTests
{
    [Fact]
    public void LoadedParentIdentityMustMatchDeployedSharedBuild()
    {
        var d = WorkerDeployment.Resolve();
        var loaded = typeof(WinGPUDoctor.Core.CollectorRun).Assembly;
        var entry = d.Manifest.Entries.Single(e => e.AssemblyName == loaded.GetName().Name);
        WorkerDeployment.ValidateLoadedIdentity(loaded, entry);
        foreach (var wrong in new[] { entry with { Mvid = Guid.NewGuid().ToString("D") }, entry with { Sha256 = new string('0', 64) },
            entry with { AssemblyVersion = "99.0.0.0" }, entry with { AssemblyName = "WinGPUDoctor.Windows" } })
            Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.ValidateLoadedIdentity(loaded, wrong));
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("extra")]
    [InlineData("mixed")]
    public void RecursiveClosureRejectsMissingChangedExtraAndMixedOutput(string fault)
    {
        var descriptor = WorkerDeployment.Resolve();
        var root = Path.Combine(Path.GetTempPath(), "wingpudoctor-synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var entry in descriptor.Manifest.Entries)
            {
                var target = Path.Combine(root, entry.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(descriptor.WorkingDirectory, entry.RelativePath), target);
            }
            WorkerDeployment.Validate(root, descriptor.Manifest);
            var nested = descriptor.Manifest.Entries.Single(e => e.RelativePath.Replace('\\', '/').EndsWith("runtimes/win/lib/net10.0/System.Management.dll", StringComparison.Ordinal));
            var path = Path.Combine(root, nested.RelativePath);
            switch (fault)
            {
                case "missing": File.Delete(path); break;
                case "changed": File.WriteAllBytes(path, [1, 2, 3]); break;
                case "extra": File.WriteAllBytes(Path.Combine(root, "runtimes", "stale.dll"), [1]); break;
                case "mixed": File.Copy(Path.Combine(root, "WinGPUDoctor.Core.dll"), Path.Combine(root, "WinGPUDoctor.Windows.dll"), true); break;
            }
            Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.Validate(root, descriptor.Manifest));
        }
        finally { Directory.Delete(root, true); } // Test owns this fresh, synthetic-only directory.
    }
    [Fact]
    public void NormalizedDuplicatesTraversalAndMvidMismatchAreRejected()
    {
        var d = WorkerDeployment.Resolve(); var entries = d.Manifest.Entries;
        var nested = entries.First(e => e.RelativePath.Contains('/'));
        Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.Validate(d.WorkingDirectory,
            new([.. entries, nested with { RelativePath = nested.RelativePath.Replace('/', '\\').ToUpperInvariant() }])));
        foreach (var path in new[] { "../escape", "a/../escape", "C:/escape", "a//b", "a./b", "a\\..\\b" })
            Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.Normalize(path));
        var changed = entries.Select(e => e.AssemblyName == "WinGPUDoctor.Core" ? e with { Mvid = Guid.NewGuid().ToString("D") } : e).ToArray();
        Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.Validate(d.WorkingDirectory, new(changed)));
    }
    [Fact]
    public void RuntimeMustBeExactRecognizedLayoutAndCommandLinePinsSelection()
    {
        var d = WorkerDeployment.Resolve(); var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var actual = WorkerDeployment.ResolveRuntime(runtime, typeof(object).Assembly.Location, Environment.Version.ToString());
        Assert.Equal(d.HostPath, actual.Host);
        Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.ResolveRuntime(runtime, typeof(object).Assembly.Location, "99.0.0"));
        Assert.Throws<WorkerDeploymentException>(() => WorkerDeployment.ResolveRuntime(AppContext.BaseDirectory, typeof(object).Assembly.Location, Environment.Version.ToString()));
        var command = NativeWorkerLauncher.BuildCommandLine(d, null).ToString();
        Assert.Contains("\"exec\" \"--fx-version\" \"" + Environment.Version + "\" \"--roll-forward\" \"Disable\"", command);
        Assert.Equal("\"C:\\some dir\\\\\"", NativeWorkerLauncher.Quote("C:\\some dir\\"));
        Assert.Equal("\"a\\\"b\"", NativeWorkerLauncher.Quote("a\"b"));
    }
}
