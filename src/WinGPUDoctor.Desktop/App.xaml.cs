using System.Windows;
using System.Windows.Threading;
using WinGPUDoctor.Desktop.ViewModels;
using WinGPUDoctor.Desktop.Views;
using WinGPUDoctor.Supervisor;

namespace WinGPUDoctor.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#pragma warning disable WPF0001 // ADR 0008 D1: the built-in Fluent theme API is still marked experimental.
        ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
        AppTheme.Apply(this);
        DispatcherUnhandledException += OnUnhandledException;
        var window = new MainWindow(new MainViewModel(SupervisedWindowsCollector.CreateLocal().CollectAsync));
        MainWindow = window;
        window.Show();
    }

    // Never show exception text or paths; an unexpected UI failure ends the app.
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(UiText.Get("Error.Unexpected"), UiText.Get("App.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }
}
