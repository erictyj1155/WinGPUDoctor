using System.Windows.Input;

namespace WinGPUDoctor.Desktop.ViewModels;

// ICommand lives in System.ObjectModel, so view models stay free of WPF types.
public sealed class RelayCommand(Action execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute();

    public void Execute(object? parameter)
    {
        if (canExecute()) execute();
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
