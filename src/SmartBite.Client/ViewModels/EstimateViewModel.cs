using System.Collections.ObjectModel;
using System.IO;
using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>A component of the dish. Changing its mass asks the server to recalculate the whole estimate.</summary>
public class ComponentRow(EstimateComponent component, EstimateViewModel owner) : ViewModelBase
{
    private string _grams = Number.Show(component.Grams);

    public string Name => Component.Name;
    public Nutrients Per100g => Component.Per100g;

    public string Grams
    {
        get => _grams;
        set
        {
            Set(ref _grams, value);
            _ = owner.RecalculateAsync();
        }
    }

    public EstimateComponent Component { get; private set; } = component;
    public string Calories => Number.Show(Component.Nutrients.Calories);
    public string Protein => Number.Show(Component.Nutrients.Protein);
    public string Fat => Number.Show(Component.Nutrients.Fat);
    public string Carbs => Number.Show(Component.Nutrients.Carbs);

    /// <summary>Takes the values calculated by the server, with the mass as the server accepted it.</summary>
    public void Update(EstimateComponent calculated)
    {
        Component = calculated;
        _grams = Number.Show(calculated.Grams);
        foreach (
            var property in new[]
            {
                nameof(Grams),
                nameof(Calories),
                nameof(Protein),
                nameof(Fat),
                nameof(Carbs),
            }
        )
        {
            Notify(property);
        }
    }
}

/// <summary>Estimates calories of a dish from a photo or from a text.</summary>
public class EstimateViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private readonly MainViewModel _shell;
    private bool _isPhoto = true;
    private string _text = "";
    private string? _photoPath;
    private string _photoError = "";
    private bool _isBusy;
    private CancellationTokenSource? _cancel;
    private EstimateResponse? _original;
    private Nutrients _total = new(0, 0, 0, 0);
    private string _dishName = "";

    public EstimateViewModel(ApiClient api, MainViewModel shell)
    {
        _api = api;
        _shell = shell;
        PickPhotoCommand = new RelayCommand(() => SetPhoto(_shell.Dialogs.PickPhoto()));
        RemovePhotoCommand = new RelayCommand(() => SetPhoto(null));
        EstimateCommand = new RelayCommand(EstimateAsync, () => CanEstimate);
        CancelCommand = new RelayCommand(() => _cancel?.Cancel());
        RemoveComponentCommand = new RelayCommand(RemoveComponentAsync);
        ResetCommand = new RelayCommand(() => ShowEstimate(_original!));
        AddToDiaryCommand = new RelayCommand(AddToDiary);
    }

    public bool IsPhoto
    {
        get => _isPhoto;
        set
        {
            Set(ref _isPhoto, value);
            Notify(nameof(IsText));
            Notify(nameof(CanEstimate));
        }
    }

    public bool IsText
    {
        get => !IsPhoto;
        set => IsPhoto = !value;
    }

    public string Text
    {
        get => _text;
        set
        {
            Set(ref _text, value);
            Notify(nameof(TextCounter));
            Notify(nameof(CanEstimate));
        }
    }

    public string TextCounter => $"{Text.Length} / 1000";

    // Photo

    public string? PhotoPath
    {
        get => _photoPath;
        private set
        {
            Set(ref _photoPath, value);
            Notify(nameof(HasPhoto));
            Notify(nameof(NoPhoto));
            Notify(nameof(PhotoInfo));
            Notify(nameof(CanEstimate));
        }
    }

    public bool HasPhoto => PhotoPath is not null;
    public bool NoPhoto => PhotoPath is null;

    public string PhotoInfo =>
        PhotoPath is null
            ? ""
            : $"{Path.GetFileName(PhotoPath)} · {new FileInfo(PhotoPath).Length / 1024.0 / 1024:0.0} МБ";

    public string PhotoError
    {
        get => _photoError;
        private set
        {
            Set(ref _photoError, value);
            Notify(nameof(HasPhotoError));
        }
    }

    public bool HasPhotoError => PhotoError != "";

    /// <summary>Checks the file the same way the server does, so a wrong file is reported at once.</summary>
    public void SetPhoto(string? path)
    {
        PhotoError = "";
        if (path is null)
        {
            PhotoPath = null;
            return;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
        {
            PhotoError = "Підтримуються лише зображення JPEG і PNG";
        }
        else if (new FileInfo(path).Length > EstimateLimits.MaxPhotoBytes)
        {
            PhotoError = "Розмір файлу перевищує 5 МБ";
        }
        else
        {
            PhotoPath = path;
        }
    }

    // State of the result area: placeholder, loading or result.

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            Set(ref _isBusy, value);
            NotifyState();
        }
    }

    public bool CanEstimate => !IsBusy && (IsPhoto ? PhotoPath is not null : Text.Trim() != "");
    public bool ShowPlaceholder => _original is null && !IsBusy;
    public bool ShowLoading => IsBusy;
    public bool ShowResult => _original is not null && !IsBusy;
    public string LoadingText =>
        IsPhoto
            ? "Аналізуємо фото... Це може тривати до хвилини"
            : "Аналізуємо опис... Це може тривати до хвилини";

    // Result

    public string DishName => _dishName;
    public ObservableCollection<ComponentRow> Components { get; } = [];
    public bool CanRemoveComponent => Components.Count > 1;
    public string TotalGrams => Number.Show(Components.Sum(c => c.Component.Grams)) + " г";
    public string TotalCalories => Number.Show(_total.Calories);
    public string TotalProtein => Number.Show(_total.Protein);
    public string TotalFat => Number.Show(_total.Fat);
    public string TotalCarbs => Number.Show(_total.Carbs);

    public RelayCommand PickPhotoCommand { get; }
    public RelayCommand RemovePhotoCommand { get; }
    public RelayCommand EstimateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RemoveComponentCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand AddToDiaryCommand { get; }

    private async Task EstimateAsync()
    {
        _cancel = new CancellationTokenSource();
        var ct = _cancel.Token;
        IsBusy = true;
        EstimateResponse? estimate = null;
        if (
            await Run(async () =>
                estimate = IsPhoto
                    ? await _api.EstimatePhotoAsync(PhotoPath!, ct)
                    : await _api.EstimateTextAsync(Text.Trim(), ct)
            )
        )
        {
            _original = estimate;
            ShowEstimate(estimate!);
        }
        IsBusy = false;
    }

    private void ShowEstimate(EstimateResponse estimate)
    {
        _dishName = estimate.DishName;
        _total = estimate.Total;
        Components.Clear();
        foreach (var component in estimate.Components)
        {
            Components.Add(new ComponentRow(component, this));
        }
        NotifyResult();
    }

    /// <summary>Asks the server for new values after a mass was changed.</summary>
    public async Task RecalculateAsync()
    {
        var request = new List<ComponentRequest>();
        foreach (var row in Components)
        {
            if (!Number.TryParse(row.Grams, out var grams))
            {
                Error = "Масу потрібно вказати числом";
                row.Update(row.Component);
                return;
            }
            request.Add(new ComponentRequest(row.Name, grams, row.Per100g));
        }

        EstimateResponse? result = null;
        if (
            await Run(async () =>
                result = await _api.RecalculateAsync(new RecalculateRequest(DishName, request))
            )
        )
        {
            for (var i = 0; i < Components.Count; i++)
            {
                Components[i].Update(result!.Components[i]);
            }
            _total = result!.Total;
            NotifyResult();
        }
        else
        {
            // The server refused the values (for example a mass of 0), so show the last good ones again.
            foreach (var row in Components)
            {
                row.Update(row.Component);
            }
        }
    }

    private async Task RemoveComponentAsync(object? row)
    {
        if (Components.Count > 1)
        {
            Components.Remove((ComponentRow)row!);
            await RecalculateAsync();
        }
    }

    private void AddToDiary()
    {
        var entry = DiaryEntryViewModel.FromEstimate(
            _api,
            new EstimateResponse(DishName, Components.Select(c => c.Component).ToList(), _total),
            DateOnly.FromDateTime(DateTime.Now)
        );
        if (_shell.Dialogs.ShowEntry(entry))
        {
            _shell.OpenToday(entry.EntryDate);
        }
    }

    private void NotifyState()
    {
        foreach (
            var property in new[]
            {
                nameof(CanEstimate),
                nameof(ShowPlaceholder),
                nameof(ShowLoading),
                nameof(ShowResult),
            }
        )
        {
            Notify(property);
        }
    }

    private void NotifyResult()
    {
        string[] changed =
        [
            nameof(DishName),
            nameof(CanRemoveComponent),
            nameof(TotalGrams),
            nameof(TotalCalories),
            nameof(TotalProtein),
            nameof(TotalFat),
            nameof(TotalCarbs),
        ];
        foreach (var property in changed)
        {
            Notify(property);
        }
        NotifyState();
    }
}
