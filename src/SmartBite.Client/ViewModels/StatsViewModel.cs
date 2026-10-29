using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>
/// A bar of the chart. The bar is drawn in a two-row grid: the empty part above and the filled part below,
/// with heights in proportion to (max - value) and value.
/// </summary>
public record DayBar(
    string Day,
    string Value,
    GridLength Empty,
    GridLength Filled,
    Brush Brush,
    bool IsToday
);

/// <summary>Calories for 7 days against the norm.</summary>
public class StatsViewModel : PageViewModel
{
    private readonly ApiClient _api;
    private DateOnly _end = Today;
    private WeekStatsResponse? _week;

    public StatsViewModel(ApiClient api)
    {
        _api = api;
        PreviousCommand = new RelayCommand(() => GoTo(_end.AddDays(-7)));
        NextCommand = new RelayCommand(() => GoTo(_end.AddDays(7)), () => _end < Today);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public RelayCommand PreviousCommand { get; }
    public RelayCommand NextCommand { get; }

    public string Range => $"{_end.AddDays(-6):dd.MM} - {_end:dd.MM.yyyy}";

    public ObservableCollection<DayBar> Bars { get; } = [];

    /// <summary>The norm line is drawn the same way as a bar: a gap above and a gap below it.</summary>
    public GridLength NormEmpty { get; private set; } = new(1, GridUnitType.Star);
    public GridLength NormFilled { get; private set; } = new(1, GridUnitType.Star);

    public bool HasNorm => _week?.NormCalories is not null;
    public string NormText => $"норма {Number.Show(_week?.NormCalories ?? 0)}";

    public string Summary
    {
        get
        {
            if (_week is null)
            {
                return "";
            }

            var average = _week.Days.Average(d => d.Calories);
            var inNorm = _week.Days.Count(d => d.Calories > 0 && d.Calories <= _week.NormCalories);
            return $"Середнє за 7 днів: {Number.Show(Math.Round(average))} ккал · Днів у межах норми: {inNorm} з 7";
        }
    }

    public override async Task LoadAsync()
    {
        if (await Run(async () => _week = await _api.GetWeekAsync(_end)))
        {
            Show();
        }
    }

    private Task GoTo(DateOnly end)
    {
        _end = end;
        Notify(nameof(Range));
        return LoadAsync();
    }

    private void Show()
    {
        var norm = _week!.NormCalories ?? 0;

        // The scale goes up to the highest value (or the norm) rounded up to 500.
        var highest = Math.Max(_week.Days.Max(d => d.Calories), norm);
        var max = Math.Max(500, Math.Ceiling(highest * 1.1m / 500) * 500);

        Bars.Clear();
        foreach (var day in _week.Days)
        {
            var brush =
                day.Calories == 0 ? Palette.Empty
                : day.Calories > norm && norm > 0 ? Palette.Over
                : Palette.Ok;
            Bars.Add(
                new DayBar(
                    day.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd d"),
                    Number.Show(day.Calories),
                    new GridLength((double)(max - day.Calories), GridUnitType.Star),
                    new GridLength((double)day.Calories, GridUnitType.Star),
                    brush,
                    day.Date == Today
                )
            );
        }

        NormEmpty = new GridLength((double)(max - norm), GridUnitType.Star);
        NormFilled = new GridLength((double)norm, GridUnitType.Star);
        foreach (
            var property in new[]
            {
                nameof(NormEmpty),
                nameof(NormFilled),
                nameof(HasNorm),
                nameof(NormText),
                nameof(Summary),
            }
        )
        {
            Notify(property);
        }
    }
}
