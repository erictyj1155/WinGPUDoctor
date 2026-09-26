using WinGPUDoctor.Cli;
using WinGPUDoctor.Core;
using WinGPUDoctor.Host;
using WinGPUDoctor.Supervisor;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 1 && args[0] is "--help" or "-h")
    {
        Console.WriteLine($"WinGPUDoctor {ToolIdentity.Version} — read-only GPU inventory and active display paths\nUsage: wingpudoctor [--format markdown|json] [--output FILE] [--yes]\nDefault: privacy-projected Markdown preview on stdout; no file is written.\n--output: preview on stderr, then type EXPORT to save that exact snapshot.\n--yes: explicitly accept export without an interactive prompt; requires --output.\nReview descriptions before sharing; filtering cannot guarantee anonymity.\nExisting files are never overwritten. UNC/device paths are rejected. No upload.\nTopology does not identify the GPU used by applications.\nExit codes: 0 complete; 2 arguments/platform/destination; 3 incomplete collection (report still shown or saved) or collection stopped with no report (controlled first Ctrl+C or fatal collection failure); 4 export declined; 5 export failed.");
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
    ExportDestination? destination = null;
    if (output is not null)
    {
        destination = HostExport.ResolveDestination(output);
        switch (destination.Status)
        {
            case ExportDestinationStatus.RejectedNamespace: return Usage();
            case ExportDestinationStatus.NonFixedDrive:
                Console.Error.WriteLine("Export requires a local fixed drive."); return 2;
            case ExportDestinationStatus.InvalidLocalDestination:
                Console.Error.WriteLine("Invalid local export destination."); return 2;
        }
    }
    using var scan = new HostScanSession();
    using var consoleCancellation = new ConsoleCancellation(scan);
    consoleCancellation.Register();
    var collector = new SupervisedWindowsCollector(progress: M4CollectionProbe.FromEnvironment());
    var outcome = await scan.RunAsync(collector.CollectAsync, consoleCancellation.Unregister);
    return outcome.Kind switch
    {
        HostScanOutcomeKind.Cancelled => Notice("Collection cancelled. No report was exported."),
        HostScanOutcomeKind.CollectionFailed => Notice("Collection could not complete. No report was exported."),
        _ => WriteReport(outcome, format, destination, yes)
    };
}

static int WriteReport(HostScanOutcome outcome, string format, ExportDestination? destination, bool yes)
{
    var report = outcome.Report!;
    var content = format == "json" ? ReportWriter.Json(report) : ReportWriter.Markdown(report);
    if (destination is null) Console.Write(content);
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
        if (!HostExport.TryWriteNew(destination, content))
        { Console.Error.WriteLine("Export failed. Check the local destination; existing files are not overwritten. A new partial file may remain after a write failure."); return 5; }
        Console.Error.WriteLine("Report saved locally. Nothing was uploaded.");
    }
    return outcome.IsIncomplete ? 3 : 0;
}

static int Notice(string message) { Console.Error.WriteLine(message); return 3; }
static int Usage() { Console.Error.WriteLine("Invalid arguments. Use --help."); return 2; }

internal sealed class ConsoleCancellation : IDisposable
{
    private readonly HostScanSession _scan;
    private bool _registered;

    internal ConsoleCancellation(HostScanSession scan) => _scan = scan;

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
        args.Cancel = _scan.RequestCancellation() == HostInterruptResult.Controlled;

    public void Dispose()
    {
        Unregister();
    }
}
