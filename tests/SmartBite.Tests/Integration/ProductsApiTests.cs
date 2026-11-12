using System.Net;
using System.Net.Http.Json;
using SmartBite.Contracts;

namespace SmartBite.Tests.Integration;

// us-3 через http
public class ProductsApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static ProductRequest Product(
        string name,
        ProductCategory category = ProductCategory.Other
    ) => new(name, 1, ProductUnit.Piece, category);

    [Fact]
    public async Task CreatedProduct_AppearsInTheList()
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "api/products",
            new ProductRequest("  Яйця  ", 10, ProductUnit.Piece, ProductCategory.DairyAndEggs)
        );
        var created = await response.OkAsync<ProductResponse>();
        var list = await (await client.GetAsync("api/products")).OkAsync<List<ProductResponse>>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/products/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(
            new ProductResponse(
                created.Id,
                "Яйця",
                10,
                ProductUnit.Piece,
                ProductCategory.DairyAndEggs
            ),
            Assert.Single(list)
        );
    }

    [Theory]
    [InlineData("", 1, "Введіть назву продукту")]
    [InlineData("Сир", 0, "Кількість має бути більшою за нуль")]
    [InlineData("Сир", -1, "Кількість має бути більшою за нуль")]
    public async Task InvalidProduct_IsNotSaved(string name, decimal quantity, string message)
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "api/products",
            new ProductRequest(name, quantity, ProductUnit.Gram, ProductCategory.Other)
        );

        Assert.Contains(message, await response.ValidationErrorsAsync());
        Assert.Empty(
            await (await client.GetAsync("api/products")).OkAsync<List<ProductResponse>>()
        );
    }

    [Fact]
    public async Task TooLongName_IsRejected()
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync("api/products", Product(new string('a', 101)));

        Assert.Contains("не більше 100 символів", await response.ValidationErrorsAsync());
    }

    [Fact]
    public async Task Search_ByPartOfName_IgnoringCase_AndByCategory()
    {
        var client = await api.SignedInClientAsync();
        await client.PostAsJsonAsync(
            "api/products",
            Product("Молоко", ProductCategory.DairyAndEggs)
        );
        await client.PostAsJsonAsync("api/products", Product("Кокосове молоко"));
        await client.PostAsJsonAsync("api/products", Product("Хліб", ProductCategory.Bakery));

        async Task<string[]> Find(string query) =>
            (
                await (await client.GetAsync($"api/products?{query}")).OkAsync<
                    List<ProductResponse>
                >()
            )
                .Select(p => p.Name)
                .ToArray();

        Assert.Equal(["Кокосове молоко", "Молоко"], await Find("search=МОЛОК"));
        Assert.Equal(["Молоко"], await Find("search=молок&category=DairyAndEggs"));
        Assert.Equal(["Хліб"], await Find("category=Bakery"));
        Assert.Equal(3, (await Find("search=%20%20")).Length); // пустий пошук ігнорується
        Assert.Empty(await Find("search=риба"));
    }

    [Fact]
    public async Task Update_And_Delete()
    {
        var client = await api.SignedInClientAsync();
        var created = await (
            await client.PostAsJsonAsync("api/products", Product("Сир"))
        ).OkAsync<ProductResponse>();

        var updated = await (
            await client.PutAsJsonAsync(
                $"api/products/{created.Id}",
                new ProductRequest(
                    "Твердий сир",
                    200,
                    ProductUnit.Gram,
                    ProductCategory.DairyAndEggs
                )
            )
        ).OkAsync<ProductResponse>();
        var deleted = await client.DeleteAsync($"api/products/{created.Id}");

        Assert.Equal("Твердий сир", updated.Name);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"api/products/{created.Id}")).StatusCode
        );
    }

    [Fact]
    public async Task ProductsOfAnotherUser_AreInvisibleAndUntouchable()
    {
        var owner = await api.SignedInClientAsync();
        var stranger = await api.SignedInClientAsync();
        var product = await (
            await owner.PostAsJsonAsync("api/products", Product("Сир"))
        ).OkAsync<ProductResponse>();

        Assert.Empty(
            await (await stranger.GetAsync("api/products")).OkAsync<List<ProductResponse>>()
        );
        Assert.Equal(
            "Продукт не знайдено",
            await (await stranger.GetAsync($"api/products/{product.Id}")).DetailAsync(
                HttpStatusCode.NotFound
            )
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await stranger.PutAsJsonAsync($"api/products/{product.Id}", Product("Чуже"))
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await stranger.DeleteAsync($"api/products/{product.Id}")).StatusCode
        );

        // у власника продукт на місці і не змінився
        var mine = await (
            await owner.GetAsync($"api/products/{product.Id}")
        ).OkAsync<ProductResponse>();
        Assert.Equal("Сир", mine.Name);
    }
}
