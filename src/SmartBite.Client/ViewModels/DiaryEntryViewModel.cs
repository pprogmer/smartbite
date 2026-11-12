using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>The dialog that adds a diary entry or edits an existing one.</summary>
public class DiaryEntryViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private readonly int? _id;
    private readonly EntrySource _source;
    private string _servings;
    private string _calories;
    private string _protein;
    private string _fat;
    private string _carbs;

    /// <param name="id">The entry being edited, or null for a new one.</param>
    public DiaryEntryViewModel(
        ApiClient api,
        int? id,
        string name,
        DateOnly date,
        MealType meal,
        EntrySource source,
        decimal servings,
        Nutrients perServing
    )
    {
        _api = api;
        _id = id;
        _source = source;
        Name = name;
        Date = date.ToDateTime(TimeOnly.MinValue);
        Meal = meal;
        _servings = Number.Show(servings);
        _calories = Number.Show(perServing.Calories);
        _protein = Number.Show(perServing.Protein);
        _fat = Number.Show(perServing.Fat);
        _carbs = Number.Show(perServing.Carbs);
        SaveCommand = new RelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public static DiaryEntryViewModel Manual(ApiClient api, DateOnly date) =>
        new(api, null, "", date, MealForNow(), EntrySource.Manual, 1, new Nutrients(0, 0, 0, 0));

    public static DiaryEntryViewModel Edit(ApiClient api, DiaryEntryResponse e) =>
        new(api, e.Id, e.Name, e.Date, e.MealType, e.Source, e.Servings, e.PerServing);

    public static DiaryEntryViewModel FromRecipe(
        ApiClient api,
        RecipeResponse recipe,
        DateOnly date
    ) =>
        new(
            api,
            null,
            recipe.Name,
            date,
            MealForNow(),
            EntrySource.Recipe,
            recipe.Servings,
            recipe.PerServing
        );

    public static DiaryEntryViewModel FromEstimate(
        ApiClient api,
        EstimateResponse estimate,
        DateOnly date
    ) =>
        new(
            api,
            null,
            estimate.DishName,
            date,
            MealForNow(),
            EntrySource.Estimate,
            1,
            estimate.Total
        );

    private static MealType MealForNow() =>
        DateTime.Now.Hour switch
        {
            < 11 => MealType.Breakfast,
            < 16 => MealType.Lunch,
            < 21 => MealType.Dinner,
            _ => MealType.Snack,
        };

    /// <summary>Raised with true when the entry was saved, false when the user cancelled.</summary>
    public event Action<bool>? CloseRequested;

    public string Title => _id is null ? "Додати до щоденника" : "Редагувати запис";

    public string Name { get; set; }
    public DateTime Date { get; set; }
    public MealType Meal { get; set; }

    public DateOnly EntryDate => DateOnly.FromDateTime(Date);

    /// <summary>Values of a recipe or an estimate come from the model, so only manual entries can change them.</summary>
    public bool IsEstimated => _source != EntrySource.Manual;

    public string Servings
    {
        get => _servings;
        set => Change(ref _servings, value);
    }

    public string Calories
    {
        get => _calories;
        set => Change(ref _calories, value);
    }

    public string Protein
    {
        get => _protein;
        set => Change(ref _protein, value);
    }

    public string Fat
    {
        get => _fat;
        set => Change(ref _fat, value);
    }

    public string Carbs
    {
        get => _carbs;
        set => Change(ref _carbs, value);
    }

    // "Total" is always servings x per-serving values and is never edited.
    public string TotalCalories => Total(Calories);
    public string TotalProtein => Total(Protein);
    public string TotalFat => Total(Fat);
    public string TotalCarbs => Total(Carbs);

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    private void Change(
        ref string field,
        string value,
        [System.Runtime.CompilerServices.CallerMemberName] string? name = null
    )
    {
        Set(ref field, value, name);
        Notify(nameof(TotalCalories));
        Notify(nameof(TotalProtein));
        Notify(nameof(TotalFat));
        Notify(nameof(TotalCarbs));
    }

    private string Total(string perServing) =>
        Number.TryParse(Servings, out var servings) && Number.TryParse(perServing, out var value)
            ? Number.Show(servings * value)
            : "—";

    private async Task SaveAsync()
    {
        if (
            !Number.TryParse(Servings, out var servings)
            || !Number.TryParse(Calories, out var calories)
            || !Number.TryParse(Protein, out var protein)
            || !Number.TryParse(Fat, out var fat)
            || !Number.TryParse(Carbs, out var carbs)
        )
        {
            Error = "Порції та харчову цінність потрібно вказати числами";
            return;
        }

        var request = new DiaryEntryRequest(
            EntryDate,
            Meal,
            Name ?? "",
            _source,
            servings,
            new Nutrients(calories, protein, fat, carbs)
        );
        var saved = await Run(() =>
            _id is { } id ? _api.UpdateEntryAsync(id, request) : _api.AddEntryAsync(request)
        );
        if (saved)
        {
            CloseRequested?.Invoke(true);
        }
    }
}
