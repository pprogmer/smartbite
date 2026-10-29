using System.Collections.ObjectModel;
using System.Windows.Media;
using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>A diary entry as a table row.</summary>
public class EntryRow(DiaryEntryResponse entry, RelayCommand edit, RelayCommand delete)
{
    public string Name => entry.Name;
    public string Source => Labels.Sources.Of(entry.Source);
    public string Servings => Number.Show(entry.Servings);
    public string Calories => Number.Show(entry.Total.Calories);
    public string Protein => Number.Show(entry.Total.Protein);
    public string Fat => Number.Show(entry.Total.Fat);
    public string Carbs => Number.Show(entry.Total.Carbs);
    public RelayCommand EditCommand { get; } = edit;
    public RelayCommand DeleteCommand { get; } = delete;
}

/// <summary>Entries of one meal.</summary>
public record MealGroup(string Title, string Calories, IReadOnlyList<EntryRow> Rows);

/// <summary>A progress bar for protein, fat or carbs.</summary>
public record MacroBar(string Title, Brush Brush, double Value, double Maximum, string Text);

/// <summary>The main screen: the daily summary and the diary of one day.</summary>
public class TodayViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private readonly MainViewModel _shell;
    private DateOnly _date = Today;
    private DiaryDayResponse? _day;

    public TodayViewModel(ApiClient api, MainViewModel shell)
    {
        _api = api;
        _shell = shell;
        PreviousCommand = new RelayCommand(() => GoTo(Date.AddDays(-1)));
        NextCommand = new RelayCommand(() => GoTo(Date.AddDays(1)));
        TodayCommand = new RelayCommand(() => GoTo(Today));
        AddCommand = new RelayCommand(AddAsync);
        EstimateCommand = new RelayCommand(() => _shell.Open(Page.Estimate));
        CookCommand = new RelayCommand(() => _shell.Open(Page.Products));
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public DateOnly Date
    {
        get => _date;
        set
        {
            Set(ref _date, value);
            Notify(nameof(DateText));
        }
    }

    public string DateText => Date.ToString("dd.MM.yyyy");

    public RelayCommand PreviousCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand TodayCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand EstimateCommand { get; }
    public RelayCommand CookCommand { get; }

    public decimal Consumed => _day?.Consumed.Calories ?? 0;
    public decimal Norm => _day?.Norm?.Calories ?? 0;
    public bool IsOver => _day?.ExcessCalories > 0;
    public string ConsumedText => Number.Show(Consumed);
    public string NormText =>
        _day?.Norm is null ? "норму не задано" : $"/ {Number.Show(Norm)} ккал";

    public string CaloriesNote =>
        _day?.Norm is null ? ""
        : IsOver ? $"Перевищення норми на {Number.Show(_day.ExcessCalories)} ккал"
        : $"Залишилось {Number.Show(_day.Remaining!.Calories)} ккал";

    public IReadOnlyList<MacroBar> Macros =>
        _day is null
            ? []
            :
            [
                Macro("Білки", Palette.Protein, _day.Consumed.Protein, _day.Norm?.Protein),
                Macro("Жири", Palette.Fat, _day.Consumed.Fat, _day.Norm?.Fat),
                Macro("Вуглеводи", Palette.Carbs, _day.Consumed.Carbs, _day.Norm?.Carbs),
            ];

    public ObservableCollection<MealGroup> Meals { get; } = [];

    public bool IsEmpty => _day?.Entries.Count == 0;

    public override async Task LoadAsync()
    {
        if (await Run(async () => _day = await _api.GetDayAsync(Date)))
        {
            ShowDay();
        }
    }

    private Task GoTo(DateOnly date)
    {
        Date = date;
        return LoadAsync();
    }

    private static MacroBar Macro(string title, Brush brush, decimal consumed, decimal? norm) =>
        new(
            title,
            brush,
            (double)consumed,
            (double)(norm ?? 0),
            norm is null
                ? $"{Number.Show(consumed)} г"
                : $"{Number.Show(consumed)} / {Number.Show(norm.Value)} г"
        );

    private void ShowDay()
    {
        Meals.Clear();
        foreach (var meal in Labels.Meals)
        {
            var entries = _day!.Entries.Where(e => e.MealType == meal.Value).ToList();
            var rows = entries
                .Select(e => new EntryRow(
                    e,
                    new RelayCommand(() => EditAsync(e)),
                    new RelayCommand(() => DeleteAsync(e))
                ))
                .ToList();
            Meals.Add(
                new MealGroup(
                    meal.Label,
                    Number.Show(entries.Sum(e => e.Total.Calories)) + " ккал",
                    rows
                )
            );
        }

        string[] changed =
        [
            nameof(Consumed),
            nameof(Norm),
            nameof(IsOver),
            nameof(ConsumedText),
            nameof(NormText),
            nameof(CaloriesNote),
            nameof(Macros),
            nameof(IsEmpty),
        ];
        foreach (var property in changed)
        {
            Notify(property);
        }
    }

    private async Task AddAsync()
    {
        if (_shell.Dialogs.ShowEntry(DiaryEntryViewModel.Manual(_api, Date)))
        {
            await LoadAsync();
        }
    }

    private async Task EditAsync(DiaryEntryResponse entry)
    {
        if (_shell.Dialogs.ShowEntry(DiaryEntryViewModel.Edit(_api, entry)))
        {
            await LoadAsync();
        }
    }

    private async Task DeleteAsync(DiaryEntryResponse entry)
    {
        if (
            _shell.Dialogs.Confirm($"Видалити запис «{entry.Name}»?")
            && await Run(async () => _day = await _api.DeleteEntryAsync(entry.Id))
        )
        {
            ShowDay();
        }
    }
}
