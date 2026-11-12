using Microsoft.EntityFrameworkCore;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Unit;

// підсумок дня і статистика за тиждень (us-5, us-8, us-10) на базі в пам'яті
public class DiaryServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 11, 12);
    private const int UserId = 1;

    private readonly SmartBiteDbContext _db = new(
        new DbContextOptionsBuilder<SmartBiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options
    );

    private DiaryService Service => new(_db);

    public void Dispose() => _db.Dispose();

    private void AddProfile() =>
        _db.Profiles.Add(
            new Profile
            {
                UserId = UserId,
                Sex = Sex.Male,
                Age = 30,
                HeightCm = 180,
                WeightKg = 80,
                ActivityLevel = ActivityLevel.Low,
                Goal = Goal.Lose,
            }
        );

    private void AddEntry(
        decimal calories,
        DateOnly? date = null,
        decimal servings = 1,
        int userId = UserId,
        MealType meal = MealType.Lunch
    ) =>
        _db.DiaryEntries.Add(
            new DiaryEntry
            {
                UserId = userId,
                Date = date ?? Day,
                MealType = meal,
                Name = $"Страва {calories}",
                Servings = servings,
                Calories = calories,
            }
        );

    [Fact]
    public async Task Us8_AcceptanceExample()
    {
        AddProfile();
        AddEntry(450);
        AddEntry(620);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Equal(TestData.Us2Norm, day.Norm);
        Assert.Equal(1070, day.Consumed.Calories);
        Assert.Equal(1010, day.Remaining!.Calories);
        Assert.Equal(0, day.ExcessCalories);
    }

    [Fact]
    public async Task ExceededNorm_RemainingIsZero_AndExcessIsShown()
    {
        AddProfile();
        AddEntry(1500);
        AddEntry(800);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Equal(0, day.Remaining!.Calories);
        Assert.Equal(220, day.ExcessCalories); // 2300 - 2080
    }

    [Fact]
    public async Task WithoutProfile_NormAndRemainingAreNull()
    {
        AddEntry(300);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Null(day.Norm);
        Assert.Null(day.Remaining);
        Assert.Equal(0, day.ExcessCalories);
        Assert.Equal(300, day.Consumed.Calories);
    }

    [Fact]
    public async Task EmptyDay_HasNothingConsumed()
    {
        AddProfile();
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Empty(day.Entries);
        Assert.Equal(NutrientMath.Zero, day.Consumed);
        Assert.Equal(day.Norm, day.Remaining);
    }

    [Fact]
    public async Task Servings_MultiplyTheEntry()
    {
        // us-5: дві порції рахуються двічі
        AddEntry(250, servings: 2);
        AddEntry(100, servings: 0.5m);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Equal(250, day.Entries.Single(e => e.Servings == 2).PerServing.Calories);
        Assert.Equal(500, day.Entries.Single(e => e.Servings == 2).Total.Calories);
        Assert.Equal(550, day.Consumed.Calories);
    }

    [Fact]
    public async Task OnlyThatUserAndDate_AreCounted()
    {
        AddEntry(300);
        AddEntry(1000, userId: 2);
        AddEntry(1000, date: Day.AddDays(-1));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Single(day.Entries);
        Assert.Equal(300, day.Consumed.Calories);
    }

    [Fact]
    public async Task Entries_AreOrderedByMeal()
    {
        AddEntry(1, meal: MealType.Snack);
        AddEntry(2, meal: MealType.Breakfast);
        AddEntry(3, meal: MealType.Dinner);
        AddEntry(4, meal: MealType.Lunch);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var day = await Service.GetDayAsync(UserId, Day);

        Assert.Equal(
            [MealType.Breakfast, MealType.Lunch, MealType.Dinner, MealType.Snack],
            day.Entries.Select(e => e.MealType)
        );
    }

    [Fact]
    public async Task Week_HasSevenDays_EmptyDaysAreZero()
    {
        // us-10
        AddProfile();
        AddEntry(500, date: Day);
        AddEntry(300, date: Day, servings: 2);
        AddEntry(1200, date: Day.AddDays(-6)); // перший день тижня
        AddEntry(9999, date: Day.AddDays(-7)); // поза тижнем
        AddEntry(9999, date: Day.AddDays(1)); // поза тижнем
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var week = await Service.GetWeekAsync(UserId, Day);

        Assert.Equal(2080, week.NormCalories);
        Assert.Equal(7, week.Days.Count);
        Assert.Equal(Day.AddDays(-6), week.Days[0].Date);
        Assert.Equal(Day, week.Days[^1].Date);
        Assert.Equal([1200, 0, 0, 0, 0, 0, 1100], week.Days.Select(d => d.Calories));
    }

    [Fact]
    public async Task Week_WithoutProfile_HasNoNorm()
    {
        var week = await Service.GetWeekAsync(UserId, Day);

        Assert.Null(week.NormCalories);
        Assert.All(week.Days, d => Assert.Equal(0, d.Calories));
    }
}
