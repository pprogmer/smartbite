using System.Collections.ObjectModel;
using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>A product in the list. The check box chooses it for a recipe.</summary>
public class ProductRow(ProductResponse product, ProductsViewModel owner)
{
    public ProductResponse Product { get; } = product;
    public string Name => Product.Name;
    public string Quantity =>
        $"{Number.Show(Product.Quantity, "0.###")} {Labels.Units.Of(Product.Unit)}";
    public string Category => Labels.Categories.Of(Product.Category);

    public bool IsChecked
    {
        get => owner.IsChosen(Product.Id);
        set => owner.Choose(Product, value);
    }
}

/// <summary>The product list with search and filter, and the panel where a product is added or edited.</summary>
public class ProductsViewModel : PageViewModel
{
    private const int MaxForRecipe = 15;

    private readonly ApiClient _api;
    private readonly MainViewModel _shell;
    private readonly Dictionary<int, ProductResponse> _chosen = [];
    private CancellationTokenSource? _search;
    private string _searchText = "";
    private Option<ProductCategory?> _filter;
    private ProductRow? _selected;
    private bool _isEditing;
    private int? _editId;
    private string _name = "";
    private string _quantity = "";
    private ProductUnit _unit = ProductUnit.Gram;
    private ProductCategory? _category;

    public ProductsViewModel(ApiClient api, MainViewModel shell)
    {
        _api = api;
        _shell = shell;
        _filter = Filters[0];
        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new RelayCommand(SaveAsync);
        DeleteCommand = new RelayCommand(DeleteAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = "");
        CookCommand = new RelayCommand(
            () => _shell.OpenRecipe(_chosen.Values.ToList()),
            () => CanCook
        );
    }

    public ObservableCollection<ProductRow> Items { get; } = [];

    /// <summary>The categories of the filter: "Усі" first, then each category.</summary>
    public IReadOnlyList<Option<ProductCategory?>> Filters { get; } =
    [
        new(null, "Усі"),
        .. Labels.Categories.Select(c => new Option<ProductCategory?>(c.Value, c.Label)),
    ];

    public string SearchText
    {
        get => _searchText;
        set
        {
            Set(ref _searchText, value);
            _ = ReloadDelayedAsync();
        }
    }

    public Option<ProductCategory?> Filter
    {
        get => _filter;
        set
        {
            Set(ref _filter, value);
            _ = LoadAsync();
        }
    }

    public ProductRow? Selected
    {
        get => _selected;
        set
        {
            Set(ref _selected, value);
            if (value is not null)
            {
                StartEdit(value.Product);
            }
        }
    }

    public bool IsEmpty => Items.Count == 0 && SearchText == "" && Filter.Value is null;

    // The chosen products are remembered while the list is filtered, so a search does not lose them.
    public string ChosenText => $"Вибрано: {_chosen.Count}";
    public bool CanCook => _chosen.Count is >= 1 and <= MaxForRecipe;

    // Edit panel

    public bool IsEditing
    {
        get => _isEditing;
        private set => Set(ref _isEditing, value);
    }

    public bool IsNew => _editId is null;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string Quantity
    {
        get => _quantity;
        set => Set(ref _quantity, value);
    }

    public ProductUnit Unit
    {
        get => _unit;
        set => Set(ref _unit, value);
    }

    public ProductCategory? Category
    {
        get => _category;
        set => Set(ref _category, value);
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand CookCommand { get; }

    public bool IsChosen(int id) => _chosen.ContainsKey(id);

    public void Choose(ProductResponse product, bool chosen)
    {
        if (chosen)
        {
            _chosen[product.Id] = product;
        }
        else
        {
            _chosen.Remove(product.Id);
        }
        ChosenChanged();
    }

    private void ChosenChanged()
    {
        Notify(nameof(ChosenText));
        Notify(nameof(CanCook));
    }

    public override async Task LoadAsync()
    {
        // The old request is cancelled so a slow answer cannot replace a newer one.
        _search?.Cancel();
        _search = new CancellationTokenSource();
        var ct = _search.Token;

        List<ProductResponse>? products = null;
        if (
            await Run(async () =>
                products = await _api.GetProductsAsync(SearchText, Filter.Value, ct)
            )
        )
        {
            Items.Clear();
            foreach (var product in products!)
            {
                Items.Add(new ProductRow(product, this));
            }
            Notify(nameof(IsEmpty));
        }
    }

    /// <summary>Searches a moment after the last key press instead of after every key.</summary>
    private async Task ReloadDelayedAsync()
    {
        _search?.Cancel();
        _search = new CancellationTokenSource();
        try
        {
            await Task.Delay(250, _search.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await LoadAsync();
    }

    private void StartNew()
    {
        Selected = null;
        _editId = null;
        Name = "";
        Quantity = "";
        Unit = ProductUnit.Gram;
        Category = null;
        ShowEditPanel();
    }

    private void StartEdit(ProductResponse product)
    {
        _editId = product.Id;
        Name = product.Name;
        Quantity = Number.Show(product.Quantity, "0.###");
        Unit = product.Unit;
        Category = product.Category;
        ShowEditPanel();
    }

    private void ShowEditPanel()
    {
        Error = "";
        IsEditing = true;
        Notify(nameof(IsNew));
    }

    private async Task SaveAsync()
    {
        // A quantity that is not a number is sent as 0, so the server reports it like any other invalid value.
        Number.TryParse(Quantity, out var quantity);
        if (Category is null)
        {
            Error = "Виберіть категорію";
            return;
        }

        var request = new ProductRequest(Name, quantity, Unit, Category.Value);
        ProductResponse? saved = null;
        var ok = await Run(async () =>
            saved = _editId is { } id
                ? await _api.UpdateProductAsync(id, request)
                : await _api.CreateProductAsync(request)
        );
        if (!ok)
        {
            return;
        }

        if (_chosen.ContainsKey(saved!.Id))
        {
            _chosen[saved.Id] = saved;
        }
        await LoadAsync();
        Selected = Items.FirstOrDefault(r => r.Product.Id == saved.Id);
    }

    private async Task DeleteAsync()
    {
        if (_editId is not { } id || !_shell.Dialogs.Confirm($"Видалити продукт «{Name}»?"))
        {
            return;
        }

        if (await Run(() => _api.DeleteProductAsync(id)))
        {
            _chosen.Remove(id);
            ChosenChanged();
            _selected = null;
            Notify(nameof(Selected));
            IsEditing = false;
            await LoadAsync();
        }
    }
}
