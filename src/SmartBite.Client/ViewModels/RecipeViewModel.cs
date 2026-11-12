using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>An ingredient or a cooking step as a line of the recipe.</summary>
public record RecipeLine(string Left, string Right);

/// <summary>Generates a recipe from the chosen products. The generated recipe stays while other screens are open.</summary>
public class RecipeViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private readonly MainViewModel _shell;
    private IReadOnlyList<ProductResponse> _products = [];
    private string _servings = "1";
    private Nutrients? _remaining;
    private RecipeResponse? _recipe;
    private bool _isBusy;
    private CancellationTokenSource? _cancel;

    public RecipeViewModel(ApiClient api, MainViewModel shell)
    {
        _api = api;
        _shell = shell;
        GenerateCommand = new RelayCommand(GenerateAsync, () => CanGenerate);
        CancelCommand = new RelayCommand(() => _cancel?.Cancel());
        ChangeProductsCommand = new RelayCommand(() => _shell.Open(Page.Products));
        AddToDiaryCommand = new RelayCommand(AddToDiary);
    }

    public void SetProducts(IReadOnlyList<ProductResponse> products)
    {
        _products = products;
        _recipe = null;
        Notify(nameof(ProductsText));
        Notify(nameof(HasProducts));
        NotifyState();
    }

    public string Servings
    {
        get => _servings;
        set => Set(ref _servings, value);
    }

    public bool HasProducts => _products.Count > 0;

    public string ProductsText =>
        HasProducts
            ? "Продукти: "
                + string.Join(
                    ", ",
                    _products.Select(p =>
                        $"{p.Name} ({Number.Show(p.Quantity, "0.###")} {Labels.Units.Of(p.Unit)})"
                    )
                )
            : "Продукти не вибрано. Виберіть їх у розділі «Мої продукти»";

    public string RemainingText =>
        _remaining is null
            ? ""
            : $"Залишок норми на {DateOnly.FromDateTime(DateTime.Now):dd.MM.yyyy}: {Number.Show(_remaining.Calories)} ккал";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            Set(ref _isBusy, value);
            NotifyState();
        }
    }

    public bool NormUsedUp => _remaining is { Calories: <= 0 };

    public bool CanGenerate => HasProducts && !IsBusy && !NormUsedUp;

    // Exactly one of these three is shown in the result area.
    public bool ShowPlaceholder => _recipe is null && !IsBusy;
    public bool ShowResult => _recipe is not null && !IsBusy;
    public bool ShowLoading => IsBusy;

    public string RecipeName => _recipe?.Name ?? "";

    public string Meta => _recipe is null ? "" : $"{_recipe.CookingTimeMinutes} хв";

    public string IngredientsTitle =>
        _recipe is null ? "" : $"Інгредієнти (на {_recipe.Servings} порц.)";

    public IReadOnlyList<RecipeLine> Ingredients =>
        _recipe
            ?.Ingredients.Select(i => new RecipeLine(i.Name, $"{Number.Show(i.Grams)} г"))
            .ToList()
        ?? [];

    public IReadOnlyList<RecipeLine> Steps =>
        _recipe?.Steps.Select((s, i) => new RecipeLine((i + 1).ToString(), s)).ToList() ?? [];

    public string PerServingText =>
        _recipe is null ? "" : $"{Number.Show(_recipe.PerServing.Calories)} ккал";
    public string Protein =>
        _recipe is null ? "" : $"Б {Number.Show(_recipe.PerServing.Protein)} г";
    public string Fat => _recipe is null ? "" : $"Ж {Number.Show(_recipe.PerServing.Fat)} г";
    public string Carbs => _recipe is null ? "" : $"В {Number.Show(_recipe.PerServing.Carbs)} г";

    /// <summary>How much of the rest of the norm one serving takes, 0-100.</summary>
    public double ShareOfRemaining =>
        _recipe is null || _remaining is not { Calories: > 0 }
            ? 0
            : Math.Min(100, (double)(_recipe.PerServing.Calories / _remaining.Calories * 100));

    public string ShareText => $"{ShareOfRemaining:0} % залишку норми";

    public RelayCommand GenerateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ChangeProductsCommand { get; }
    public RelayCommand AddToDiaryCommand { get; }

    public override async Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (await Run(async () => _remaining = (await _api.GetDayAsync(today)).Remaining))
        {
            Notify(nameof(RemainingText));
            NotifyState();
        }
    }

    private async Task GenerateAsync()
    {
        if (!int.TryParse(Servings, out var servings))
        {
            Error = "Кількість порцій потрібно вказати числом";
            return;
        }

        _cancel = new CancellationTokenSource();
        IsBusy = true;
        var request = new RecipeRequest(_products.Select(p => p.Id).ToList(), servings, null);
        await Run(async () => _recipe = await _api.GenerateRecipeAsync(request, _cancel.Token));
        IsBusy = false;
        NotifyState();
    }

    private void AddToDiary()
    {
        var entry = DiaryEntryViewModel.FromRecipe(
            _api,
            _recipe!,
            DateOnly.FromDateTime(DateTime.Now)
        );
        if (_shell.Dialogs.ShowEntry(entry))
        {
            _shell.OpenToday(entry.EntryDate);
        }
    }

    /// <summary>Tells the view that everything that depends on the recipe or on the busy state has changed.</summary>
    private void NotifyState()
    {
        string[] changed =
        [
            nameof(NormUsedUp),
            nameof(CanGenerate),
            nameof(ShowPlaceholder),
            nameof(ShowResult),
            nameof(ShowLoading),
            nameof(RecipeName),
            nameof(Meta),
            nameof(IngredientsTitle),
            nameof(Ingredients),
            nameof(Steps),
            nameof(PerServingText),
            nameof(Protein),
            nameof(Fat),
            nameof(Carbs),
            nameof(ShareOfRemaining),
            nameof(ShareText),
        ];
        foreach (var property in changed)
        {
            Notify(property);
        }
    }
}
