using System.Diagnostics;
using System.Text.Json;
using WinGPUDoctor.Supervisor;

namespace WinGPUDoctor.Cli;

// Private opt-in harness hook, deliberately absent from public CLI arguments/help.
// Normal execution has no progress sink, metadata queries or progress output.
internal sealed class M4CollectionProbe
{
    private readonly Dictionary<string, long> _starts = [];
    private readonly long _cliStart;
    private int _sequence;

    private M4CollectionProbe()
    {
        using var process = Process.GetCurrentProcess();
        _cliStart = process.StartTime.ToUniversalTime().Ticks;
    }

    internal static Action<CollectionProgress>? FromEnvironment() =>
        Environment.GetEnvironmentVariable("WINGPUDOCTOR_M4_PROCESS_PROBE") == "1" ? new M4CollectionProbe().Record : null;

    private void Record(CollectionProgress progress)
    {
        if (progress.Kind == CollectionProgressKind.WorkerStarted)
        {
            using var worker = Process.GetProcessById(progress.WorkerPid);
            // This worker is still awaiting Request. Capture birth identity before provider entry.
            _starts[progress.Operation] = worker.StartTime.ToUniversalTime().Ticks;
        }
        Console.Error.WriteLine("WGD-M4-PROBE " + JsonSerializer.Serialize(new
        {
            Sequence = ++_sequence,
            Kind = progress.Kind.ToString(),
            progress.Operation,
            progress.WorkerPid,
            WorkerStartTimeUtcTicks = _starts.GetValueOrDefault(progress.Operation),
            CliPid = Environment.ProcessId,
            CliStartTimeUtcTicks = _cliStart,
            progress.Attempt,
            MonotonicTimestamp = Stopwatch.GetTimestamp(),
            MonotonicFrequency = Stopwatch.Frequency
        }));
    }
}
