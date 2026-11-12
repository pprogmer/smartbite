using System.Net;
using System.Text;
using System.Text.Json;

namespace SmartBite.Tests.Fakes;

// підробка openrouter: тест сам задає відповіді, а запити запам'ятовуються. справжній сервіс не викликається
public class FakeOpenRouter : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _replies = new();

    // тіла запитів у тому порядку, як їх відправили
    public List<JsonDocument> Requests { get; } = [];

    // відповідь моделі, де content це об'єкт перетворений в json
    public FakeOpenRouter Reply(object content) => ReplyText(JsonSerializer.Serialize(content));

    // відповідь моделі з довільним текстом (наприклад json всередині markdown)
    public FakeOpenRouter ReplyText(string content)
    {
        var completion = JsonSerializer.Serialize(
            new { choices = new[] { new { message = new { content } } } }
        );
        return Enqueue(_ => Task.FromResult(Json(HttpStatusCode.OK, completion)));
    }

    public FakeOpenRouter ReplyStatus(HttpStatusCode status) =>
        Enqueue(_ => Task.FromResult(Json(status, """{"error":{"message":"fake"}}""")));

    // ніколи не відповідає, запит закінчується тільки по скасуванню
    public FakeOpenRouter Hang() =>
        Enqueue(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

    public void Reset()
    {
        _replies.Clear();
        Requests.Clear();
    }

    // текст останнього повідомлення в n-му запиті (промпт або прохання виправити)
    public string LastMessage(int request) =>
        Requests[request]
            .RootElement.GetProperty("messages")
            .EnumerateArray()
            .Last()
            .GetProperty("content")
            .ToString();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        Requests.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)));
        if (!_replies.TryDequeue(out var reply))
        {
            throw new InvalidOperationException("The test did not queue a reply for this request");
        }
        return await reply(ct);
    }

    private FakeOpenRouter Enqueue(Func<CancellationToken, Task<HttpResponseMessage>> reply)
    {
        _replies.Enqueue(reply);
        return this;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
