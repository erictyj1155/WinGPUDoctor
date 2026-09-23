using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinGPUDoctor.Supervisor;

internal sealed class JobObject : IDisposable
{
    internal SafeKernelHandle Handle { get; }

    private JobObject(SafeKernelHandle handle) => Handle = handle;

    internal static JobObject Create()
    {
        var raw = NativeMethods.CreateJobObjectW(IntPtr.Zero, null);
        var handle = new SafeKernelHandle(raw);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Job object creation failed.");
        }

        var limits = new NativeMethods.JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new()
            {
                LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_ACTIVE_PROCESS |
                    NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                ActiveProcessLimit = 1
            }
        };
        var size = Marshal.SizeOf<NativeMethods.JobObjectExtendedLimitInformation>();
        var pointer = IntPtr.Zero;
        try
        {
            pointer = Marshal.AllocHGlobal(size);
            using var reference = new SafeHandleLease(handle);
            Marshal.StructureToPtr(limits, pointer, false);
            if (!NativeMethods.SetInformationJobObject(reference.Value,
                NativeMethods.JobObjectExtendedLimitInformationClass, pointer, (uint)size))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Job limit configuration failed.");
        }
        catch
        {
            handle.Dispose();
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }

        return new(handle);
    }

    internal bool ContainsProcess(SafeKernelHandle process)
    {
        if (process.IsInvalid) return false;
        using var processReference = new SafeHandleLease(process);
        using var jobReference = new SafeHandleLease(Handle);
        return NativeMethods.IsProcessInJob(processReference.Value, jobReference.Value, out var inJob) && inJob;
    }

    internal bool Terminate(uint exitCode)
    {
        using var reference = new SafeHandleLease(Handle);
        return NativeMethods.TerminateJobObject(reference.Value, exitCode);
    }

    public void Dispose() => Handle.Dispose();
}
