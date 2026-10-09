using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartBite.Contracts;

namespace SmartBite.Client.Services;

/// <summary>The only way the client talks to the server.</summary>
public class ApiClient(HttpClient http)
{
    /// <summary>Signs in or registers. Returns an error message, or null on success.</summary>
    public async Task<string?> AuthAsync(bool register, string email, string password)
    {
        try
        {
            var url = register ? "api/auth/register" : "api/auth/login";
            var response = await http.PostAsJsonAsync(url, new AuthRequest(email, password));
            if (!response.IsSuccessStatusCode)
            {
                return await ReadErrorAsync(response);
            }

            var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            return null;
        }
        catch (HttpRequestException)
        {
            return "Не вдалося з'єднатися із сервером. Спробуйте пізніше";
        }
    }

    // The API returns RFC 7807 problem details: "errors" for invalid fields, "detail" for other errors.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            if (root.TryGetProperty("errors", out var errors))
            {
                return string.Join(
                    "\n",
                    errors.EnumerateObject().SelectMany(f => f.Value.EnumerateArray()).Select(m => m.GetString())
                );
            }
            if (root.TryGetProperty("detail", out var detail))
            {
                return detail.GetString() ?? "";
            }
        }
        catch (JsonException) { }

        return $"Помилка сервера ({(int)response.StatusCode})";
    }
}
