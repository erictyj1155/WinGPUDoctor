using WinGPUDoctor.Core;
using WinGPUDoctor.Host;
using WinGPUDoctor.Supervisor;

namespace WinGPUDoctor.Desktop.ViewModels;

public enum ScanState { Welcome, Scanning, Cancelling, Result, Stopped, NeedsRestart }

// Scan lifecycle per ADR 0008 D5: Scan is disabled while a scan runs, Cancel is single-use and
// never escalates, and any non-cancel failure needs a restart and is never retried automatically.
public sealed class MainViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<CollectionSnapshot>> _collect;
    private HostScanSession? _session;
    private ScanState _state;
    private ResultViewModel? _result;
    private bool _closeRequested;

    public MainViewModel(Func<CancellationToken, Task<CollectionSnapshot>> collect)
    {
        _collect = collect ?? throw new ArgumentNullException(nameof(collect));
        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => CanScan);
        CancelCommand = new RelayCommand(Cancel, () => State == ScanState.Scanning);
    }

    public RelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }

    // Raised after a close requested during a scan can proceed.
    public event EventHandler? CloseReady;

    public ScanState State
    {
        get => _state;
        private set
        {
            if (!Set(ref _state, value)) return;
            foreach (var name in new[] { nameof(CanScan), nameof(IsWelcome), nameof(IsBusy), nameof(IsCancelling),
                nameof(IsResult), nameof(IsStopped), nameof(NeedsRestart), nameof(BusyText) })
                OnPropertyChanged(name);
            ScanCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public bool CanScan => State is ScanState.Welcome or ScanState.Result or ScanState.Stopped;
    public bool IsWelcome => State == ScanState.Welcome;
    public bool IsBusy => State is ScanState.Scanning or ScanState.Cancelling;
    public bool IsCancelling => State == ScanState.Cancelling;
    public bool IsResult => State == ScanState.Result;
    public bool IsStopped => State == ScanState.Stopped;
    public bool NeedsRestart => State == ScanState.NeedsRestart;
    public string BusyText => UiText.Get(State == ScanState.Cancelling ? "Scan.Stopping" : "Scan.Reading");

    // A new scan discards the previous report before collection starts.
    public ResultViewModel? Result
    {
        get => _result;
        private set => Set(ref _result, value);
    }

    public async Task ScanAsync()
    {
        if (!CanScan) return;
        Result = null;
        var session = new HostScanSession(); // Controllers are terminal, so every scan gets a fresh one.
        _session = session;
        State = ScanState.Scanning;
        ScanState next;
        ResultViewModel? result = null;
        try
        {
            // Off the UI thread: worker launch and cleanup must not block input.
            var outcome = await Task.Run(() => session.RunAsync(_collect));
            (next, result) = outcome.Kind switch
            {
                HostScanOutcomeKind.Completed => (ScanState.Result, new ResultViewModel(ReportDocument.From(outcome.Report!))),
                HostScanOutcomeKind.Cancelled => (ScanState.Stopped, null),
                _ => (ScanState.NeedsRestart, null)
            };
        }
        catch (Exception)
        {
            // Outermost handling (review N2): no exception text or path reaches the UI.
            next = ScanState.NeedsRestart;
        }
        finally
        {
            _session = null;
            session.Dispose();
        }
        Result = result;
        State = next;
        if (_closeRequested) CloseReady?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        if (State != ScanState.Scanning || _session is not { } session) return;
        State = ScanState.Cancelling;
        // A Forced result means output commitment already won; the scan then completes normally.
        _ = Task.Run(session.RequestCancellation);
    }

    // Closing during a scan requests controlled cancellation first and closes once it has stopped.
    public bool RequestClose()
    {
        if (!IsBusy) return true;
        _closeRequested = true;
        Cancel();
        return false;
    }
}
