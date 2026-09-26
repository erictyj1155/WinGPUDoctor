using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WinGPUDoctor.Desktop.Views;

public enum PaletteKind { Dark, Light, HighContrast }

// Picks the color palette (Themes/Palette.*.xaml) from the Windows settings and follows their changes:
// a high-contrast theme first, then the Windows app mode (light or dark). Settings are only read.
public static class AppTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int WM_SYSCOLORCHANGE = 0x0015, WM_SETTINGCHANGE = 0x001A, WM_THEMECHANGED = 0x031A;

    private static ResourceDictionary? _palette;
    private static PaletteKind? _applied;
    private static HwndSourceHook? _hook; // Kept referenced for the window's lifetime.

    // AppsUseLightTheme is 0 for dark mode; a missing value means the Windows default, light.
    public static PaletteKind Resolve(bool highContrast, int? appsUseLightTheme) =>
        highContrast ? PaletteKind.HighContrast : appsUseLightTheme == 0 ? PaletteKind.Dark : PaletteKind.Light;

    public static void Apply(Application app)
    {
        var kind = Resolve(SystemParameters.HighContrast, ReadAppsUseLightTheme());
        // High-contrast colors are read when the palette loads, so it reloads on every change.
        if (kind == _applied && kind != PaletteKind.HighContrast) return;
        var palette = new ResourceDictionary
        {
            Source = new Uri($"/wingpudoctor-gui;component/Themes/Palette.{kind}.xaml", UriKind.Relative)
        };
        var merged = app.Resources.MergedDictionaries;
        if (_palette is not null) merged.Remove(_palette);
        merged.Add(palette);
        _palette = palette;
        _applied = kind;
    }

    // Windows announces theme, contrast and animation changes with these messages. The palette and the
    // animation setting are re-evaluated afterwards, once WPF has refreshed its own cached system values.
    public static void Watch(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source) return;
        _hook = (IntPtr _, int message, IntPtr _, IntPtr _, ref bool _) =>
        {
            if (message is WM_SETTINGCHANGE or WM_SYSCOLORCHANGE or WM_THEMECHANGED)
                window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    Apply(Application.Current);
                    Motion.Refresh();
                });
            return IntPtr.Zero;
        };
        source.AddHook(_hook);
    }

    private static int? ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") as int?;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
