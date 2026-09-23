using System.Runtime.InteropServices;
using System.Diagnostics;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using Xunit;
namespace WinGPUDoctor.Tests;
public class WorkerProcessTests
{
    [Fact]
    public async Task FailedCreateProcessReleasesAcquiredPipeJobAndAttributes()
    {
        var descriptor = WorkerDeployment.Resolve(); var admission = new HostAdmission();
        var bad = descriptor with { HostPath = Path.Combine(descriptor.WorkingDirectory, "does-not-exist-synthetic.exe") };
        var failure = await Assert.ThrowsAsync<WorkerAdmissionException>(async () => await NativeWorkerLauncher.LaunchAsync(
            bad, new("immediate-success"), Limit(), Limit(6), TimeSpan.FromSeconds(2), admission));
        Assert.Equal(AdmissionFailure.Launch, failure.Failure); Assert.Equal(0, admission.RetainedOwners);
        var subsequent = await Launch("immediate-success");
        try { await Handshake(subsequent); Assert.IsType<ResultFrame>(await subsequent.ReceiveAsync(Limit(), default)); }
        finally { await Cleanup(subsequent); }
    }
    private static Deadline Limit(double seconds = 3) => Deadline.After(TimeProvider.System, TimeSpan.FromSeconds(seconds));
    private static async Task<NativeWorkerSession> Launch(string scenario, int delayMilliseconds = 0, long sentinel = 0)
    {
        var descriptor = WorkerDeployment.Resolve();
        var process = await NativeWorkerLauncher.LaunchAsync(descriptor,
            new(scenario, delayMilliseconds, sentinel),
            Limit(), Limit(6), TimeSpan.FromSeconds(2), new HostAdmission());
        return new NativeWorkerSession(process, descriptor.Identity);
    }
    private static async Task Handshake(NativeWorkerSession session)
    {
        var op = WorkerOperation.WmiOperatingSystem;
        await session.SendAsync(new RequestFrame(op), Limit(), default);
        var ready = Assert.IsType<ReadyFrame>(await session.ReceiveAsync(Limit(), default));
        Assert.Equal(session.ExpectedIdentity, ready.Identity);
        await session.SendAsync(ProtocolTests.Start(op), Limit(), default);
        Assert.IsType<AttemptStartedFrame>(await session.ReceiveAsync(Limit(), default));
    }
    private static async Task Cleanup(NativeWorkerSession s)
    { Assert.True(await s.CleanupAsync(Limit())); s.Dispose(); }
    [Fact]
    public async Task OverlappedReadAndPendingCancellationKeepOwnersAlive()
    {
        using var server = OverlappedPipeServer.Create();
        using var client = server.CreateInheritableClientHandle();
        await server.AcceptClientAsync(Limit(), default);
        using var reference = new SafeHandleLease(client);
        using var borrowed = new Microsoft.Win32.SafeHandles.SafeFileHandle(reference.Value, false);
        using var stream = new FileStream(borrowed, FileAccess.ReadWrite, 1, false);
        var bytes = new byte[4]; var read = server.ReadAsync(bytes, Limit(), default);
        stream.Write(new byte[] { 1, 2, 3, 4 }); Assert.Equal(4, await read);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        await Assert.ThrowsAsync<TimeoutException>(async () => await server.ReadAsync(bytes, Limit(.05), default));
        Assert.True(await server.DrainAsync(Limit()));
    }
    [Theory]
    [InlineData("immediate-success", 0)]
    [InlineData("complete-frame-then-remain-alive", 0)]
    [InlineData("delayed-completion", 150)]
    public async Task SyntheticWorkerIsContainedAndReadyIdentityMatches(string scenario, int delayMilliseconds)
    {
        var session = await Launch(scenario, delayMilliseconds);
        try
        {
            Assert.True(session.IsInCreationTimeJob); Assert.False(session.IsElevated);
            var operation = WorkerOperation.WmiOperatingSystem;
            await session.SendAsync(new RequestFrame(operation), Limit(), default);
            Assert.IsType<ReadyFrame>(await session.ReceiveAsync(Limit(), default));
            var timer = Stopwatch.StartNew();
            await session.SendAsync(ProtocolTests.Start(operation), Limit(), default);
            Assert.IsType<AttemptStartedFrame>(await session.ReceiveAsync(Limit(), default));
            Assert.IsType<ResultFrame>(await session.ReceiveAsync(Limit(), default));
            if (delayMilliseconds > 0)
                Assert.True(timer.ElapsedMilliseconds >= 100,
                    $"Expected delayed child completion, observed {timer.ElapsedMilliseconds} ms.");
        }
        finally { await Cleanup(session); }
    }
    [Theory]
    [InlineData("block")]
    [InlineData("partial-frame-then-block")]
    public async Task BlockedWorkerTimesOutAndCleanupConfirmsIoAndExit(string scenario)
    {
        var s = await Launch(scenario);
        try { await Handshake(s); await Assert.ThrowsAsync<TimeoutException>(async () => await s.ReceiveAsync(Limit(.1), default)); }
        finally { await Cleanup(s); }
    }
    [Theory]
    [InlineData("malformed-frame")]
    [InlineData("abnormal-exit")]
    public async Task MalformedOrExitedWorkerCannotReturnResult(string scenario)
    {
        var s = await Launch(scenario);
        try
        {
            await Handshake(s);
            var exception = await Record.ExceptionAsync(async () => await s.ReceiveAsync(Limit(), default));
            Assert.IsType(scenario == "malformed-frame" ? typeof(ProtocolValidationException) : typeof(EndOfStreamException), exception);
        }
        finally { await Cleanup(s); }
    }
    [Fact]
    public async Task UnrelatedInheritableEventIsExcludedAndRepeatedCleanupWorks()
    {
        var attributes = new NativeMethods.SecurityAttributes { Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>(), InheritHandle = 1 };
        using var sentinel = new SafeKernelHandle(CreateEventW(ref attributes, true, false, null)); Assert.False(sentinel.IsInvalid);
        using var reference = new SafeHandleLease(sentinel);
        for (var i = 0; i < 3; i++)
        {
            var s = await Launch("sentinel", sentinel: reference.Value.ToInt64());
            try { await Handshake(s); Assert.IsType<ResultFrame>(await s.ReceiveAsync(Limit(), default)); }
            finally { await Cleanup(s); }
            Assert.Equal(258u, NativeMethods.WaitForSingleObject(reference.Value, 0));
        }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateEventW(ref NativeMethods.SecurityAttributes attributes,
        [MarshalAs(UnmanagedType.Bool)] bool manual, [MarshalAs(UnmanagedType.Bool)] bool initial, string? name);
}
