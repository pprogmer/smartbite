using SmartBite.Client.Services;

namespace SmartBite.Client.ViewModels;

/// <summary>Sign-in and registration screen.</summary>
public class AuthViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private bool _isRegister;
    private string _error = "";
    private string _message = "";

    public AuthViewModel(ApiClient api)
    {
        _api = api;
        SubmitCommand = new RelayCommand(SubmitAsync);
        ToggleModeCommand = new RelayCommand(() =>
        {
            IsRegister = !IsRegister;
            Error = "";
            return Task.CompletedTask;
        });
    }

    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";

    public bool IsRegister
    {
        get => _isRegister;
        set
        {
            Set(ref _isRegister, value);
            Notify(nameof(SubmitText));
            Notify(nameof(ToggleModeText));
        }
    }

    public string Error
    {
        get => _error;
        set => Set(ref _error, value);
    }

    public string Message
    {
        get => _message;
        set => Set(ref _message, value);
    }

    public string SubmitText => IsRegister ? "Зареєструватися" : "Увійти";

    public string ToggleModeText =>
        IsRegister ? "Вже є обліковий запис? Увійти" : "Немає облікового запису? Зареєструватися";

    public RelayCommand SubmitCommand { get; }
    public RelayCommand ToggleModeCommand { get; }

    private async Task SubmitAsync()
    {
        Error = "";
        Message = "";
        if (IsRegister && Password != ConfirmPassword)
        {
            Error = "Паролі не збігаються";
            return;
        }

        var error = await _api.AuthAsync(IsRegister, Email.Trim(), Password);
        if (error is null)
        {
            // TODO: navigate to the profile (after registration) or "Сьогодні" (after sign-in).
            Message = $"Ви увійшли як {Email.Trim()}";
        }
        else
        {
            Error = error;
        }
    }
}
