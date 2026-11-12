using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Unit;

// генерація рецепта з фейковою моделлю (us-4)
public class RecipeServiceTests
{
    private readonly FakeOpenRouter _api = new();
    private readonly RecipeService _service;

    private static readonly Product[] Products =
    [
        new()
        {
            Id = 1,
            Name = "Яйця",
            Quantity = 6,
            Unit = ProductUnit.Piece,
        },
    ];

    public RecipeServiceTests()
    {
        var options = Options.Create(
            new OpenRouterOptions
            {
                ApiKey = "test-key",
                RecipeModel = "test/recipe",
                TimeoutSeconds = 5,
                MaxAttempts = 3,
            }
        );
        var client = new OpenRouterClient(
            new HttpClient(_api) { BaseAddress = new Uri("https://openrouter.test/") },
            options,
            NullLogger<OpenRouterClient>.Instance
        );
        _service = new RecipeService(client, options);
    }

    [Fact]
    public async Task Prompt_ContainsProductsServingsAndRemainingNorm()
    {
        _api.Reply(TestData.Omelette());

        await _service.GenerateAsync(
            Products,
            2,
            new Nutrients(500, 40, 20, 50),
            TestContext.Current.CancellationToken
        );

        var prompt = _api.LastMessage(0);
        Assert.Contains("1: Яйця, 6 Piece", prompt);
        Assert.Contains("Кількість порцій: 2", prompt);
        Assert.Contains("500 ккал", prompt);
    }

    [Fact]
    public async Task ServingOverTheLimit_IsSentBackAndTheFixedRecipeIsReturned()
    {
        // лишилось 300 ккал, на порцію можна до 330
        _api.Reply(TestData.Omelette(calories: 450)).Reply(TestData.Omelette(calories: 320));

        var recipe = await _service.GenerateAsync(
            Products,
            1,
            new Nutrients(300, 20, 10, 30),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(320, recipe.PerServing.Calories);
        Assert.Contains("перевищує допустимі 330 ккал", _api.LastMessage(1));
    }

    [Fact]
    public async Task Result_IsTrimmedAndRounded()
    {
        _api.Reply(
            TestData.Omelette() with
            {
                Name = "  Омлет ",
                Ingredients = [new IngredientDraft(1, " Яйця ", 120.4m)],
                Steps = [" Збити яйця. "],
            }
        );

        var recipe = await _service.GenerateAsync(
            Products,
            1,
            new Nutrients(1000, 50, 50, 100),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Омлет", recipe.Name);
        Assert.Equal(new RecipeIngredient("Яйця", 120), recipe.Ingredients[0]);
        Assert.Equal("Збити яйця.", recipe.Steps[0]);
        // 204 / 9 = 22.666... г жирів показується з одним знаком після коми
        Assert.Equal(22.7m, recipe.PerServing.Fat);
    }
}
