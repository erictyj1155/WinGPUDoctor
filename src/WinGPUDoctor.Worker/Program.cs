using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;

namespace WinGPUDoctor.Worker;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Development fingerprint probe only: the selected host executes this with the CLI's
        // runtimeconfig/deps. No provider, supervisor, pipe or native collector is constructed.
        if (args is ["--execution-identity"])
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                HostPath = Environment.ProcessPath,
                RuntimeVersion = Environment.Version.ToString(),
                RuntimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                CoreLibrary = typeof(object).Assembly.Location
            }));
            return 0;
        }
        if (!TryParseOptions(args, out var scenario, out var delayMilliseconds, out var sentinel))
            return 2;
        try
        {
            using var input = Console.OpenStandardInput();
            using var output = Console.OpenStandardOutput();
            return Run(input, output, scenario, delayMilliseconds, sentinel);
        }
        catch
        {
            // Worker diagnostics never cross the protocol or console.
            return 1;
        }
    }

    private static bool TryParseOptions(string[] args, out string? scenario, out int delayMilliseconds, out long sentinel)
    {
        scenario = null;
        delayMilliseconds = 0;
        sentinel = 0;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--synthetic-scenario" when i + 1 < args.Length:
                    scenario = args[++i];
                    break;
                case "--delay-ms" when i + 1 < args.Length && int.TryParse(args[++i], out var delay) && delay is >= 0 and <= 60_000:
                    delayMilliseconds = delay;
                    break;
                case "--sentinel" when i + 1 < args.Length && long.TryParse(args[++i], out sentinel) && sentinel > 0:
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    private static int Run(Stream input, Stream output, string? scenario, int delayMilliseconds, long sentinel)
    {
        var validator = new ProtocolSequenceValidator();
        var requestFrame = ProtocolFraming.ReadAsync(input, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        if (requestFrame is not RequestFrame request) return 3;
        validator.Record(request);
        var identity = new WorkerBuildIdentity(ProtocolConstants.Version, Environment.Version.ToString(),
            typeof(Program).Assembly.ManifestModule.ModuleVersionId.ToString("D"),
            typeof(RequestFrame).Assembly.ManifestModule.ModuleVersionId.ToString("D"),
            typeof(CollectorRun).Assembly.ManifestModule.ModuleVersionId.ToString("D"),
            typeof(Windows.WindowsCollector).Assembly.ManifestModule.ModuleVersionId.ToString("D"));
        var ready = new ReadyFrame(request.Operation, identity);
        ProtocolFraming.WriteAsync(output, ready, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        validator.Record(ready);

        var startFrame = ProtocolFraming.ReadAsync(input, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        if (startFrame is not StartFrame start || start.Operation != request.Operation) return 3;
        validator.Record(start);
        if (scenario is not null)
        {
            var attempt = new AttemptStartedFrame(request.Operation);
            ProtocolFraming.WriteAsync(output, attempt, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            validator.Record(attempt);
            return RunSynthetic(output, scenario, delayMilliseconds, sentinel, request.Operation);
        }

        WorkerOperationRunner.Execute(WorkerOperationDispatcher.CreateLocal(), request.Operation, start.Payload, (frame, bytes) =>
        {
            validator.Record(frame);
            output.Write(bytes);
            output.Flush();
        });
        return 0;
    }

    private static int RunSynthetic(Stream output, string scenario, int delayMilliseconds, long sentinel,
        WorkerOperation operation)
    {
        switch (scenario)
        {
            case "sentinel":
                if (sentinel <= 0 || SetEvent(new IntPtr(sentinel))) return 7;
                WriteResult(output, SuccessResult(operation));
                return 0;
            case "block":
                Thread.Sleep(Timeout.Infinite);
                return 0;
            case "partial-frame-then-block":
                WritePartialFrameAndBlock(output);
                return 0;
            case "malformed-frame":
                WriteRawFrame(output, [0x7b]);
                return 0;
            case "abnormal-exit":
                return 7;
            case "complete-frame-then-remain-alive":
                WriteResult(output, SuccessResult(operation));
                Thread.Sleep(Timeout.Infinite);
                return 0;
            case "delayed-completion":
                if (delayMilliseconds > 0) Thread.Sleep(delayMilliseconds);
                WriteResult(output, SuccessResult(operation));
                return 0;
            case "immediate-failure":
                WriteResult(output, operation == WorkerOperation.DisplayActiveTopology
                    ? new ResultFrame(operation, WorkerResultState.Failed, ReasonCode.NativeError,
                        new(null, null, null, null, new(
                            Observation<IReadOnlyList<DisplayFacts>>.Absent(DataState.Failed, DataSource.DisplayConfig, ReasonCode.NativeError),
                            new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Failed, ReasonCode.NativeError)
                            { Issues = [new(CollectionOperation.QueryPaths, ReasonCode.NativeError, null)] })))
                    : new ResultFrame(operation, WorkerResultState.Failed, ReasonCode.ProviderUnavailable, null));
                return 0;
            case "immediate-success":
                WriteResult(output, SuccessResult(operation));
                return 0;
            default:
                return 2;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetEvent(IntPtr handle);

    private static void WriteResult(Stream stream, ResultFrame frame)
    {
        ProtocolFraming.WriteAsync(stream, frame, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    private static void WriteRawFrame(Stream stream, byte[] payload)
    {
        var prefix = BitConverter.GetBytes((uint)payload.Length);
        if (!BitConverter.IsLittleEndian) Array.Reverse(prefix);
        stream.Write(prefix, 0, prefix.Length);
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }

    private static void WritePartialFrameAndBlock(Stream stream)
    {
        // Declare a larger frame than is written; the parent must time out without freeing buffers.
        var prefix = BitConverter.GetBytes((uint)4096);
        if (!BitConverter.IsLittleEndian) Array.Reverse(prefix);
        stream.Write(prefix, 0, prefix.Length);
        stream.Write([0x7b], 0, 1);
        stream.Flush();
        Thread.Sleep(Timeout.Infinite);
    }

    private static ResultFrame SuccessResult(WorkerOperation operation)
    {
        var payload = operation switch
        {
            WorkerOperation.WmiOperatingSystem => new WorkerResultPayload(
                new WmiOperatingSystemResult(
                    Observation<string>.Known("10.0.26200", DataSource.WmiOperatingSystem),
                    Observation<string>.Known("26200", DataSource.WmiOperatingSystem)),
                null, null, null, null),
            WorkerOperation.WmiComputerSystem => new WorkerResultPayload(
                null,
                new WmiComputerSystemResult(
                    Observation<string>.Known("Example OEM", DataSource.WmiComputerSystem),
                    Observation<string>.Known("Example Model", DataSource.WmiComputerSystem)),
                null, null, null),
            WorkerOperation.WmiVideoControllers => new WorkerResultPayload(
                null, null,
                new WmiVideoControllersResult(Observation<IReadOnlyList<WmiVideoControllerFact>>.Known(
                    [new WmiVideoControllerFact("synthetic-device", Observation<string>.Known("Example GPU", DataSource.WmiVideoController),
                        Observation<string>.Known("10DE", DataSource.WmiVideoController),
                        Observation<string>.Known("1234", DataSource.WmiVideoController))], DataSource.WmiVideoController)),
                null, null),
            WorkerOperation.WmiDisplayDrivers => new WorkerResultPayload(
                null, null, null,
                new WmiDisplayDriversResult(Observation<IReadOnlyList<WmiDisplayDriverFact>>.Known(
                    [new WmiDisplayDriverFact("synthetic-device", Observation<string>.Known("Example Provider", DataSource.WmiSignedDriver),
                        Observation<string>.Known("1.2.3.4", DataSource.WmiSignedDriver),
                        Observation<string>.Known("2026-09-13", DataSource.WmiSignedDriver))], DataSource.WmiSignedDriver)),
                null),
            WorkerOperation.DisplayActiveTopology => new WorkerResultPayload(
                null, null, null, null,
                new DisplayActiveTopologyResult(Observation<IReadOnlyList<DisplayFacts>>.Known([], DataSource.DisplayConfig),
                    new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Succeeded, ReasonCode.None)
                    { QueryMode = DisplayQueryMode.VirtualModeAndRefreshAware })),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        return new ResultFrame(operation, WorkerResultState.Succeeded, ReasonCode.None, payload);
    }
}
