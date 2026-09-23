using System.Runtime.InteropServices;
using WinGPUDoctor.Supervisor;
using Xunit;

namespace WinGPUDoctor.Tests;

public class NativeLayoutTests
{
    [Fact]
    public void WorkerLaunchStructuresMatchWindowsPointerWidths()
    {
        var pointer = IntPtr.Size;
        Assert.Equal(pointer == 8 ? 24 : 12, Marshal.SizeOf<NativeMethods.SecurityAttributes>());
        Assert.Equal(pointer == 8 ? 104 : 68, Marshal.SizeOf<NativeMethods.StartupInfo>());
        Assert.Equal(pointer == 8 ? 112 : 72, Marshal.SizeOf<NativeMethods.StartupInfoEx>());
        Assert.Equal(pointer == 8 ? 24 : 16, Marshal.SizeOf<NativeMethods.ProcessInformation>());
        Assert.Equal(4, Marshal.SizeOf<NativeMethods.TokenElevationType>());
        Assert.Equal(pointer == 8 ? 16 : 8, Marshal.SizeOf<NativeMethods.TokenMandatoryLabel>());
        Assert.Equal(pointer, Marshal.OffsetOf<NativeMethods.TokenMandatoryLabel>(nameof(NativeMethods.TokenMandatoryLabel.Attributes)).ToInt32());
        Assert.Equal(pointer, Marshal.OffsetOf<NativeMethods.SecurityAttributes>(nameof(NativeMethods.SecurityAttributes.SecurityDescriptor)).ToInt32());
        Assert.Equal(pointer * 2, Marshal.OffsetOf<NativeMethods.SecurityAttributes>(nameof(NativeMethods.SecurityAttributes.InheritHandle)).ToInt32());
        Assert.Equal(pointer == 8 ? 80 : 56, Marshal.OffsetOf<NativeMethods.StartupInfo>(nameof(NativeMethods.StartupInfo.StdInput)).ToInt32());
        Assert.Equal(pointer == 8 ? 88 : 60, Marshal.OffsetOf<NativeMethods.StartupInfo>(nameof(NativeMethods.StartupInfo.StdOutput)).ToInt32());
        Assert.Equal(pointer == 8 ? 96 : 64, Marshal.OffsetOf<NativeMethods.StartupInfo>(nameof(NativeMethods.StartupInfo.StdError)).ToInt32());
        Assert.Equal(pointer == 8 ? 104 : 68, Marshal.OffsetOf<NativeMethods.StartupInfoEx>(nameof(NativeMethods.StartupInfoEx.AttributeList)).ToInt32());
        Assert.Equal(pointer * 2, Marshal.OffsetOf<NativeMethods.ProcessInformation>(nameof(NativeMethods.ProcessInformation.ProcessId)).ToInt32());
        if (pointer == 8)
        {
            Assert.Equal(64, Marshal.SizeOf<NativeMethods.JobObjectBasicLimitInformation>());
            Assert.Equal(144, Marshal.SizeOf<NativeMethods.JobObjectExtendedLimitInformation>());
            Assert.Equal(16, Marshal.OffsetOf<NativeMethods.JobObjectBasicLimitInformation>(nameof(NativeMethods.JobObjectBasicLimitInformation.LimitFlags)).ToInt32());
            Assert.Equal(40, Marshal.OffsetOf<NativeMethods.JobObjectBasicLimitInformation>(nameof(NativeMethods.JobObjectBasicLimitInformation.ActiveProcessLimit)).ToInt32());
            Assert.Equal(0, Marshal.OffsetOf<NativeMethods.JobObjectExtendedLimitInformation>(
                nameof(NativeMethods.JobObjectExtendedLimitInformation.BasicLimitInformation)).ToInt32());
            Assert.Equal(64, Marshal.OffsetOf<NativeMethods.JobObjectExtendedLimitInformation>(
                nameof(NativeMethods.JobObjectExtendedLimitInformation.IoInfo)).ToInt32());
            Assert.Equal(112, Marshal.OffsetOf<NativeMethods.JobObjectExtendedLimitInformation>(
                nameof(NativeMethods.JobObjectExtendedLimitInformation.ProcessMemoryLimit)).ToInt32());
        }
    }

    [Fact]
    public void ProcessAttributeIdentifiersAndContainmentFlagsMatchWindowsSdk()
    {
        Assert.Equal(0x0002000D, NativeMethods.ProcThreadAttributeJobList.ToInt64());
        Assert.Equal(0x00020002, NativeMethods.ProcThreadAttributeHandleList.ToInt64());
        Assert.Equal(0x00080000u, NativeMethods.EXTENDED_STARTUPINFO_PRESENT);
        Assert.Equal(0x08000000u, NativeMethods.CREATE_NO_WINDOW);
        Assert.Equal(0x00002000u, NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE);
        Assert.Equal(0x00000008u, NativeMethods.JOB_OBJECT_LIMIT_ACTIVE_PROCESS);
        Assert.Equal(0x00000008u, NativeMethods.PIPE_REJECT_REMOTE_CLIENTS);
        Assert.Equal(0x00080000u, NativeMethods.FILE_FLAG_FIRST_PIPE_INSTANCE);
    }
}
