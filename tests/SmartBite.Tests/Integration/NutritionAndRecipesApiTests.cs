using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Integration;

// us-4, us-6, us-7 через http, модель замінена на фейк
public class NutritionAndRecipesApiTests : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Day = new(2026, 11, 12);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly ApiFactory _api;
    private readonly FakeOpenRouter _model;

    public NutritionAndRecipesApiTests(ApiFactory api)
    {
        _api = api;
        _model = api.OpenRouter;
        _model.Reset();
    }

    private static MultipartFormDataContent Photo(byte[] bytes, string fileName = "dish.png")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "photo", fileName);
        return form;
    }

    // оцінка калорійності

    [Fact]
    public async Task Us7_TextEstimate_ReturnsComponentsAndTotal()
    {
        var client = await _api.SignedInClientAsync();
        _model.Reply(TestData.Porridge());

        var estimate = await (
            await client.PostAsJsonAsync(
                "api/nutrition/estimate/text",
                new EstimateTextRequest("200 г вівсяної каші, банан")
            )
        ).OkAsync<EstimateResponse>();

        Assert.Equal("Вівсянка з бананом", estimate.DishName);
        Assert.Equal(2, estimate.Components.Count);
        Assert.Equal(283, estimate.Total.Calories);
        Assert.Contains("200 г вівсяної каші, банан", _model.LastMessage(0));
    }

    [Fact]
    public async Task TextWithoutFood_Gives422()
    {
        var client = await _api.SignedInClientAsync();
        _model.Reply(
            new
            {
                isFood = false,
                dishName = "",
                components = Array.Empty<object>(),
            }
        );

        var response = await client.PostAsJsonAsync(
            "api/nutrition/estimate/text",
            new EstimateTextRequest("гарна погода")
        );

        Assert.Equal(
            "У тексті не знайдено продуктів",
            await response.DetailAsync(HttpStatusCode.UnprocessableEntity)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyText_IsRejectedWithoutCallingTheModel(string text)
    {
        var client = await _api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "api/nutrition/estimate/text",
            new EstimateTextRequest(text)
        );

        Assert.Contains("Введіть, що ви з'їли", await response.ValidationErrorsAsync());
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task ModelUnavailable_Gives503WithAFriendlyMessage()
    {
        var client = await _api.SignedInClientAsync();
        _model
            .ReplyStatus(HttpStatusCode.BadGateway)
            .ReplyStatus(HttpStatusCode.BadGateway)
            .ReplyStatus(HttpStatusCode.BadGateway);

        var response = await client.PostAsJsonAsync(
            "api/nutrition/estimate/text",
            new EstimateTextRequest("банан")
        );

        Assert.Equal(
            "Не вдалося оцінити страву. Спробуйте пізніше",
            await response.DetailAsync(HttpStatusCode.ServiceUnavailable)
        );
    }

    [Fact]
    public async Task Us6_PngPhoto_IsSentToTheModelAsDataUrl()
    {
        var client = await _api.SignedInClientAsync();
        _model.Reply(TestData.Porridge());

        var response = await client.PostAsync("api/nutrition/estimate/photo", Photo(Png));

        await response.OkAsync<EstimateResponse>();
        var url = _model
            .Requests[0]
            .RootElement.GetProperty("messages")[1]
            .GetProperty("content")[1]
            .GetProperty("image_url")
            .GetProperty("url")
            .GetString();
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(Png)}", url);
    }

    [Fact]
    public async Task Us6_PhotoWithoutFood_Gives422()
    {
        var client = await _api.SignedInClientAsync();
        _model.Reply(
            new
            {
                isFood = false,
                dishName = "",
                components = Array.Empty<object>(),
            }
        );

        var response = await client.PostAsync("api/nutrition/estimate/photo", Photo(Png));

        Assert.Equal(
            "На фотографії не виявлено страви",
            await response.DetailAsync(HttpStatusCode.UnprocessableEntity)
        );
    }

    [Fact]
    public async Task Us6_NotAnImage_IsRejected_EvenWithAnImageName()
    {
        var client = await _api.SignedInClientAsync();

        var response = await client.PostAsync(
            "api/nutrition/estimate/photo",
            Photo("GIF89a..."u8.ToArray(), "dish.png")
        );

        Assert.Contains("JPEG або PNG розміром до 5 МБ", await response.ValidationErrorsAsync());
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task Us6_PhotoOver5MB_IsRejected()
    {
        var client = await _api.SignedInClientAsync();
        var big = new byte[EstimateLimits.MaxPhotoBytes + 1];
        Png.CopyTo(big, 0);

        var response = await client.PostAsync("api/nutrition/estimate/photo", Photo(big));

        Assert.Contains("до 5 МБ", await response.ValidationErrorsAsync());
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task Us6_PhotoOfExactly5MB_IsAccepted()
    {
        var client = await _api.SignedInClientAsync();
        var limit = new byte[EstimateLimits.MaxPhotoBytes];
        Png.CopyTo(limit, 0);
        _model.Reply(TestData.Porridge());

        var response = await client.PostAsync("api/nutrition/estimate/photo", Photo(limit));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingPhoto_IsRejected()
    {
        var client = await _api.SignedInClientAsync();

        var response = await client.PostAsync(
            "api/nutrition/estimate/photo",
            new MultipartFormDataContent { { new StringContent("x"), "comment" } }
        );

        Assert.Contains("Виберіть фотографію", await response.ValidationErrorsAsync());
    }

    [Fact]
    public async Task Us7_Recalculate_DoublesNutrientsForDoubleMass()
    {
        var client = await _api.SignedInClientAsync();
        var per100g = new Nutrients(120, 4, 2, 21);

        var before = await (
            await client.PostAsJsonAsync(
                "api/nutrition/recalculate",
                new RecalculateRequest("Каша", [new ComponentRequest("Каша", 150, per100g)])
            )
        ).OkAsync<EstimateResponse>();
        var after = await (
            await client.PostAsJsonAsync(
                "api/nutrition/recalculate",
                new RecalculateRequest("Каша", [new ComponentRequest("Каша", 300, per100g)])
            )
        ).OkAsync<EstimateResponse>();

        Assert.Equal(before.Total.Scale(2), after.Total);
    }

    [Fact]
    public async Task Recalculate_WithoutComponents_IsRejected()
    {
        var client = await _api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "api/nutrition/recalculate",
            new RecalculateRequest("Каша", [])
        );

        Assert.Contains("Залиште хоча б один компонент", await response.ValidationErrorsAsync());
    }

    // рецепти

    private static async Task<int> AddProductAsync(HttpClient client, string name) =>
        (
            await (
                await client.PostAsJsonAsync(
                    "api/products",
                    new ProductRequest(name, 6, ProductUnit.Piece, ProductCategory.DairyAndEggs)
                )
            ).OkAsync<ProductResponse>()
        ).Id;

    [Fact]
    public async Task Us4_Recipe_FitsTheRestOfTheNorm()
    {
        var client = await _api.SignedInClientAsync(withProfile: true);
        var eggs = await AddProductAsync(client, "Яйця");
        await client.PostAsJsonAsync(
            "api/diary",
            new DiaryEntryRequest(
                Day,
                MealType.Breakfast,
                "Сніданок",
                EntrySource.Manual,
                1,
                new Nutrients(1780, 200, 0, 245)
            )
        );
        // лишилось 300 ккал, тож на порцію можна до 330
        _model.Reply(
            TestData.Omelette(calories: 320) with
            {
                Ingredients = [new(eggs, "Яйця", 120), new(0, "сіль", 2)],
            }
        );

        var recipe = await (
            await client.PostAsJsonAsync("api/recipes/generate", new RecipeRequest([eggs], 1, Day))
        ).OkAsync<RecipeResponse>();

        Assert.Equal(300, recipe.Remaining.Calories);
        Assert.Equal(320, recipe.PerServing.Calories);
        Assert.Contains("Залишок добової норми: 300 ккал", _model.LastMessage(0));
    }

    [Fact]
    public async Task Us4_RecipeWithAProductThatWasNotSelected_IsCorrected()
    {
        var client = await _api.SignedInClientAsync(withProfile: true);
        var eggs = await AddProductAsync(client, "Яйця");
        _model
            .Reply(TestData.Omelette() with { Ingredients = [new(0, "бекон", 50)] })
            .Reply(TestData.Omelette() with { Ingredients = [new(eggs, "Яйця", 120)] });

        var recipe = await (
            await client.PostAsJsonAsync("api/recipes/generate", new RecipeRequest([eggs], 1, Day))
        ).OkAsync<RecipeResponse>();

        Assert.Equal("Яйця", Assert.Single(recipe.Ingredients).Name);
        Assert.Contains("«бекон» не входить до вибраних продуктів", _model.LastMessage(1));
    }

    [Fact]
    public async Task Recipe_FromAnotherUsersProduct_Gives404()
    {
        var owner = await _api.SignedInClientAsync(withProfile: true);
        var stranger = await _api.SignedInClientAsync(withProfile: true);
        var eggs = await AddProductAsync(owner, "Яйця");

        var response = await stranger.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([eggs], 1, Day)
        );

        Assert.Equal(
            "Деякі з вибраних продуктів не знайдено",
            await response.DetailAsync(HttpStatusCode.NotFound)
        );
        Assert.Empty(_model.Requests);
    }

    [Fact]
    public async Task Recipe_DuplicateIds_AreCountedOnce()
    {
        var client = await _api.SignedInClientAsync(withProfile: true);
        var eggs = await AddProductAsync(client, "Яйця");
        _model.Reply(TestData.Omelette() with { Ingredients = [new(eggs, "Яйця", 120)] });

        var response = await client.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([eggs, eggs], 1, Day)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Recipe_WithoutProfile_Gives409()
    {
        var client = await _api.SignedInClientAsync();
        var eggs = await AddProductAsync(client, "Яйця");

        var response = await client.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([eggs], 1, Day)
        );

        Assert.Equal(
            "Спочатку заповніть профіль",
            await response.DetailAsync(HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task Recipe_WhenTheNormIsUsedUp_Gives409()
    {
        var client = await _api.SignedInClientAsync(withProfile: true);
        var eggs = await AddProductAsync(client, "Яйця");
        await client.PostAsJsonAsync(
            "api/diary",
            new DiaryEntryRequest(
                Day,
                MealType.Lunch,
                "Бенкет",
                EntrySource.Manual,
                1,
                new Nutrients(2080, 200, 0, 320)
            )
        );

        var response = await client.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([eggs], 1, Day)
        );

        Assert.Equal(
            "Добову норму на цей день уже вичерпано",
            await response.DetailAsync(HttpStatusCode.Conflict)
        );
        Assert.Empty(_model.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Recipe_ServingsOutOfRange_IsRejected(int servings)
    {
        var client = await _api.SignedInClientAsync(withProfile: true);

        var response = await client.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([1], servings, Day)
        );

        Assert.Contains(
            "Кількість порцій має бути від 1 до 10",
            await response.ValidationErrorsAsync()
        );
    }

    [Fact]
    public async Task Recipe_NoProducts_IsRejected()
    {
        var client = await _api.SignedInClientAsync(withProfile: true);

        var response = await client.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([], 1, Day)
        );

        Assert.Contains("Виберіть хоча б один продукт", await response.ValidationErrorsAsync());
    }

    [Fact]
    public async Task Us4_ModelUnavailable_Gives503WithTheMessageFromTheStory()
    {
        var client = await _api.SignedInClientAsync(withProfile: true);
        var eggs = await AddProductAsync(client, "Яйця");
        _model.ReplyStatus(HttpStatusCode.Unauthorized); // наприклад неправильний ключ, без повторів

        var response = await client.PostAsJsonAsync(
            "api/recipes/generate",
            new RecipeRequest([eggs], 1, Day)
        );

        Assert.Equal(
            "Не вдалося згенерувати рецепт. Спробуйте пізніше",
            await response.DetailAsync(HttpStatusCode.ServiceUnavailable)
        );
    }
}
