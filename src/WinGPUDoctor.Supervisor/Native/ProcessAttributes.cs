using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WinGPUDoctor.Supervisor;

// Fault seam covers ownership transitions, not arbitrary production launch behavior.
internal interface IAttributeListApi
{
    nuint Size();
    IntPtr Allocate(int size);
    bool Initialize(IntPtr pointer, ref nuint size);
    bool Update(IntPtr pointer, IntPtr key, IntPtr value, nuint size);
    void Delete(IntPtr pointer);
    void Free(IntPtr pointer);
}
internal sealed class AttributeListApi : IAttributeListApi
{
    public nuint Size()
    {
        nuint size = 0;
        var result = NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
        if (result || Marshal.GetLastWin32Error() != 122 || size == 0 || size > 65536)
            throw new Win32Exception("Attribute sizing failed.");
        return size;
    }
    public IntPtr Allocate(int size) => Marshal.AllocHGlobal(size);
    public bool Initialize(IntPtr pointer, ref nuint size) => NativeMethods.InitializeProcThreadAttributeList(pointer, 2, 0, ref size);
    public bool Update(IntPtr pointer, IntPtr key, IntPtr value, nuint size) =>
        NativeMethods.UpdateProcThreadAttribute(pointer, 0, key, value, size, IntPtr.Zero, IntPtr.Zero);
    public void Delete(IntPtr pointer) => NativeMethods.DeleteProcThreadAttributeList(pointer);
    public void Free(IntPtr pointer) => Marshal.FreeHGlobal(pointer);
}

internal sealed class ProcessAttributes : IDisposable
{
    private readonly List<SafeHandleLease> _references = [];
    private IntPtr _jobValue;
    private IntPtr _handleValues;
    private SafeProcThreadAttributeList? _list;
    internal IntPtr Pointer => _list!.DangerousGetHandle();
    internal ProcessAttributes(SafeKernelHandle job, SafeKernelHandle client, SafeKernelHandle nul,
        IAttributeListApi? api = null)
    {
        api ??= new AttributeListApi();
        try
        {
            foreach (var handle in new[] { job, client, nul }) _references.Add(new SafeHandleLease(handle));
            _jobValue = Marshal.AllocHGlobal(IntPtr.Size);
            _handleValues = Marshal.AllocHGlobal(2 * IntPtr.Size);
            Marshal.WriteIntPtr(_jobValue, _references[0].Value);
            Marshal.WriteIntPtr(_handleValues, _references[1].Value);
            Marshal.WriteIntPtr(_handleValues, IntPtr.Size, _references[2].Value);
            var size = api.Size();
            _list = new SafeProcThreadAttributeList(checked((int)size), api);
            if (!api.Initialize(Pointer, ref size)) throw new Win32Exception("Attribute initialization failed.");
            _list.MarkInitialized();
            if (!api.Update(Pointer, NativeMethods.ProcThreadAttributeJobList, _jobValue, (nuint)IntPtr.Size) ||
                !api.Update(Pointer, NativeMethods.ProcThreadAttributeHandleList, _handleValues, (nuint)(2 * IntPtr.Size)))
                throw new Win32Exception("Attribute update failed.");
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        _list?.Dispose();
        _list = null;
        if (_handleValues != IntPtr.Zero) { Marshal.FreeHGlobal(_handleValues); _handleValues = IntPtr.Zero; }
        if (_jobValue != IntPtr.Zero) { Marshal.FreeHGlobal(_jobValue); _jobValue = IntPtr.Zero; }
        foreach (var reference in _references) reference.Dispose();
        _references.Clear();
    }
}
