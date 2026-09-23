using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WinGPUDoctor.Supervisor;

internal sealed class PipeSecurityDescriptor : IDisposable
{
    private readonly SafeLocalAlloc _descriptor;

    private PipeSecurityDescriptor(SafeLocalAlloc descriptor) => _descriptor = descriptor;

    internal NativeMethods.SecurityAttributes Attributes { get; private set; }

    internal static PipeSecurityDescriptor CreateCurrentUser(bool inheritable)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("Current user SID is unavailable.");
        var sddl = $"D:P(A;;GA;;;{user.Value})";
        if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl,
            NativeMethods.SDDL_REVISION_1, out var descriptor, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Pipe security descriptor creation failed.");
        var safe = new SafeLocalAlloc(descriptor);
        var result = new PipeSecurityDescriptor(safe)
        {
            Attributes = new()
            {
                Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>(),
                SecurityDescriptor = safe.DangerousGetHandle(),
                InheritHandle = inheritable ? 1 : 0
            }
        };
        return result;
    }

    public void Dispose() => _descriptor.Dispose();
}
