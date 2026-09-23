using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WinGPUDoctor.Supervisor;

internal sealed record WorkerLaunchOptions(string? SyntheticScenario = null, int DelayMilliseconds = 0, long SentinelHandle = 0);

internal sealed class WorkerProcess : IDisposable
{
    private readonly SafeKernelHandle _processHandle;
    private readonly JobObject _job;
    private readonly OverlappedPipeServer _pipe;
    internal WorkerProcess(SafeKernelHandle process, JobObject job, OverlappedPipeServer pipe, int pid)
    { _processHandle = process; _job = job; _pipe = pipe; ProcessId = pid; }
    internal int ProcessId { get; }
    internal OverlappedPipeServer Pipe => _pipe;
    internal bool IsInCreationTimeJob => _job.ContainsProcess(_processHandle);
    internal bool IsElevated => TokenInspector.IsElevated(_processHandle);
    internal bool WaitForExit(TimeSpan timeout, out uint exitCode)
    {
        using var reference = new SafeHandleLease(_processHandle);
        var wait = NativeMethods.WaitForSingleObject(reference.Value, Deadline.FiniteMilliseconds(timeout));
        exitCode = uint.MaxValue;
        if (wait != NativeMethods.WAIT_OBJECT_0) return false;
        return NativeMethods.GetExitCodeProcess(reference.Value, out exitCode);
    }
    internal void RequestTermination() => _ = _job.Terminate(1);
    internal async ValueTask<bool> ConfirmExitAsync(Deadline deadline)
    {
        // Poll the native handle with zero wait; no blocked thread/task owns a process wait.
        while (!deadline.Expired)
        {
            if (WaitForExit(TimeSpan.Zero, out _)) return !deadline.Expired;
            await Task.Delay(deadline.Remaining < TimeSpan.FromMilliseconds(10) ? deadline.Remaining : TimeSpan.FromMilliseconds(10),
                deadline.Clock).ConfigureAwait(false);
        }
        return false;
    }
    public void Dispose() { _pipe.Dispose(); _job.Dispose(); _processHandle.Dispose(); }
}

internal static class TokenInspector
{
    internal static bool IsElevated(SafeKernelHandle process)
    {
        using var processReference = new SafeHandleLease(process);
        if (!NativeMethods.OpenProcessToken(processReference.Value, NativeMethods.TokenQuery, out var raw))
            throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
        using var token = new SafeKernelHandle(raw);
        using var reference = new SafeHandleLease(token);
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            if (!NativeMethods.GetTokenInformation(reference.Value, NativeMethods.TokenElevation, buffer, 4, out var length) || length != 4)
                throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
            return Marshal.ReadInt32(buffer) != 0;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}

internal static class NativeWorkerLauncher
{
    internal static async ValueTask<WorkerProcess> LaunchAsync(WorkerDeploymentDescriptor descriptor, WorkerLaunchOptions? options,
        Deadline connectDeadline, Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission,
        CancellationToken token = default)
    {
        if (admission.IsPoisoned) throw new WorkerAdmissionException(AdmissionFailure.HostPoisoned);
        _ = ParentSecurityContext.BeforeLaunch(ParentSecurityContext.Read, () => true);
        connectDeadline.Check(token);
        OverlappedPipeServer? pipe = null;
        JobObject? job = null;
        try
        {
            pipe = OverlappedPipeServer.Create();
            job = JobObject.Create();
            using var client = pipe.CreateInheritableClientHandle();
            await pipe.AcceptClientAsync(connectDeadline, token).ConfigureAwait(false);
            using var nul = CreateNullHandle();
            var environment = BuildEnvironmentBlock(descriptor.HostPath);
            try
            {
                using var attributes = new ProcessAttributes(job.Handle, client, nul);
                var startup = new NativeMethods.StartupInfoEx
                {
                    StartupInfo = new()
                    {
                        Size = Marshal.SizeOf<NativeMethods.StartupInfoEx>(), Flags = (int)NativeMethods.STARTF_USESTDHANDLES,
                        StdInput = client.DangerousGetHandle(), StdOutput = client.DangerousGetHandle(), StdError = nul.DangerousGetHandle()
                    },
                    AttributeList = attributes.Pointer
                };
                connectDeadline.Check(token);
                if (admission.IsPoisoned) throw new WorkerAdmissionException(AdmissionFailure.HostPoisoned);
                if (!NativeMethods.CreateProcessW(descriptor.HostPath, BuildCommandLine(descriptor, options), IntPtr.Zero, IntPtr.Zero, true,
                    NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_NO_WINDOW,
                    environment, descriptor.WorkingDirectory, ref startup, out var information))
                    throw new WorkerAdmissionException(AdmissionFailure.Launch);
                using var thread = new SafeKernelHandle(information.Thread);
                return new(new SafeKernelHandle(information.Process), job, pipe, information.ProcessId);
                // Attribute list -> backing values/references -> client/NUL copies (using scopes).
            }
            finally { Marshal.FreeHGlobal(environment); }
        }
        catch
        {
            var cleanup = cleanupLimit.Clip(cleanupAllowance);
            try
            {
                var resolved = pipe is null || await pipe.DrainAsync(cleanup).ConfigureAwait(false);
                if (resolved) { pipe?.Dispose(); job?.Dispose(); }
                else admission.Poison(new object?[] { pipe, job });
            }
            catch { admission.Poison(new object?[] { pipe, job }); }
            throw;
        }
    }
    private static SafeKernelHandle CreateNullHandle()
    {
        var attributes = new NativeMethods.SecurityAttributes { Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>(), InheritHandle = 1 };
        var raw = NativeMethods.CreateFileW("NUL", NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, ref attributes, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
        var handle = new SafeKernelHandle(raw);
        if (handle.IsInvalid) { handle.Dispose(); throw new WorkerAdmissionException(AdmissionFailure.Launch); }
        return handle;
    }
    internal static StringBuilder BuildCommandLine(WorkerDeploymentDescriptor descriptor, WorkerLaunchOptions? options)
    {
        var args = new List<string> { descriptor.HostPath, "exec", "--fx-version", descriptor.RuntimeVersion,
            "--roll-forward", "Disable", descriptor.WorkerPath };
        if (options?.SyntheticScenario is { } scenario) { args.Add("--synthetic-scenario"); args.Add(scenario); }
        if (options is { DelayMilliseconds: > 0 }) { args.Add("--delay-ms"); args.Add(options.DelayMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        if (options is { SentinelHandle: > 0 }) { args.Add("--sentinel"); args.Add(options.SentinelHandle.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        return new(string.Join(' ', args.Select(Quote)));
    }
    internal static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private static IntPtr BuildEnvironmentBlock(string hostPath)
    {
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "SystemRoot", "windir", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value) values[name] = value;
        values["DOTNET_ROOT"] = Path.GetDirectoryName(hostPath)!;
        values["DOTNET_ROOT_" + RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()] = Path.GetDirectoryName(hostPath)!;
        values["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        values["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false";
        values["DOTNET_EnableDiagnostics"] = "0";
        return Marshal.StringToHGlobalUni(string.Join('\0', values.Select(p => p.Key + "=" + p.Value)) + "\0\0");
    }
}
