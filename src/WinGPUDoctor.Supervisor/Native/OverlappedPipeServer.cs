using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace WinGPUDoctor.Supervisor;

// The stream owns the single SafePipeHandle. PendingIo owns cancellation and completion;
// a timed-out stream is never disposed until DrainAsync confirms ownership is resolved.
internal sealed class OverlappedPipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly NamedPipeServerStream _server;
    private readonly PendingIo _pending = new();
    private bool _closed;
    private OverlappedPipeServer(string name, NamedPipeServerStream server) { _pipeName = name; _server = server; }
    internal bool IsResolved => _pending.IsResolved;
    internal static OverlappedPipeServer Create()
    {
        var name = $@"\\.\pipe\wingpudoctor-{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";
        using var security = PipeSecurityDescriptor.CreateCurrentUser(false);
        var attributes = security.Attributes;
        var raw = NativeMethods.CreateNamedPipeW(name,
            NativeMethods.PIPE_ACCESS_DUPLEX | NativeMethods.FILE_FLAG_OVERLAPPED | NativeMethods.FILE_FLAG_FIRST_PIPE_INSTANCE,
            NativeMethods.PIPE_REJECT_REMOTE_CLIENTS, 1, 0, 0, 0, ref attributes);
        var handle = new SafePipeHandle(raw, ownsHandle: true);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error(); handle.Dispose();
            throw new Win32Exception(error, "Private worker pipe creation failed.");
        }
        try { return new(name, new NamedPipeServerStream(PipeDirection.InOut, true, false, handle)); }
        catch { handle.Dispose(); throw; }
    }
    internal SafeKernelHandle CreateInheritableClientHandle()
    {
        var attributes = new NativeMethods.SecurityAttributes
        { Length = Marshal.SizeOf<NativeMethods.SecurityAttributes>(), InheritHandle = 1 };
        var raw = NativeMethods.CreateFileW(_pipeName, NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            0, ref attributes, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
        var handle = new SafeKernelHandle(raw);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error(); handle.Dispose();
            throw new Win32Exception(error, "Private worker pipe client creation failed.");
        }
        return handle;
    }
    internal async ValueTask AcceptClientAsync(Deadline deadline, CancellationToken token = default) =>
        _ = await _pending.RunAsync(async ct => { await _server.WaitForConnectionAsync(ct).ConfigureAwait(false); return 0; }, deadline, token);
    internal ValueTask<int> ReadAsync(Memory<byte> buffer, Deadline deadline, CancellationToken token) =>
        _pending.RunAsync(ct => _server.ReadAsync(buffer, ct).AsTask(), deadline, token);
    internal async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, Deadline deadline, CancellationToken token) =>
        _ = await _pending.RunAsync(async ct => { await _server.WriteAsync(buffer, ct).ConfigureAwait(false); return 0; }, deadline, token);
    internal void CancelPending() => _pending.RequestCancellation();
    internal ValueTask<bool> DrainAsync(Deadline deadline) => _pending.DrainAsync(deadline);
    public void Dispose()
    {
        if (_closed) return;
        _pending.Dispose(); // Throws before releasing the stream if any I/O is still owned.
        _server.Dispose();
        _closed = true;
    }
}
