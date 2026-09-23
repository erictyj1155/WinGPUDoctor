using System.Runtime.InteropServices;

namespace WinGPUDoctor.Supervisor;

internal readonly record struct ParentSecurityContext(bool Elevated, uint IntegrityRid)
{
    internal static T BeforeLaunch<T>(Func<ParentSecurityContext> inspect, Func<T> launch)
    {
        ParentSecurityContext context;
        try { context = inspect(); }
        catch { throw new WorkerAdmissionException(AdmissionFailure.SecurityContext); }
        if (context.Elevated || context.IntegrityRid > 0x2000)
            throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
        return launch();
    }

    internal static ParentSecurityContext Read()
    {
        // Borrowed current-process pseudo-handle is never wrapped or closed.
        if (!NativeMethods.OpenProcessToken(new IntPtr(-1), NativeMethods.TokenQuery, out var raw))
            throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
        using var token = new SafeKernelHandle(raw);
        using var tokenReference = new SafeHandleLease(token);
        const int capacity = 4096;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            if (!NativeMethods.GetTokenInformation(tokenReference.Value, NativeMethods.TokenElevation,
                    buffer, 4, out var length) || length != 4)
                throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
            var elevated = Marshal.ReadInt32(buffer) != 0;
            if (!NativeMethods.GetTokenInformation(tokenReference.Value, NativeMethods.TokenIntegrityLevel,
                    buffer, capacity, out length) || length > capacity || length < Marshal.SizeOf<NativeMethods.TokenMandatoryLabel>())
                throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
            var sid = Marshal.PtrToStructure<NativeMethods.TokenMandatoryLabel>(buffer).Sid;
            var offset = sid.ToInt64() - buffer.ToInt64();
            if (offset < Marshal.SizeOf<NativeMethods.TokenMandatoryLabel>() || offset > length - 12)
                throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
            // Integrity SID must be S-1-16-RID: revision 1, one subauthority, mandatory authority 16.
            if (Marshal.ReadByte(sid, 0) != 1 || Marshal.ReadByte(sid, 1) != 1 ||
                Enumerable.Range(2, 5).Any(i => Marshal.ReadByte(sid, i) != 0) || Marshal.ReadByte(sid, 7) != 16)
                throw new WorkerAdmissionException(AdmissionFailure.SecurityContext);
            return new(elevated, unchecked((uint)Marshal.ReadInt32(sid, 8)));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
}
