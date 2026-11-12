using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartBite.Tests.Integration;

public static class Http
{
    // перевіряє код відповіді і повертає тіло як problem details
    public static async Task<JsonElement> ProblemAsync(
        this HttpResponseMessage response,
        HttpStatusCode expected
    )
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<string> DetailAsync(
        this HttpResponseMessage response,
        HttpStatusCode expected
    ) => (await response.ProblemAsync(expected)).GetProperty("detail").GetString()!;

    // всі повідомлення з помилки валідації 400 одним текстом
    public static async Task<string> ValidationErrorsAsync(this HttpResponseMessage response)
    {
        var problem = await response.ProblemAsync(HttpStatusCode.BadRequest);
        return string.Join(
            "\n",
            problem
                .GetProperty("errors")
                .EnumerateObject()
                .SelectMany(f => f.Value.EnumerateArray())
                .Select(m => m.GetString())
        );
    }

    public static async Task<T> OkAsync<T>(this HttpResponseMessage response)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
        );
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
