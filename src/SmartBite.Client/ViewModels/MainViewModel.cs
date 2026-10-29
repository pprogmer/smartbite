using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

public enum Page
{
    Today,
    Products,
    Recipe,
    Estimate,
    Stats,
    Profile,
}

public record NavItem(Page Page, string Title);

/// <summary>
/// The main window: shows the sign-in screen, or the side panel with the current page.
/// It is also the navigator that pages use to open each other.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private Dictionary<Page, PageViewModel> _pages = [];
    private ViewModelBase? _current;
    private NavItem? _selected;
    private string _email = "";
    private bool _canNavigate = true;

    public MainViewModel(ApiClient api, DialogService dialogs)
    {
        _api = api;
        Dialogs = dialogs;
        SignOutCommand = new RelayCommand(() => ShowAuth());
        _api.SessionExpired += () => ShowAuth("Сеанс завершено. Увійдіть знову");
        ShowAuth();
    }

    public DialogService Dialogs { get; }

    public IReadOnlyList<NavItem> Items { get; } =
    [
        new(Page.Today, "Сьогодні"),
        new(Page.Products, "Мої продукти"),
        new(Page.Recipe, "Рецепт"),
        new(Page.Estimate, "Оцінити страву"),
        new(Page.Stats, "Статистика"),
        new(Page.Profile, "Профіль"),
    ];

    public ViewModelBase? Current => _current;

    public bool IsSignedIn => _email != "";

    public string Email => _email;

    /// <summary>False until the profile is saved for the first time.</summary>
    public bool CanNavigate
    {
        get => _canNavigate;
        private set => Set(ref _canNavigate, value);
    }

    public NavItem? SelectedItem
    {
        get => _selected;
        set
        {
            if (value is not null)
            {
                Open(value.Page);
            }
        }
    }

    public RelayCommand SignOutCommand { get; }

    /// <summary>Called by the sign-in screen. A user without a profile starts with the profile screen.</summary>
    public async Task SignedInAsync(string email)
    {
        var hasProfile = await _api.GetProfileAsync() is not null;

        _email = email;
        Notify(nameof(IsSignedIn));
        Notify(nameof(Email));
        _pages = new()
        {
            [Page.Today] = new TodayViewModel(_api, this),
            [Page.Products] = new ProductsViewModel(_api, this),
            [Page.Recipe] = new RecipeViewModel(_api, this),
            [Page.Estimate] = new EstimateViewModel(_api, this),
            [Page.Stats] = new StatsViewModel(_api),
            [Page.Profile] = new ProfileViewModel(_api, this),
        };
        CanNavigate = hasProfile;
        Open(hasProfile ? Page.Today : Page.Profile);
    }

    /// <summary>Called by the profile screen after the first save, when the other pages open up.</summary>
    public void ProfileCompleted()
    {
        CanNavigate = true;
        Open(Page.Today);
    }

    public void Open(Page page)
    {
        _selected = Items.First(i => i.Page == page);
        Notify(nameof(SelectedItem));
        Show(_pages[page]);
        _ = _pages[page].LoadAsync();
    }

    /// <summary>Opens the diary on the given day, e.g. after an entry was added.</summary>
    public void OpenToday(DateOnly date)
    {
        ((TodayViewModel)_pages[Page.Today]).Date = date;
        Open(Page.Today);
    }

    public void OpenRecipe(IReadOnlyList<ProductResponse> products)
    {
        ((RecipeViewModel)_pages[Page.Recipe]).SetProducts(products);
        Open(Page.Recipe);
    }

    private void ShowAuth(string message = "")
    {
        _api.SignOut();
        _email = "";
        Notify(nameof(IsSignedIn));
        Show(new AuthViewModel(_api, this) { Error = message });
    }

    private void Show(ViewModelBase page) => Set(ref _current, page, nameof(Current));
}
