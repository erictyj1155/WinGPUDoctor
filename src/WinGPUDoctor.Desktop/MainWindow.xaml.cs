using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using WinGPUDoctor.Desktop.ViewModels;

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
            if (e.PropertyName == nameof(MainViewModel.State)) FocusPrimary();
        };
        model.CloseReady += (_, _) =>
        {
            _closeAllowed = true;
            Close();
        };
        Loaded += (_, _) => FocusPrimary();
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
            ScanState.Result => ResultScroll,
            ScanState.Stopped => StoppedScan,
            _ => RestartClose
        };
        target?.Focus();
    });

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
