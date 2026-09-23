using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace WinGPUDoctor.Supervisor;

internal sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeKernelHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle) => SetHandle(handle);

    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
}

internal sealed class SafeLocalAlloc : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeLocalAlloc(IntPtr handle) : base(true) => SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        NativeMethods.LocalFree(handle);
        return true;
    }
}

internal sealed class SafeProcThreadAttributeList : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly IAttributeListApi _api;
    private bool _initialized;
    internal SafeProcThreadAttributeList(int size, IAttributeListApi api) : base(true)
    {
        _api = api;
        SetHandle(api.Allocate(size));
    }
    internal void MarkInitialized() => _initialized = true;

    protected override bool ReleaseHandle()
    {
        if (_initialized) _api.Delete(handle);
        _api.Free(handle);
        return true;
    }
}

internal sealed class SafeHandleLease : IDisposable
{
    private readonly SafeHandle _owner;
    private bool _added;
    internal SafeHandleLease(SafeHandle owner)
    {
        _owner = owner;
        owner.DangerousAddRef(ref _added);
    }
    internal IntPtr Value => _owner.DangerousGetHandle();
    public void Dispose()
    {
        if (!_added) return;
        _added = false;
        _owner.DangerousRelease();
    }
}
