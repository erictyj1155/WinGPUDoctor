using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class CalibrationTests
{
    private static readonly CollectionTimingPolicy CalibrationPolicy = new(
        OverallBudget: TimeSpan.FromSeconds(60),
        OperationBudget: TimeSpan.FromSeconds(15),
        CleanupAllowance: TimeSpan.FromSeconds(2),
        LaterOperationReservation: TimeSpan.FromSeconds(2),
        FinalBookkeepingReserve: TimeSpan.FromMilliseconds(500),
        ConnectBudget: TimeSpan.FromSeconds(5),
        FrameBudget: TimeSpan.FromSeconds(15));

    [Fact]
    public async Task M4CalibrationWhenRequested()
    {
        if (Environment.GetEnvironmentVariable("WINGPUDOCTOR_M4_CALIBRATION") != "1") return;
        var directory = Environment.GetEnvironmentVariable("WINGPUDOCTOR_M4_CALIBRATION_DIR");
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Calibration directory is required.");
        Directory.CreateDirectory(directory);

        var synthetic = new List<object>();
        foreach (var scenario in new[] { "immediate-success", "delayed-completion", "result-then-exit",
            "result-then-remain-alive", "blocked", "partial-frame" })
        {
            for (var run = 1; run <= 3; run++)
                synthetic.Add(await RunSyntheticAsync(scenario, run));
        }

        var healthy = new List<object>();
        for (var run = 1; run <= 6; run++)
            healthy.Add(await RunHealthyAsync(run));

        var output = new
        {
            GeneratedOnUtc = DateTime.UtcNow.ToString("o"),
            SampleScope = "one laptop, unchanged configuration, standard-user read-only engineering sample",
            Synthetic = synthetic,
            Healthy = healthy
        };
        File.WriteAllText(Path.Combine(directory, "calibration.json"),
            JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    }

    private static async Task<object> RunSyntheticAsync(string scenario, int run)
    {
        var timing = new CollectionTimingCollector();
        var descriptor = WorkerDeployment.Resolve();
        var launch = Stopwatch.StartNew();
        var process = await NativeWorkerLauncher.LaunchAsync(descriptor,
            new(scenario switch { "result-then-exit" => "immediate-success",
                "result-then-remain-alive" => "complete-frame-then-remain-alive",
                "blocked" => "block", "partial-frame" => "partial-frame-then-block", _ => scenario },
                DelayMilliseconds: scenario == "delayed-completion" ? 150 : 0),
            Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)),
            Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(6)), TimeSpan.FromSeconds(2), new HostAdmission());
        timing.Record(CollectionTimingStage.ProcessCreation, "synthetic", launch.Elapsed);
        var session = new NativeWorkerSession(process, descriptor.Identity, timing);
        var operation = WorkerOperation.WmiOperatingSystem;
        var total = Stopwatch.StartNew();
        await session.SendAsync(new RequestFrame(operation), Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)), default);
        Assert.IsType<ReadyFrame>(await session.ReceiveAsync(Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)), default));
        await session.SendAsync(new StartFrame(operation, new WorkerRequestPayload(null)),
            Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)), default);
        Assert.IsType<AttemptStartedFrame>(await session.ReceiveAsync(Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)), default));
        if (scenario is "immediate-success" or "delayed-completion" or "result-then-exit" or "result-then-remain-alive")
            Assert.IsType<ResultFrame>(await session.ReceiveAsync(Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)), default));
        else if (scenario == "partial-frame")
            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await session.ReceiveAsync(Deadline.After(TimeProvider.System, TimeSpan.FromMilliseconds(100)), default));
        var cleanup = await session.CleanupAsync(Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(3)));
        Assert.True(cleanup);
        session.Dispose();
        total.Stop();
        return new { Scenario = scenario, Run = run, TotalMs = total.Elapsed.TotalMilliseconds,
            Timing = Timing(timing) };
    }

    private static async Task<object> RunHealthyAsync(int run)
    {
        var timing = new CollectionTimingCollector();
        var collector = new SupervisedWindowsCollector(CalibrationPolicy,
            TimeProvider.System, null, new HostAdmission(), timing);
        var watch = Stopwatch.StartNew();
        var snapshot = await collector.CollectAsync();
        watch.Stop();
        return new
        {
            Run = run,
            TotalMs = watch.Elapsed.TotalMilliseconds,
            GpuCount = snapshot.Facts.Gpus.Value?.Count ?? 0,
            DisplayCount = snapshot.Facts.Displays.Value?.Count ?? 0,
            Incomplete = snapshot.Collection.Any(runMeta => runMeta.IsIncomplete()),
            Statuses = snapshot.Collection.Select(runMeta => new { Source = runMeta.Source.ToString(), runMeta.Status, runMeta.Reason }).ToArray(),
            Timing = Timing(timing)
        };
    }

    private static object[] Timing(CollectionTimingCollector timing) =>
        timing.Samples.Select(sample => (object)new
        {
            Stage = sample.Stage.ToString(),
            sample.Operation,
            Milliseconds = sample.Duration.TotalMilliseconds
        }).ToArray();
}
