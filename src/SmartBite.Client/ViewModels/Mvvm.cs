using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SmartBite.Client.Services;

namespace SmartBite.Client.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        field = value;
        Notify(name);
    }

    protected void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A screen shown in the main window. Its error banner is filled by <see cref="Run"/>.</summary>
public abstract class PageViewModel : ViewModelBase
{
    private string _error = "";

    protected PageViewModel() => DismissErrorCommand = new RelayCommand(() => Error = "");

    public string Error
    {
        get => _error;
        set
        {
            Set(ref _error, value);
            Notify(nameof(HasError));
        }
    }

    public bool HasError => Error != "";

    public RelayCommand DismissErrorCommand { get; }

    /// <summary>Called each time the screen is opened.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>Runs an API call; a failed call shows its message in the banner. Returns false on failure.</summary>
    protected async Task<bool> Run(Func<Task> action)
    {
        Error = "";
        try
        {
            await action();
            return true;
        }
        catch (ApiException e)
        {
            Error = e.Message;
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>Command that is disabled while its async action runs.</summary>
public class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute) { }

    public RelayCommand(Func<object?, Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(
            _ =>
            {
                execute();
                return Task.CompletedTask;
            },
            canExecute
        ) { }

    // WPF asks again whenever the UI changes, so CanExecute needs no manual refresh.
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _running = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
