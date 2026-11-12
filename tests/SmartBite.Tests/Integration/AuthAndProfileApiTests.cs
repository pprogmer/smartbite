using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartBite.Contracts;
using SmartBite.Data;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Integration;

// us-1, us-2 і права доступу через http
public class AuthAndProfileApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task Register_ThenLogin_ReturnsTokens()
    {
        var client = api.CreateClient();
        var email = NewEmail();

        var registered = await (
            await client.PostAsJsonAsync("api/auth/register", new AuthRequest(email, "password123"))
        ).OkAsync<AuthResponse>();
        var loggedIn = await (
            await client.PostAsJsonAsync("api/auth/login", new AuthRequest(email, "password123"))
        ).OkAsync<AuthResponse>();

        Assert.Equal(email, registered.Email);
        Assert.False(string.IsNullOrEmpty(loggedIn.Token));
    }

    [Fact]
    public async Task Password_IsStoredOnlyAsHash()
    {
        var email = NewEmail();
        await api.CreateClient()
            .PostAsJsonAsync("api/auth/register", new AuthRequest(email, "password123"));

        using var scope = api.Services.CreateScope();
        var user = scope
            .ServiceProvider.GetRequiredService<SmartBiteDbContext>()
            .Users.Single(u => u.Email == email);
        Assert.DoesNotContain("password123", user.PasswordHash);
        Assert.True(user.PasswordHash.Length > 40);
    }

    [Fact]
    public async Task Email_IsCaseAndSpaceInsensitive()
    {
        var client = api.CreateClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("api/auth/register", new AuthRequest(email, "password123"));

        var duplicate = await client.PostAsJsonAsync(
            "api/auth/register",
            new AuthRequest($"  {email.ToUpperInvariant()} ", "password123")
        );
        var login = await client.PostAsJsonAsync(
            "api/auth/login",
            new AuthRequest(email.ToUpperInvariant(), "password123")
        );

        Assert.Equal(
            "Користувач із такою адресою вже існує",
            await duplicate.DetailAsync(HttpStatusCode.Conflict)
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData(true)] // неправильний пароль
    [InlineData(false)] // незнайома пошта
    public async Task WrongCredentials_Give401WithTheSameMessage(bool knownEmail)
    {
        var client = api.CreateClient();
        var email = NewEmail();
        if (knownEmail)
        {
            await client.PostAsJsonAsync(
                "api/auth/register",
                new AuthRequest(email, "password123")
            );
        }

        var response = await client.PostAsJsonAsync(
            "api/auth/login",
            new AuthRequest(email, "wrong-password")
        );

        // текст однаковий, щоб не було видно, які пошти зареєстровані
        Assert.Equal(
            "Неправильна адреса або пароль",
            await response.DetailAsync(HttpStatusCode.Unauthorized)
        );
    }

    [Theory]
    [InlineData("not-an-email", "password123", "коректну адресу")]
    [InlineData("a@b.com", "short", "щонайменше 8 символів")]
    [InlineData("", "password123", "Введіть адресу")]
    public async Task InvalidRegistration_Gives400(string email, string password, string message)
    {
        var response = await api.CreateClient()
            .PostAsJsonAsync("api/auth/register", new AuthRequest(email, password));

        Assert.Contains(message, await response.ValidationErrorsAsync());
    }

    [Theory]
    [InlineData("GET", "api/profile")]
    [InlineData("GET", "api/products")]
    [InlineData("GET", "api/diary")]
    [InlineData("POST", "api/nutrition/recalculate")]
    public async Task WithoutToken_Gives401(string method, string url)
    {
        var response = await api.CreateClient().SendAsync(new HttpRequestMessage(new(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgedToken_Gives401()
    {
        var client = api.CreateClient();
        // підписаний іншим ключем
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJlLW5vdC12YWxpZA"
        );

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("api/profile")).StatusCode
        );
    }

    [Fact]
    public async Task NewUser_HasNoProfile()
    {
        var client = await api.SignedInClientAsync();

        Assert.Equal(
            "Профіль ще не заповнено",
            await (await client.GetAsync("api/profile")).DetailAsync(HttpStatusCode.NotFound)
        );
    }

    [Fact]
    public async Task SavedProfile_ReturnsTheUs2Norm()
    {
        var client = await api.SignedInClientAsync();

        var saved = await (
            await client.PutAsJsonAsync("api/profile", TestData.Us2Profile)
        ).OkAsync<ProfileResponse>();
        var loaded = await (await client.GetAsync("api/profile")).OkAsync<ProfileResponse>();

        Assert.Equal(TestData.Us2Norm, saved.Norm);
        Assert.Equal(saved, loaded);
    }

    [Fact]
    public async Task ChangedWeight_RecalculatesTheNorm()
    {
        var client = await api.SignedInClientAsync(withProfile: true);

        var updated = await (
            await client.PutAsJsonAsync("api/profile", TestData.Us2Profile with { WeightKg = 90 })
        ).OkAsync<ProfileResponse>();

        Assert.Equal(2197, updated.Norm.Calories);
    }

    [Theory]
    [InlineData(99, "Зріст має бути від 100 до 250 см")]
    [InlineData(251, "Зріст має бути від 100 до 250 см")]
    public async Task HeightOutOfRange_IsNotSaved(int height, string message)
    {
        // us-2
        var client = await api.SignedInClientAsync();

        var response = await client.PutAsJsonAsync(
            "api/profile",
            TestData.Us2Profile with
            {
                HeightCm = height,
            }
        );

        Assert.Contains(message, await response.ValidationErrorsAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("api/profile")).StatusCode);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(250)]
    public async Task HeightOnTheBoundary_IsSaved(int height)
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PutAsJsonAsync(
            "api/profile",
            TestData.Us2Profile with
            {
                HeightCm = height,
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EnumAsNumber_IsRejected()
    {
        // enum приймається тільки назвою, тому число типу 42 не потрапить у базу
        var client = await api.SignedInClientAsync();

        var response = await client.PutAsync(
            "api/profile",
            JsonContent.Create(
                new
                {
                    sex = "Male",
                    age = 30,
                    heightCm = 180,
                    weightKg = 80,
                    activityLevel = 42,
                    goal = "Lose",
                }
            )
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
