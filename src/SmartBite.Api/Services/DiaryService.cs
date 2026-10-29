using Microsoft.EntityFrameworkCore;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Services;

public class DiaryService(SmartBiteDbContext db)
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public async Task<DiaryDayResponse> GetDayAsync(int userId, DateOnly date)
    {
        var norm = await GetNormAsync(userId);
        var entries = await db
            .DiaryEntries.Where(e => e.UserId == userId && e.Date == date)
            .OrderBy(e => e.MealType)
            .ThenBy(e => e.Id)
            .Select(e => ToResponse(e))
            .ToListAsync();

        var consumed = entries.Select(e => e.Total).Sum().Rounded();
        return new DiaryDayResponse(
            date,
            norm,
            consumed,
            norm?.Minus(consumed),
            norm is null ? 0 : Math.Max(0, consumed.Calories - norm.Calories),
            entries
        );
    }

    /// <summary>Calories for the 7 days ending with <paramref name="end"/>; days without entries are 0.</summary>
    public async Task<WeekStatsResponse> GetWeekAsync(int userId, DateOnly end)
    {
        var start = end.AddDays(-6);
        var totals = await db
            .DiaryEntries.Where(e => e.UserId == userId && e.Date >= start && e.Date <= end)
            .GroupBy(e => e.Date)
            .Select(g => new { Date = g.Key, Calories = g.Sum(e => e.Calories * e.Servings) })
            .ToDictionaryAsync(d => d.Date, d => d.Calories);

        var days = Enumerable
            .Range(0, 7)
            .Select(i => start.AddDays(i))
            .Select(d => new DayCalories(d, NutrientMath.Round(totals.GetValueOrDefault(d))))
            .ToList();
        return new WeekStatsResponse((await GetNormAsync(userId))?.Calories, days);
    }

    public static DiaryEntryResponse ToResponse(DiaryEntry e)
    {
        var perServing = new Nutrients(e.Calories, e.Protein, e.Fat, e.Carbs);
        return new DiaryEntryResponse(
            e.Id,
            e.Date,
            e.MealType,
            e.Name,
            e.Source,
            e.Servings,
            perServing,
            perServing.Scale(e.Servings).Rounded()
        );
    }

    private async Task<Nutrients?> GetNormAsync(int userId)
    {
        var profile = await db.Profiles.FindAsync(userId);
        return profile is null ? null : NormCalculator.Calculate(profile);
    }
}
