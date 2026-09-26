using WinGPUDoctor.Core;
using WinGPUDoctor.Host;
using WinGPUDoctor.Supervisor;

namespace WinGPUDoctor.Desktop.ViewModels;

public enum ScanState { Welcome, Scanning, Cancelling, Result, Stopped, NeedsRestart }

// Scan lifecycle per ADR 0008 D5: Scan is disabled while a scan runs, Cancel is single-use and
// never escalates, and any non-cancel failure needs a restart and is never retried automatically.
public sealed class MainViewModel : ObservableObject
{
    // Upper bound on waiting for a controlled stop after the window is closed: the supervisor's
    // overall collection budget (60 s, CollectionTimingPolicy) plus margin. After it, the window
    // closes anyway and the worker Job's kill-on-close is the backstop. A test pins this bound.
    public static TimeSpan DefaultCloseWait { get; } = TimeSpan.FromSeconds(75);

    private readonly Func<CancellationToken, Task<CollectionSnapshot>> _collect;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _closeWait;
    private HostScanSession? _session;
    private ScanState _state;
    private ResultViewModel? _result;
    private SaveViewModel? _save;
    private StopRequest _stopRequest;
    private bool _closeRequested;
    private bool _closeRaised;

    private enum StopRequest { None, Pending, Controlled, Forced }

    public MainViewModel(Func<CancellationToken, Task<CollectionSnapshot>> collect, TimeProvider? clock = null,
        TimeSpan? closeWait = null)
    {
        _collect = collect ?? throw new ArgumentNullException(nameof(collect));
        _clock = clock ?? TimeProvider.System;
        _closeWait = closeWait ?? DefaultCloseWait;
        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => CanScan);
        CancelCommand = new RelayCommand(() => _ = CancelAsync(),
            () => State == ScanState.Scanning && _stopRequest == StopRequest.None);
        OpenSaveCommand = new RelayCommand(OpenSave, () => IsViewingResult);
        CloseSaveCommand = new RelayCommand(() => Save = null, () => IsSaving);
    }

    public RelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand OpenSaveCommand { get; }
    public RelayCommand CloseSaveCommand { get; }

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
            SaveViewChanged();
        }
    }

    public bool CanScan => State is ScanState.Welcome or ScanState.Result or ScanState.Stopped;
    public bool IsWelcome => State == ScanState.Welcome;
    public bool IsBusy => State is ScanState.Scanning or ScanState.Cancelling;
    public bool IsCancelling => State == ScanState.Cancelling;
    public bool IsResult => State == ScanState.Result;
    public bool IsStopped => State == ScanState.Stopped;
    public bool NeedsRestart => State == ScanState.NeedsRestart;
    public string BusyText => UiText.Get(State == ScanState.Cancelling
        ? _closeRequested ? "Scan.StoppingToClose" : "Scan.Stopping"
        : _stopRequest switch
        {
            StopRequest.Pending => "Scan.RequestingStop",
            StopRequest.Forced => "Scan.Finishing",
            _ => "Scan.Reading"
        });

    // A new scan discards the previous report before collection starts.
    public ResultViewModel? Result
    {
        get => _result;
        private set
        {
            if (Set(ref _result, value)) SaveViewChanged();
        }
    }

    // The review-and-save panel for the current result; it never outlives its report.
    public SaveViewModel? Save
    {
        get => _save;
        private set
        {
            if (Set(ref _save, value)) SaveViewChanged();
        }
    }

    public bool IsViewingResult => State == ScanState.Result && Result is not null && Save is null;
    public bool IsSaving => State == ScanState.Result && Save is not null;

    private void OpenSave()
    {
        if (IsViewingResult) Save = new SaveViewModel(Result!.Document);
    }

    private void SaveViewChanged()
    {
        OnPropertyChanged(nameof(IsViewingResult));
        OnPropertyChanged(nameof(IsSaving));
        OpenSaveCommand.NotifyCanExecuteChanged();
        CloseSaveCommand.NotifyCanExecuteChanged();
    }

    public async Task ScanAsync()
    {
        if (!CanScan) return;
        Save = null;
        Result = null;
        var session = new HostScanSession(); // Controllers are terminal, so every scan gets a fresh one.
        _session = session;
        _stopRequest = StopRequest.None;
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
        if (_closeRequested) RaiseCloseReady();
    }

    // CancellationTokenSource.Cancel() runs token callbacks synchronously, so the request is sent on
    // the thread pool and can never hold the UI thread. Until its result is known the screen shows a
    // neutral "Requesting stop"; Controlled then shows "Stopping", while Forced means output commitment
    // already won and the scan finishes with its result. Single-use; never escalates.
    public async Task CancelAsync()
    {
        if (State != ScanState.Scanning || _stopRequest != StopRequest.None || _session is not { } session) return;
        SetStopRequest(StopRequest.Pending);
        var result = await Task.Run(session.RequestCancellation);
        if (!ReferenceEquals(_session, session) || State != ScanState.Scanning) return; // The scan already ended.
        if (result == HostInterruptResult.Controlled)
        {
            _stopRequest = StopRequest.Controlled;
            State = ScanState.Cancelling;
        }
        else SetStopRequest(StopRequest.Forced);
    }

    private void SetStopRequest(StopRequest value)
    {
        _stopRequest = value;
        OnPropertyChanged(nameof(BusyText));
        CancelCommand.NotifyCanExecuteChanged();
    }

    // Closing during a scan starts the close deadline first, independently of the cancellation request,
    // then sends that request. The window closes when the scan has stopped or, at the latest, when the
    // deadline passes, even if the request itself never returns.
    public bool RequestClose()
    {
        if (!IsBusy) return true;
        if (_closeRequested) return false;
        _closeRequested = true;
        _ = CloseAfterBoundAsync();
        OnPropertyChanged(nameof(BusyText));
        _ = CancelAsync();
        return false;
    }

    private async Task CloseAfterBoundAsync()
    {
        await Task.Delay(_closeWait, _clock);
        if (IsBusy) RaiseCloseReady();
    }

    private void RaiseCloseReady()
    {
        if (_closeRaised) return;
        _closeRaised = true;
        CloseReady?.Invoke(this, EventArgs.Empty);
    }
}
