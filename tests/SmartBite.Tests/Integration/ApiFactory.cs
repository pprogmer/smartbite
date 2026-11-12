using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Integration;

// весь api в пам'яті: справжні контролери, валідація і jwt, але база inmemory замість postgres і фейковий openrouter
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _database = Guid.NewGuid().ToString();

    public FakeOpenRouter OpenRouter { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // не development, щоб не запускались міграції на postgres і не читались user secrets
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789");
        builder.UseSetting("OpenRouter:ApiKey", "test-key");
        builder.UseSetting("OpenRouter:TimeoutSeconds", "5");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SmartBiteDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SmartBiteDbContext>>();
            services.AddDbContext<SmartBiteDbContext>(o => o.UseInMemoryDatabase(_database));

            services
                .AddHttpClient<OpenRouterClient>()
                .ConfigurePrimaryHttpMessageHandler(() => OpenRouter);
        });
    }

    // клієнт, який зайшов як новий користувач з унікальною поштою
    public async Task<HttpClient> SignedInClientAsync(bool withProfile = false)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "api/auth/register",
            new AuthRequest($"user-{Guid.NewGuid():N}@test.com", "password123")
        );
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            auth!.Token
        );

        if (withProfile)
        {
            (
                await client.PutAsJsonAsync("api/profile", TestData.Us2Profile)
            ).EnsureSuccessStatusCode();
        }
        return client;
    }
}
