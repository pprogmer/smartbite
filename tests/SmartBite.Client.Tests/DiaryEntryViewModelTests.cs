using System.Net;
using System.Net.Http;
using SmartBite.Client.ViewModels;
using SmartBite.Contracts;

namespace SmartBite.Client.Tests;

// діалог додавання запису в щоденник (us-5, us-7, us-9)
public class DiaryEntryViewModelTests
{
    private static readonly DateOnly Day = new(2026, 11, 12);
    private const string DayJson =
        """{"date":"2026-11-12","consumed":{"calories":0,"protein":0,"fat":0,"carbs":0},"excessCalories":0,"entries":[]}""";

    private readonly FakeServer _server = new();

    private static RecipeResponse Recipe() =>
        new("Омлет", 15, 2, [], [], new Nutrients(320, 20, 24.9m, 4), new Nutrients(500, 0, 0, 0));

    // запускає збереження і чекає результат: true/false якщо діалог закривається, null якщо лишився з помилкою
    private static async Task<bool?> SaveAsync(DiaryEntryViewModel vm)
    {
        var outcome = new TaskCompletionSource<bool?>();
        vm.CloseRequested += saved => outcome.TrySetResult(saved);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.HasError) && vm.HasError)
            {
                outcome.TrySetResult(null);
            }
        };

        vm.SaveCommand.Execute(null);
        return await outcome.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void FromRecipe_TakesServingsAndPerServingValues()
    {
        var vm = DiaryEntryViewModel.FromRecipe(_server.Client(), Recipe(), Day);

        Assert.Equal("Омлет", vm.Name);
        Assert.Equal("2", vm.Servings);
        Assert.Equal("320", vm.Calories);
        Assert.Equal("640", vm.TotalCalories); // us-5: дві порції - вдвічі більше калорій
        Assert.Equal("49,8", vm.TotalFat);
        Assert.True(vm.IsEstimated);
    }

    [Fact]
    public void Totals_FollowTheServings()
    {
        var vm = DiaryEntryViewModel.FromRecipe(_server.Client(), Recipe(), Day);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Servings = "1,5";

        Assert.Equal("480", vm.TotalCalories);
        Assert.Contains(nameof(vm.TotalCalories), changed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("два")]
    public void Totals_ShowADash_ForWrongNumbers(string servings)
    {
        var vm = DiaryEntryViewModel.FromRecipe(_server.Client(), Recipe(), Day);

        vm.Servings = servings;

        Assert.Equal("—", vm.TotalCalories);
    }

    [Fact]
    public void Manual_StartsEmptyAndEditable()
    {
        var vm = DiaryEntryViewModel.Manual(_server.Client(), Day);

        Assert.Equal("Додати до щоденника", vm.Title);
        Assert.False(vm.IsEstimated);
        Assert.Equal("1", vm.Servings);
        Assert.Equal(Day, vm.EntryDate);
    }

    [Fact]
    public async Task WrongNumber_IsNotSent()
    {
        var vm = DiaryEntryViewModel.Manual(_server.Client(), Day);
        vm.Name = "Яблуко";
        vm.Calories = "багато";

        await SaveAsync(vm);

        Assert.Equal("Порції та харчову цінність потрібно вказати числами", vm.Error);
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task Save_SendsTheEditedValues_AndCloses()
    {
        // us-7: зберігається те, що користувач виправив
        _server.Reply(HttpStatusCode.OK, DayJson);
        var vm = DiaryEntryViewModel.Manual(_server.Client(), Day);
        vm.Name = "Яблуко";
        vm.Calories = "52,5";
        vm.Carbs = "13.1";

        var result = await SaveAsync(vm);

        Assert.True(result);
        Assert.Equal(HttpMethod.Post, _server.Requests[0].Method);
        Assert.Contains("\"calories\":52.5", _server.Bodies[0]);
        Assert.Contains("\"carbs\":13.1", _server.Bodies[0]);
        Assert.Contains("\"source\":\"Manual\"", _server.Bodies[0]);
    }

    [Fact]
    public async Task EditingAnEntry_UsesPut()
    {
        _server.Reply(HttpStatusCode.OK, DayJson);
        var entry = new DiaryEntryResponse(
            7,
            Day,
            MealType.Dinner,
            "Суп",
            EntrySource.Manual,
            1,
            new Nutrients(200, 10, 5, 28),
            new Nutrients(200, 10, 5, 28)
        );
        var vm = DiaryEntryViewModel.Edit(_server.Client(), entry);

        Assert.Equal("Редагувати запис", vm.Title);
        Assert.True(await SaveAsync(vm));
        Assert.Equal(HttpMethod.Put, _server.Requests[0].Method);
        Assert.EndsWith("api/diary/7", _server.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ServerRejection_IsShownInTheBanner_AndTheDialogStaysOpen()
    {
        _server.Reply(
            HttpStatusCode.BadRequest,
            """{"errors":{"PerServing.Calories":["Калорійність має бути від 0 до 100000"]}}""",
            "application/problem+json"
        );
        var vm = DiaryEntryViewModel.Manual(_server.Client(), Day);
        vm.Name = "Помилка";
        vm.Calories = "-5";

        var result = await SaveAsync(vm);

        Assert.Null(result); // діалог не закрився
        Assert.Equal("Калорійність має бути від 0 до 100000", vm.Error);
        Assert.True(vm.HasError);
    }
}
