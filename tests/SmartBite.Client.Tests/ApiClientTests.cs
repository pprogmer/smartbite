using System.Net;
using System.Net.Http;
using SmartBite.Client.Services;
using SmartBite.Contracts;

namespace SmartBite.Client.Tests;

// як клієнт перетворює відповіді сервера на повідомлення для користувача
public class ApiClientTests
{
    private readonly FakeServer _server = new();

    [Fact]
    public async Task SignIn_SetsTheTokenForLaterRequests()
    {
        _server
            .Reply(HttpStatusCode.OK, """{"token":"abc","email":"a@b.com"}""")
            .Reply(HttpStatusCode.OK, "[]");
        var api = _server.Client();

        await api.AuthAsync(register: false, "a@b.com", "password123");
        await api.GetProductsAsync(null, null);

        Assert.EndsWith("api/auth/login", _server.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("Bearer abc", _server.Requests[1].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task SignOut_ForgetsTheToken()
    {
        _server
            .Reply(HttpStatusCode.OK, """{"token":"abc","email":"a@b.com"}""")
            .Reply(HttpStatusCode.OK, "[]");
        var api = _server.Client();

        await api.AuthAsync(register: true, "a@b.com", "password123");
        api.SignOut();
        await api.GetProductsAsync(null, null);

        Assert.Null(_server.Requests[1].Headers.Authorization);
    }

    [Fact]
    public async Task ValidationErrors_AreJoinedLineByLine()
    {
        _server.Reply(
            HttpStatusCode.BadRequest,
            """
            {"title":"One or more validation errors occurred.","status":400,
             "errors":{"Name":["Введіть назву продукту"],"Quantity":["Кількість має бути більшою за нуль"]}}
            """,
            "application/problem+json"
        );

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            _server
                .Client()
                .CreateProductAsync(
                    new ProductRequest("", 0, ProductUnit.Gram, ProductCategory.Other)
                )
        );

        Assert.Equal("Введіть назву продукту\nКількість має бути більшою за нуль", e.Message);
        Assert.Equal(HttpStatusCode.BadRequest, e.StatusCode);
    }

    [Fact]
    public async Task ProblemDetail_IsShownAsIs()
    {
        _server.Reply(
            HttpStatusCode.ServiceUnavailable,
            """{"status":503,"detail":"Не вдалося згенерувати рецепт. Спробуйте пізніше"}"""
        );

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            _server.Client().GenerateRecipeAsync(new RecipeRequest([1], 1, null), default)
        );

        Assert.Equal("Не вдалося згенерувати рецепт. Спробуйте пізніше", e.Message);
    }

    [Theory]
    [InlineData("<html>Bad gateway</html>", "text/html")]
    [InlineData("", "text/plain")]
    [InlineData("{}", "application/json")]
    public async Task UnreadableError_GivesAGeneralMessageWithTheCode(string body, string type)
    {
        _server.Reply(HttpStatusCode.BadGateway, body, type);

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            _server.Client().GetProductsAsync(null, null)
        );

        Assert.Equal("Помилка сервера (502)", e.Message);
    }

    [Fact]
    public async Task ExpiredToken_RaisesSessionExpired()
    {
        _server.Reply(HttpStatusCode.Unauthorized, "");
        var api = _server.Client();
        var expired = false;
        api.SessionExpired += () => expired = true;

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            api.GetDayAsync(new DateOnly(2026, 11, 12))
        );

        Assert.True(expired);
        Assert.Equal("Сеанс завершено. Увійдіть знову", e.Message);
    }

    [Fact]
    public async Task WrongPassword_IsNotTakenForAnExpiredSession()
    {
        _server.Reply(
            HttpStatusCode.Unauthorized,
            """{"detail":"Неправильна адреса або пароль"}"""
        );
        var api = _server.Client();
        var expired = false;
        api.SessionExpired += () => expired = true;

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            api.AuthAsync(false, "a@b.com", "wrong-pass")
        );

        Assert.False(expired);
        Assert.Equal("Неправильна адреса або пароль", e.Message);
    }

    [Fact]
    public async Task MissingProfile_IsNull_NotAnError()
    {
        _server.Reply(HttpStatusCode.NotFound, """{"detail":"Профіль ще не заповнено"}""");

        Assert.Null(await _server.Client().GetProfileAsync());
    }

    [Fact]
    public async Task ServerDown_GivesAFriendlyMessage()
    {
        _server.Throw(new HttpRequestException("No connection could be made"));

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            _server.Client().GetProductsAsync(null, null)
        );

        Assert.Equal("Не вдалося з'єднатися із сервером. Спробуйте пізніше", e.Message);
    }

    [Fact]
    public async Task HttpTimeout_GivesAFriendlyMessage()
    {
        _server.Throw(new TaskCanceledException());

        var e = await Assert.ThrowsAsync<ApiException>(() =>
            _server.Client().GetProductsAsync(null, null)
        );

        Assert.Equal("Сервер не відповів вчасно. Спробуйте пізніше", e.Message);
    }

    [Fact]
    public async Task Search_IsEscapedInTheUrl()
    {
        _server.Reply(HttpStatusCode.OK, "[]");

        await _server.Client().GetProductsAsync("сир & масло", ProductCategory.DairyAndEggs);

        var query = _server.Requests[0].RequestUri!.Query;
        Assert.Contains("category=DairyAndEggs", query);
        Assert.Contains("search=%D1%81%D0%B8%D1%80%20%26%20", query);
    }

    [Fact]
    public async Task Enums_AreSentByName()
    {
        _server.Reply(
            HttpStatusCode.OK,
            """{"sex":"Male","age":30,"heightCm":180,"weightKg":80,"activityLevel":"Low","goal":"Lose","norm":{"calories":2080,"protein":156,"fat":58,"carbs":234}}"""
        );

        var profile = await _server
            .Client()
            .SaveProfileAsync(
                new ProfileRequest(Sex.Male, 30, 180, 80, ActivityLevel.Low, Goal.Lose)
            );

        Assert.Contains("\"activityLevel\":\"Low\"", _server.Bodies[0]);
        Assert.Equal(2080, profile.Norm.Calories);
    }
}
