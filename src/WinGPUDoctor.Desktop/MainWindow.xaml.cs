using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WinGPUDoctor.Desktop.ViewModels;
using WinGPUDoctor.Desktop.Views;

namespace WinGPUDoctor.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _model;
    private bool _closeAllowed;

    public MainWindow(MainViewModel model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.State) or nameof(MainViewModel.IsSaving)) FocusPrimary();
        };
        model.CloseReady += (_, _) =>
        {
            _closeAllowed = true;
            Close();
        };
        // No clipboard or drag copy from the preview: cloud clipboard sync is out of scope (GUI plan section 8).
        DataObject.AddCopyingHandler(PreviewBox, (_, e) => e.CancelCommand());
        Loaded += (_, _) => FocusPrimary();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AppTheme.Watch(this);
    }

    // Closing during a scan waits for controlled cancellation; the worker job remains the backstop.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closeAllowed && !_model.RequestClose()) e.Cancel = true;
        base.OnClosing(e);
    }

    // Keyboard and screen-reader users land on the primary action of each state.
    private void FocusPrimary() => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
    {
        UIElement? target = _model.State switch
        {
            ScanState.Welcome => WelcomeScan,
            ScanState.Scanning or ScanState.Cancelling => CancelButton,
            ScanState.Result => _model.IsSaving ? FormatMarkdown : ResultScroll,
            ScanState.Stopped => StoppedScan,
            _ => RestartClose
        };
        target?.Focus();
    });

    // The dialog only picks a path. Writing uses the Host rules: local fixed drive, CreateNew, never overwrite.
    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_model.Save is not { } save) return;
        var dialog = new SaveFileDialog
        {
            FileName = save.DefaultFileName,
            DefaultExt = save.FileExtension,
            Filter = save.FileFilter,
            AddExtension = true,
            OverwritePrompt = false,
            CheckPathExists = true,
            ValidateNames = true
        };
        if (dialog.ShowDialog(this) == true) save.Save(dialog.FileName);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
