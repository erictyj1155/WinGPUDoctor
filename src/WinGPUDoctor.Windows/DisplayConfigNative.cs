using System.Runtime.InteropServices;

namespace WinGPUDoctor.Windows;

// Windows SDK wingdi.h/winuser.h definitions, checked against Microsoft Learn.
// UINT32/enums/BOOL are 4 bytes; LUID is two 32-bit fields. No native identifiers enter Core types.
internal static class Ccd
{
    internal const uint ActivePaths = 0x2, VirtualModeAware = 0x10, VirtualRefreshAware = 0x40;
    internal const uint PathActive = 0x1, PathVirtualMode = 0x8, PathBoostRefresh = 0x10;
    internal const int Success = 0, AccessDenied = 5, NotSupported = 50, InvalidParameter = 87, InsufficientBuffer = 122;
    internal const int ApiUnavailable = -1, InvalidLayout = -2;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Luid(uint LowPart, int HighPart);
[StructLayout(LayoutKind.Sequential)]
internal struct NativeRational { internal uint Numerator, Denominator; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativeRegion { internal uint Width, Height; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint { internal int X, Y; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect { internal int Left, Top, Right, Bottom; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativeSourceMode { internal uint Width, Height, PixelFormat; internal NativePoint Position; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativeSignal
{
    internal ulong PixelRate;
    internal NativeRational HSync, VSync;
    internal NativeRegion ActiveSize, TotalSize;
    internal uint AdditionalSignalInfo, ScanLineOrdering;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeDesktopImage { internal NativePoint SourceSize; internal NativeRect Region, Clip; }
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct NativeMode
{
    [FieldOffset(0)] internal uint InfoType;
    [FieldOffset(4)] internal uint Id;
    [FieldOffset(8)] internal Luid AdapterId;
    [FieldOffset(16)] internal NativeSourceMode SourceMode;
    [FieldOffset(16)] internal NativeSignal TargetSignal;
    [FieldOffset(16)] internal NativeDesktopImage DesktopImage;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeSource
{
    internal Luid AdapterId;
    internal uint Id, ModeIndex, StatusFlags;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativeTarget
{
    internal Luid AdapterId;
    internal uint Id, ModeIndex, OutputTechnology, Rotation, Scaling;
    internal NativeRational RefreshRate;
    internal uint ScanLineOrdering;
    internal int TargetAvailable;
    internal uint StatusFlags;
}
[StructLayout(LayoutKind.Sequential)]
internal struct NativePath { internal NativeSource Source; internal NativeTarget Target; internal uint Flags; }
[StructLayout(LayoutKind.Sequential)]
internal struct DeviceInfoHeader { internal uint Type, Size; internal Luid AdapterId; internal uint Id; }
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeSourceName
{
    internal DeviceInfoHeader Header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string GdiName;
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeTargetName
{
    internal DeviceInfoHeader Header;
    internal uint Flags, OutputTechnology;
    internal ushort EdidManufacturer, EdidProduct;
    internal uint ConnectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string FriendlyName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string MonitorDevicePath;
}
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeAdapterName
{
    internal DeviceInfoHeader Header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string AdapterDevicePath;
}

internal readonly record struct NativeResult<T>(int Error, T Value);
internal interface IDisplayConfigApi
{
    int GetBufferSizes(uint flags, out uint pathCount, out uint modeCount);
    int Query(uint flags, ref uint pathCount, NativePath[] paths, ref uint modeCount, NativeMode[] modes);
    NativeResult<string> SourceName(Luid adapter, uint source);
    NativeResult<NativeTargetName> TargetName(Luid adapter, uint target);
    NativeResult<string> AdapterName(Luid adapter);
}

internal sealed class DisplayConfigApi : IDisplayConfigApi
{
    // Desktop user32 queries do not use GetLastError; their return LONG is the error code.
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] NativePath[] pathArray,
        ref uint modes, [Out] NativeMode[] modeArray, nint topologyId);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    private static extern int GetSource(ref NativeSourceName name);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    private static extern int GetTarget(ref NativeTargetName name);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", ExactSpelling = true)]
    private static extern int GetAdapter(ref NativeAdapterName name);

    internal static bool LayoutsValid => Marshal.SizeOf<Luid>() == 8 && Marshal.SizeOf<NativePath>() == 72 &&
        Marshal.SizeOf<NativeSource>() == 20 && Marshal.SizeOf<NativeTarget>() == 48 && Marshal.SizeOf<NativeMode>() == 64 &&
        Marshal.SizeOf<NativeSignal>() == 48 && Marshal.SizeOf<DeviceInfoHeader>() == 20 &&
        Marshal.SizeOf<NativeSourceName>() == 84 && Marshal.SizeOf<NativeTargetName>() == 420 && Marshal.SizeOf<NativeAdapterName>() == 276;

    public int GetBufferSizes(uint flags, out uint pathCount, out uint modeCount)
    {
        if (!LayoutsValid) { pathCount = modeCount = 0; return Ccd.InvalidLayout; }
        return GetDisplayConfigBufferSizes(flags, out pathCount, out modeCount);
    }
    public int Query(uint flags, ref uint pathCount, NativePath[] paths, ref uint modeCount, NativeMode[] modes) =>
        QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, 0);

    private static DeviceInfoHeader Header<T>(uint type, Luid adapter, uint id = 0) where T : struct =>
        new() { Type = type, Size = (uint)Marshal.SizeOf<T>(), AdapterId = adapter, Id = id };
    public NativeResult<string> SourceName(Luid adapter, uint source)
    {
        var name = new NativeSourceName { Header = Header<NativeSourceName>(1, adapter, source), GdiName = "" };
        var error = GetSource(ref name);
        return new(error, error == 0 ? name.GdiName : "");
    }
    public NativeResult<NativeTargetName> TargetName(Luid adapter, uint target)
    {
        var name = new NativeTargetName { Header = Header<NativeTargetName>(2, adapter, target), FriendlyName = "", MonitorDevicePath = "" };
        var error = GetTarget(ref name);
        return new(error, error == 0 ? name : default);
    }
    public NativeResult<string> AdapterName(Luid adapter)
    {
        var name = new NativeAdapterName { Header = Header<NativeAdapterName>(4, adapter), AdapterDevicePath = "" };
        var error = GetAdapter(ref name);
        return new(error, error == 0 ? name.AdapterDevicePath : "");
    }
}
