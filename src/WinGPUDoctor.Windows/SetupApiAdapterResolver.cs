using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinGPUDoctor.Windows;

internal interface IAdapterInstanceResolver { NativeResult<string> Resolve(string adapterInterfacePath); }

internal sealed class SetupApiAdapterResolver : IAdapterInstanceResolver
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInterfaceData { internal uint Size; internal Guid ClassGuid; internal uint Flags; internal nuint Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfoData { internal uint Size; internal Guid ClassGuid; internal uint DevInst; internal nuint Reserved; }

    private sealed class DeviceInfoSet : SafeHandleZeroOrMinusOneIsInvalid
    {
        public DeviceInfoSet() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }
    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true)]
    private static extern DeviceInfoSet SetupDiCreateDeviceInfoList(nint classGuid, nint parent);
    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiOpenDeviceInterfaceW(DeviceInfoSet set, string path, uint flags, ref DeviceInterfaceData data);
    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(DeviceInfoSet set, ref DeviceInterfaceData data,
        nint detail, uint detailSize, out uint requiredSize, ref DeviceInfoData device);
    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceIdW(DeviceInfoSet set, ref DeviceInfoData device,
        [Out] char[]? id, uint idSize, out uint requiredSize);
    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(nint set);

    public NativeResult<string> Resolve(string adapterInterfacePath)
    {
        using var set = SetupDiCreateDeviceInfoList(0, 0);
        if (set.IsInvalid) return new(Marshal.GetLastWin32Error(), "");
        var data = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
        if (!SetupDiOpenDeviceInterfaceW(set, adapterInterfacePath, 0, ref data)) return new(Marshal.GetLastWin32Error(), "");
        var device = new DeviceInfoData { Size = (uint)Marshal.SizeOf<DeviceInfoData>() };
        var success = SetupDiGetDeviceInterfaceDetailW(set, ref data, 0, 0, out _, ref device);
        var error = success ? 0 : Marshal.GetLastWin32Error();
        // Documented device-info-only query: ERROR_INSUFFICIENT_BUFFER fills DeviceInfoData.
        // We do not parse interface path text or request another copy of the path.
        if (error != 0 && error != Ccd.InsufficientBuffer) return new(error, "");
        success = SetupDiGetDeviceInstanceIdW(set, ref device, null, 0, out var required);
        error = success ? 0 : Marshal.GetLastWin32Error();
        if (error != 0 && error != Ccd.InsufficientBuffer) return new(error, "");
        if (required is < 2 or > 4096) return new(Ccd.InvalidParameter, "");
        var buffer = new char[required];
        if (!SetupDiGetDeviceInstanceIdW(set, ref device, buffer, required, out var written)) return new(Marshal.GetLastWin32Error(), "");
        if (written is < 2 || written > buffer.Length || buffer[written - 1] != '\0') return new(Ccd.InvalidParameter, "");
        return new(0, new string(buffer, 0, (int)written - 1));
    }
}
