using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;

namespace SmartBite.Api.Services;

public class OpenRouterOptions
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string RecipeModel { get; set; } = "";
    public string EstimateModel { get; set; } = "";

    /// <summary>Reasoning models can think for minutes; without it they fit the time limit.</summary>
    public bool ReasoningEnabled { get; set; }

    /// <summary>Time limit for one user request, all attempts included.</summary>
    public int TimeoutSeconds { get; set; }
    public int MaxAttempts { get; set; }
}

/// <summary>The model gave no valid answer in time. Details are already logged.</summary>
public class OpenRouterException(string message) : Exception(message);

/// <summary>Asks OpenRouter for JSON that matches a C# type and passes the caller's checks.</summary>
public class OpenRouterClient(
    HttpClient http,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterClient> logger
)
{
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    // Numbers must stay plain numbers in the schema, so no reading from strings here.
    private static readonly JsonSerializerOptions SchemaOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <param name="validate">Returns an error message for a wrong answer, or null.</param>
    public async Task<T> GenerateAsync<T>(
        string model,
        string instructions,
        string prompt,
        string? imageDataUrl,
        Func<T, string?> validate,
        CancellationToken ct
    )
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw Fail("OpenRouter:ApiKey is not configured");
        }

        object userContent = imageDataUrl is null
            ? prompt
            : new object[]
            {
                new { type = "text", text = prompt },
                new { type = "image_url", image_url = new { url = imageDataUrl } },
            };
        var messages = new List<object>
        {
            new { role = "system", content = instructions },
            new { role = "user", content = userContent },
        };
        var responseFormat = new
        {
            type = "json_schema",
            json_schema = new
            {
                name = typeof(T).Name,
                strict = true,
                schema = SchemaFor<T>(),
            },
        };

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        for (var attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            try
            {
                var body = new
                {
                    model,
                    messages,
                    response_format = responseFormat,
                    reasoning = new { enabled = settings.ReasoningEnabled },
                };
                var reply = await SendAsync(body, settings.ApiKey, deadline.Token);
                var result =
                    JsonSerializer.Deserialize<T>(ExtractJson(reply), ReadOptions)
                    ?? throw new JsonException("Reply is null");

                var error = validate(result);
                if (error is null)
                {
                    return result;
                }

                logger.LogWarning(
                    "{Model} attempt {Attempt}: reply rejected: {Error}",
                    model,
                    attempt,
                    error
                );
                // Ask the model to fix its own answer rather than start over.
                messages.Add(new { role = "assistant", content = reply });
                messages.Add(
                    new
                    {
                        role = "user",
                        content = $"Відповідь не пройшла перевірку: {error}. Виправ її та поверни повну відповідь.",
                    }
                );
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw Fail($"{model} did not answer in {settings.TimeoutSeconds} s");
            }
            catch (Exception e) when (e is HttpRequestException or JsonException)
            {
                logger.LogWarning(e, "{Model} attempt {Attempt} failed", model, attempt);
            }
        }

        throw Fail($"{model} gave no valid answer in {settings.MaxAttempts} attempts");
    }

    private async Task<string> SendAsync(object body, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var message =
                $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}";
            // Other client errors (bad key, no credits, unsupported request) will not pass on retry.
            var isClientError = (int)response.StatusCode is >= 400 and < 500;
            throw isClientError && response.StatusCode != HttpStatusCode.TooManyRequests
                ? Fail(message)
                : new HttpRequestException(message);
        }

        var completion = await response.Content.ReadFromJsonAsync<Completion>(ReadOptions, ct);
        return completion?.Choices?.FirstOrDefault()?.Message?.Content
            ?? throw new JsonException("Reply has no content");
    }

    private OpenRouterException Fail(string message)
    {
        logger.LogError("OpenRouter request failed: {Message}", message);
        return new OpenRouterException(message);
    }

    // Some models wrap JSON in a Markdown code block.
    private static string ExtractJson(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        return start >= 0 && end > start ? reply[start..(end + 1)] : reply;
    }

    private static JsonNode SchemaFor<T>() =>
        SchemaOptions.GetJsonSchemaAsNode(
            typeof(T),
            new JsonSchemaExporterOptions
            {
                TreatNullObliviousAsNonNullable = true,
                // Strict structured outputs require every object to be closed.
                TransformSchemaNode = (_, node) =>
                {
                    if (node is JsonObject obj && obj.ContainsKey("properties"))
                    {
                        obj["additionalProperties"] = false;
                    }
                    return node;
                },
            }
        );

    private record Completion(List<Choice>? Choices);

    private record Choice(Message? Message);

    private record Message(string? Content);
}
