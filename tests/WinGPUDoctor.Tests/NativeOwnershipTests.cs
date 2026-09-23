using System.Runtime.InteropServices;
using WinGPUDoctor.Supervisor;
using Xunit;
namespace WinGPUDoctor.Tests;
public class NativeOwnershipTests
{
    [Fact]
    public void CurrentParentTokenCanBeReadWithoutChangingPrivileges()
    {
        Assert.True(NativeMethods.OpenProcessToken(new IntPtr(-1), NativeMethods.TokenQuery, out var raw));
        using var token = new SafeKernelHandle(raw);
        var buffer = Marshal.AllocHGlobal(4096);
        try
        {
            var success = NativeMethods.GetTokenInformation(raw, NativeMethods.TokenElevation, buffer, 4, out var length);
            Assert.True(success, "GetTokenInformation error " + Marshal.GetLastWin32Error());
            Assert.Equal(4u, length);
        }
        finally { Marshal.FreeHGlobal(buffer); }
        var context = ParentSecurityContext.Read();
        Assert.False(context.Elevated);
        Assert.InRange(context.IntegrityRid, 0u, 0x2000u);
    }
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void AttributeFailuresReleaseInCorrectOrder(int failure, int expectedDelete)
    {
        using var job = new SafeKernelHandle(new IntPtr(10), false);
        using var client = new SafeKernelHandle(new IntPtr(11), false);
        using var nul = new SafeKernelHandle(new IntPtr(12), false);
        var api = new FaultApi(failure);
        Assert.ThrowsAny<Exception>((Action)(() =>
        {
            using var list = new ProcessAttributes(job, client, nul, api);
            throw new InvalidOperationException("synthetic CreateProcess failure");
        }));
        Assert.Equal(expectedDelete, api.Deletes); Assert.Equal(1, api.Frees);
        Assert.Equal(expectedDelete == 0 ? new[] { "free" } : new[] { "delete", "free" }, api.Events);
    }
    [Theory]
    [InlineData(true, 8192)]
    [InlineData(false, 12288)]
    [InlineData(false, 8448)]
    public void ParentSecurityRejectionNeverInvokesLaunch(bool elevated, uint integrity)
    {
        var launches = 0;
        var error = Assert.Throws<WorkerAdmissionException>(() => ParentSecurityContext.BeforeLaunch(
            () => new(elevated, integrity), () => ++launches));
        Assert.Equal(AdmissionFailure.SecurityContext, error.Failure); Assert.Equal(0, launches);
    }
    [Fact]
    public void UnverifiableParentIsRejectedAndMediumParentIsAllowed()
    {
        var launches = 0;
        Assert.Throws<WorkerAdmissionException>(() => ParentSecurityContext.BeforeLaunch(
            () => throw new InvalidOperationException(), () => ++launches));
        Assert.Equal(0, launches);
        Assert.Equal(1, ParentSecurityContext.BeforeLaunch(() => new(false, 8192), () => ++launches));
    }
    private sealed class FaultApi(int failure) : IAttributeListApi
    {
        internal int Deletes, Frees; private int _updates;
        internal List<string> Events = []; private readonly List<IntPtr> _values = [];
        public nuint Size() => 32;
        public IntPtr Allocate(int size) => Marshal.AllocHGlobal(size);
        public bool Initialize(IntPtr pointer, ref nuint size) => failure != 0;
        public bool Update(IntPtr pointer, IntPtr key, IntPtr value, nuint size)
        { _values.Add(value); return ++_updates != failure; }
        public void Delete(IntPtr pointer)
        {
            // Values must still be allocated when Delete is invoked.
            foreach (var value in _values) Assert.NotEqual(IntPtr.Zero, Marshal.ReadIntPtr(value));
            Deletes++; Events.Add("delete");
        }
        public void Free(IntPtr pointer) { Frees++; Events.Add("free"); Marshal.FreeHGlobal(pointer); }
    }
}
