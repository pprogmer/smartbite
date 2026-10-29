using SmartBite.Client.Services;

namespace SmartBite.Client.ViewModels;

/// <summary>Sign-in and registration screen.</summary>
public class AuthViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private readonly MainViewModel _shell;
    private bool _isRegister;
    private bool _isBusy;

    public AuthViewModel(ApiClient api, MainViewModel shell)
    {
        _api = api;
        _shell = shell;
        SubmitCommand = new RelayCommand(SubmitAsync);
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
            Notify(nameof(IsLogin));
            Notify(nameof(SubmitText));
            Error = "";
        }
    }

    public bool IsLogin
    {
        get => !IsRegister;
        set => IsRegister = !value;
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            Set(ref _isBusy, value);
            Notify(nameof(IsEditable));
            Notify(nameof(SubmitText));
        }
    }

    public bool IsEditable => !IsBusy;

    public string SubmitText =>
        IsBusy ? "Зачекайте..."
        : IsRegister ? "Зареєструватися"
        : "Увійти";

    public RelayCommand SubmitCommand { get; }

    private async Task SubmitAsync()
    {
        if (IsRegister && Password != ConfirmPassword)
        {
            Error = "Паролі не збігаються";
            return;
        }

        IsBusy = true;
        string? email = null;
        if (
            await Run(async () =>
                email = (await _api.AuthAsync(IsRegister, Email.Trim(), Password)).Email
            )
        )
        {
            await Run(() => _shell.SignedInAsync(email!));
        }
        IsBusy = false;
    }
}
