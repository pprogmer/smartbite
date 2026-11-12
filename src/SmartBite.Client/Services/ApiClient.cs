using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartBite.Contracts;

namespace SmartBite.Client.Services;

/// <summary>An error from the server or the network, with a message that can be shown to the user.</summary>
public class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

/// <summary>The only way the client talks to the server. Every call throws <see cref="ApiException"/> on failure.</summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Raised when the server rejects the token (it has expired), so the user must sign in again.</summary>
    public event Action? SessionExpired;

    public async Task<AuthResponse> AuthAsync(bool register, string email, string password)
    {
        var url = register ? "api/auth/register" : "api/auth/login";
        var auth = await SendAsync<AuthResponse>(
            HttpMethod.Post,
            url,
            new AuthRequest(email, password)
        );
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            auth.Token
        );
        return auth;
    }

    public void SignOut() => http.DefaultRequestHeaders.Authorization = null;

    // Profile

    /// <summary>Null when the profile is not filled in yet.</summary>
    public async Task<ProfileResponse?> GetProfileAsync()
    {
        try
        {
            return await SendAsync<ProfileResponse>(HttpMethod.Get, "api/profile");
        }
        catch (ApiException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<ProfileResponse> SaveProfileAsync(ProfileRequest request) =>
        SendAsync<ProfileResponse>(HttpMethod.Put, "api/profile", request);

    // Products

    public Task<List<ProductResponse>> GetProductsAsync(
        string? search,
        ProductCategory? category,
        CancellationToken ct = default
    ) =>
        SendAsync<List<ProductResponse>>(
            HttpMethod.Get,
            $"api/products?search={Uri.EscapeDataString(search ?? "")}&category={category}",
            ct: ct
        );

    public Task<ProductResponse> CreateProductAsync(ProductRequest request) =>
        SendAsync<ProductResponse>(HttpMethod.Post, "api/products", request);

    public Task<ProductResponse> UpdateProductAsync(int id, ProductRequest request) =>
        SendAsync<ProductResponse>(HttpMethod.Put, $"api/products/{id}", request);

    public Task DeleteProductAsync(int id) =>
        SendCoreAsync(HttpMethod.Delete, $"api/products/{id}", null, default);

    // Diary. Changes return the updated day.

    public Task<DiaryDayResponse> GetDayAsync(DateOnly date) =>
        SendAsync<DiaryDayResponse>(HttpMethod.Get, $"api/diary?date={date:yyyy-MM-dd}");

    public Task<WeekStatsResponse> GetWeekAsync(DateOnly end) =>
        SendAsync<WeekStatsResponse>(HttpMethod.Get, $"api/diary/week?end={end:yyyy-MM-dd}");

    public Task<DiaryDayResponse> AddEntryAsync(DiaryEntryRequest request) =>
        SendAsync<DiaryDayResponse>(HttpMethod.Post, "api/diary", request);

    public Task<DiaryDayResponse> UpdateEntryAsync(int id, DiaryEntryRequest request) =>
        SendAsync<DiaryDayResponse>(HttpMethod.Put, $"api/diary/{id}", request);

    public Task<DiaryDayResponse> DeleteEntryAsync(int id) =>
        SendAsync<DiaryDayResponse>(HttpMethod.Delete, $"api/diary/{id}");

    // Recipes and estimates call a language model, so they can take a while and can be cancelled.

    public Task<RecipeResponse> GenerateRecipeAsync(RecipeRequest request, CancellationToken ct) =>
        SendAsync<RecipeResponse>(HttpMethod.Post, "api/recipes/generate", request, ct);

    public Task<EstimateResponse> EstimateTextAsync(string text, CancellationToken ct) =>
        SendAsync<EstimateResponse>(
            HttpMethod.Post,
            "api/nutrition/estimate/text",
            new EstimateTextRequest(text),
            ct
        );

    public async Task<EstimateResponse> EstimatePhotoAsync(string path, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(
            new ByteArrayContent(await File.ReadAllBytesAsync(path, ct)),
            "photo",
            Path.GetFileName(path)
        );
        return await SendAsync<EstimateResponse>(
            HttpMethod.Post,
            "api/nutrition/estimate/photo",
            form,
            ct
        );
    }

    public Task<EstimateResponse> RecalculateAsync(RecalculateRequest request) =>
        SendAsync<EstimateResponse>(HttpMethod.Post, "api/nutrition/recalculate", request);

    // Plumbing

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken ct = default
    )
    {
        using var response = await SendCoreAsync(method, url, body, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is HttpContent content)
        {
            request.Content = content;
        }
        else if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Не вдалося з'єднатися із сервером. Спробуйте пізніше");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("Сервер не відповів вчасно. Спробуйте пізніше");
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        // A 401 on sign-in means wrong credentials; anywhere else it means the token has expired.
        if (response.StatusCode == HttpStatusCode.Unauthorized && url != "api/auth/login")
        {
            SessionExpired?.Invoke();
            throw new ApiException("Сеанс завершено. Увійдіть знову");
        }
        throw new ApiException(await ReadErrorAsync(response), response.StatusCode);
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
                    errors
                        .EnumerateObject()
                        .SelectMany(f => f.Value.EnumerateArray())
                        .Select(m => m.GetString())
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
