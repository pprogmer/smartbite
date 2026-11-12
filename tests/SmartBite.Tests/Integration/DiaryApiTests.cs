using System.Net;
using System.Net.Http.Json;
using SmartBite.Api.Services;
using SmartBite.Contracts;

namespace SmartBite.Tests.Integration;

// us-5, us-8, us-9, us-10 через http
public class DiaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Day = new(2026, 11, 12);

    private static DiaryEntryRequest Entry(
        decimal calories,
        decimal servings = 1,
        DateOnly? date = null,
        EntrySource source = EntrySource.Manual
    ) =>
        // тільки білки і вуглеводи, тобто 4 ккал на грам
        new(
            date ?? Day,
            MealType.Lunch,
            $"Страва {calories}",
            source,
            servings,
            new Nutrients(calories, calories / 8, 0, calories / 8)
        );

    [Fact]
    public async Task Us8_AddingEntries_UpdatesTheSummaryAtOnce()
    {
        var client = await api.SignedInClientAsync(withProfile: true);

        await client.PostAsJsonAsync("api/diary", Entry(450));
        var day = await (
            await client.PostAsJsonAsync("api/diary", Entry(620))
        ).OkAsync<DiaryDayResponse>();

        Assert.Equal(2080, day.Norm!.Calories);
        Assert.Equal(1070, day.Consumed.Calories);
        Assert.Equal(1010, day.Remaining!.Calories);
        Assert.Equal(2, day.Entries.Count);
    }

    [Fact]
    public async Task Us5_TwoServings_DoubleTheNutrients()
    {
        var client = await api.SignedInClientAsync(withProfile: true);

        var day = await (
            await client.PostAsJsonAsync(
                "api/diary",
                Entry(400, servings: 2, source: EntrySource.Recipe)
            )
        ).OkAsync<DiaryDayResponse>();

        var entry = Assert.Single(day.Entries);
        Assert.Equal(entry.PerServing.Scale(2), entry.Total);
        Assert.Equal(EntrySource.Recipe, entry.Source);
        Assert.Equal(800, day.Consumed.Calories);
    }

    [Fact]
    public async Task ExceededNorm_ShowsExcess()
    {
        var client = await api.SignedInClientAsync(withProfile: true);

        var day = await (
            await client.PostAsJsonAsync("api/diary", Entry(2200))
        ).OkAsync<DiaryDayResponse>();

        Assert.Equal(0, day.Remaining!.Calories);
        Assert.Equal(120, day.ExcessCalories);
    }

    [Theory]
    [InlineData(-1, 1, "Калорійність має бути від 0")]
    [InlineData(100, 0, "Кількість порцій має бути більшою за нуль")]
    [InlineData(100, 101, "Кількість порцій")]
    public async Task Us9_InvalidEntry_IsNotSaved(
        decimal calories,
        decimal servings,
        string message
    )
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "api/diary",
            Entry(0, servings) with
            {
                PerServing = new Nutrients(calories, 0, 0, 0),
            }
        );

        Assert.Contains(message, await response.ValidationErrorsAsync());
        var day = await (
            await client.GetAsync($"api/diary?date={Day:yyyy-MM-dd}")
        ).OkAsync<DiaryDayResponse>();
        Assert.Empty(day.Entries);
    }

    [Fact]
    public async Task Us9_EditAndDelete_RecalculateTheSummary()
    {
        var client = await api.SignedInClientAsync(withProfile: true);
        var added = await (
            await client.PostAsJsonAsync("api/diary", Entry(500))
        ).OkAsync<DiaryDayResponse>();
        var id = added.Entries[0].Id;

        var edited = await (
            await client.PutAsJsonAsync($"api/diary/{id}", Entry(300))
        ).OkAsync<DiaryDayResponse>();
        var deleted = await (
            await client.DeleteAsync($"api/diary/{id}")
        ).OkAsync<DiaryDayResponse>();

        Assert.Equal(300, edited.Consumed.Calories);
        Assert.Equal(1780, edited.Remaining!.Calories);
        Assert.Equal(0, deleted.Consumed.Calories);
        Assert.Empty(deleted.Entries);
    }

    [Fact]
    public async Task MovingAnEntryToAnotherDate_ReturnsThatDay()
    {
        var client = await api.SignedInClientAsync();
        var added = await (
            await client.PostAsJsonAsync("api/diary", Entry(500))
        ).OkAsync<DiaryDayResponse>();

        var moved = await (
            await client.PutAsJsonAsync(
                $"api/diary/{added.Entries[0].Id}",
                Entry(500, date: Day.AddDays(-1))
            )
        ).OkAsync<DiaryDayResponse>();
        var original = await (
            await client.GetAsync($"api/diary?date={Day:yyyy-MM-dd}")
        ).OkAsync<DiaryDayResponse>();

        Assert.Equal(Day.AddDays(-1), moved.Date);
        Assert.Single(moved.Entries);
        Assert.Empty(original.Entries);
    }

    [Fact]
    public async Task EntriesOfAnotherUser_CannotBeChanged()
    {
        var owner = await api.SignedInClientAsync();
        var stranger = await api.SignedInClientAsync();
        var id = (
            await (await owner.PostAsJsonAsync("api/diary", Entry(500))).OkAsync<DiaryDayResponse>()
        )
            .Entries[0]
            .Id;

        Assert.Equal(
            "Запис не знайдено",
            await (await stranger.PutAsJsonAsync($"api/diary/{id}", Entry(1))).DetailAsync(
                HttpStatusCode.NotFound
            )
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.DeleteAsync($"api/diary/{id}")).StatusCode
        );
        Assert.Empty(
            (
                await (
                    await stranger.GetAsync($"api/diary?date={Day:yyyy-MM-dd}")
                ).OkAsync<DiaryDayResponse>()
            ).Entries
        );
    }

    [Fact]
    public async Task Us10_Week_HasSevenDaysWithZeros()
    {
        var client = await api.SignedInClientAsync(withProfile: true);
        await client.PostAsJsonAsync("api/diary", Entry(500));
        await client.PostAsJsonAsync("api/diary", Entry(700, date: Day.AddDays(-2)));

        var week = await (
            await client.GetAsync($"api/diary/week?end={Day:yyyy-MM-dd}")
        ).OkAsync<WeekStatsResponse>();

        Assert.Equal(2080, week.NormCalories);
        Assert.Equal([0, 0, 0, 0, 700, 0, 500], week.Days.Select(d => d.Calories));
    }
}
