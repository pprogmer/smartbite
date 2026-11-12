using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>Body parameters and goal on the left, the calculated daily norm on the right.</summary>
public class ProfileViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private readonly MainViewModel _shell;
    private Sex? _sex;
    private ActivityLevel? _activity;
    private Goal? _goal;
    private Nutrients? _norm;
    private string _message = "";

    public ProfileViewModel(ApiClient api, MainViewModel shell)
    {
        _api = api;
        _shell = shell;
        SaveCommand = new RelayCommand(SaveAsync);
    }

    public Sex? Sex
    {
        get => _sex;
        set => Set(ref _sex, value);
    }

    public string Age { get; set; } = "";
    public string Height { get; set; } = "";
    public string Weight { get; set; } = "";

    public ActivityLevel? Activity
    {
        get => _activity;
        set => Set(ref _activity, value);
    }

    public Goal? Goal
    {
        get => _goal;
        set => Set(ref _goal, value);
    }

    public Nutrients? Norm
    {
        get => _norm;
        private set
        {
            Set(ref _norm, value);
            Notify(nameof(HasNorm));
            Notify(nameof(NoNorm));
        }
    }

    public bool HasNorm => Norm is not null;
    public bool NoNorm => Norm is null;

    /// <summary>The first sign-in: the profile has not been saved yet.</summary>
    public bool IsOnboarding => !_shell.CanNavigate;

    public string Message
    {
        get => _message;
        private set => Set(ref _message, value);
    }

    public RelayCommand SaveCommand { get; }

    public override async Task LoadAsync()
    {
        Notify(nameof(IsOnboarding));
        Message = "";
        await Run(async () =>
        {
            var profile = await _api.GetProfileAsync();
            if (profile is null)
            {
                return;
            }

            Sex = profile.Sex;
            Age = profile.Age.ToString();
            Height = profile.HeightCm.ToString();
            Weight = Number.Show(profile.WeightKg, "0.##");
            Activity = profile.ActivityLevel;
            Goal = profile.Goal;
            Norm = profile.Norm;
            Notify(nameof(Age));
            Notify(nameof(Height));
            Notify(nameof(Weight));
        });
    }

    private async Task SaveAsync()
    {
        Message = "";
        if (Sex is null || Activity is null || Goal is null)
        {
            Error = "Заповніть усі поля профілю";
            return;
        }
        if (
            !int.TryParse(Age, out var age)
            || !int.TryParse(Height, out var height)
            || !Number.TryParse(Weight, out var weight)
        )
        {
            Error = "Вік, зріст і масу потрібно вказати числами";
            return;
        }

        var wasOnboarding = IsOnboarding;
        var request = new ProfileRequest(
            Sex.Value,
            age,
            height,
            weight,
            Activity.Value,
            Goal.Value
        );
        if (await Run(async () => Norm = (await _api.SaveProfileAsync(request)).Norm))
        {
            if (wasOnboarding)
            {
                _shell.ProfileCompleted();
            }
            else
            {
                Message = "Профіль збережено";
            }
        }
    }
}
