using System.Text;
using WinGPUDoctor.Cli;
using WinGPUDoctor.Core;
using WinGPUDoctor.Supervisor;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 1 && args[0] is "--help" or "-h")
    {
        Console.WriteLine("WinGPUDoctor 0.2.0-poc — read-only GPU inventory and active display paths\nUsage: wingpudoctor [--format markdown|json] [--output FILE] [--yes]\nDefault: sanitized Markdown preview on stdout; no file is written.\n--output: preview on stderr, then type EXPORT to save that exact snapshot.\n--yes: explicitly accept export without an interactive prompt; requires --output.\nExisting files are never overwritten. UNC/device paths are rejected. No upload.\nTopology does not identify the GPU used by applications.\nExit codes: 0 complete; 2 arguments/platform; 3 partial/failed collection; 4 export declined; 5 export failed.");
        return 0;
    }
    var format = "markdown";
    string? output = null;
    var yes = false;
    var seen = new HashSet<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (!seen.Add(args[i])) return Usage();
        switch (args[i])
        {
            case "--format" when i + 1 < args.Length: format = args[++i]; break;
            case "--output" when i + 1 < args.Length: output = args[++i]; break;
            case "--yes": yes = true; break;
            default: return Usage();
        }
    }
    if (format is not ("json" or "markdown") || (yes && output is null) || (output is not null && string.IsNullOrWhiteSpace(output))) return Usage();
    if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Windows is required for live collection."); return 2; }
    string? fullPath = null;
    if (output is not null)
    {
        try
        {
            fullPath = Path.GetFullPath(output);
            // Reject network/device namespace paths and alternate data streams before any file access.
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || fullPath[2..].Contains(':')) return Usage();
            if (new DriveInfo(Path.GetPathRoot(fullPath)!).DriveType != DriveType.Fixed)
            { Console.Error.WriteLine("Export requires a local fixed drive."); return 2; }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Invalid local export destination."); return 2; }
    }
    using var consoleCancellation = new ConsoleCancellation();
    consoleCancellation.Register();
    var collector = new SupervisedWindowsCollector(progress: M4CollectionProbe.FromEnvironment());
    return await CollectionOutput.RunAsync(collector.CollectAsync,
        consoleCancellation.Controller, consoleCancellation.Unregister,
        snapshot => WriteReport(snapshot, format, fullPath, yes), Console.Error.WriteLine);
}

static int WriteReport(CollectionSnapshot snapshot, string format, string? fullPath, bool yes)
{
    var report = PrivacyPolicy.Prepare(snapshot, DateOnly.FromDateTime(DateTime.UtcNow));
    var content = format == "json" ? ReportWriter.Json(report) : ReportWriter.Markdown(report);
    if (fullPath is null) Console.Write(content);
    else
    {
        Console.Error.Write(ReportWriter.Markdown(report));
        if (!yes)
        {
            if (Console.IsInputRedirected)
            { Console.Error.WriteLine("Export requires an interactive review or explicit --yes. No file written."); return 4; }
            Console.Error.Write("Review the snapshot above. Type EXPORT to save it: ");
            if (Console.ReadLine() != "EXPORT") { Console.Error.WriteLine("Export declined. No file written."); return 4; }
        }
        try
        {
            using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        { Console.Error.WriteLine("Export failed. Check the local destination; existing files are not overwritten. A new partial file may remain after a write failure."); return 5; }
        Console.Error.WriteLine("Report saved locally. Nothing was uploaded.");
    }
    return snapshot.Collection.Any(c => c.IsIncomplete()) ? 3 : 0;
}

static int Usage() { Console.Error.WriteLine("Invalid arguments. Use --help."); return 2; }

internal sealed class ConsoleCancellation : IDisposable
{
    private readonly HostCancellationController _controller = new();
    private bool _registered;

    internal HostCancellationController Controller => _controller;

    internal void Register()
    {
        if (_registered) return;
        try
        {
            Console.CancelKeyPress += HandleCancel;
            _registered = true;
        }
        catch (PlatformNotSupportedException)
        {
            // Non-console hosts have no console cancellation event.
        }
    }

    internal void Unregister()
    {
        if (!_registered) return;
        Console.CancelKeyPress -= HandleCancel;
        _registered = false;
    }

    private void HandleCancel(object? sender, ConsoleCancelEventArgs args) =>
        args.Cancel = _controller.Interrupt() == HostInterruptResult.Controlled;

    public void Dispose()
    {
        Unregister();
        _controller.Dispose();
    }
}
