using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartBite.Api.Services;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Unit;

// повтори, виправлення і помилки клієнта моделі, з фейковим http
public class OpenRouterClientTests
{
    private record Answer(string Text, int Number);

    private readonly FakeOpenRouter _api = new();

    private OpenRouterClient Client(string apiKey = "test-key", int timeoutSeconds = 5) =>
        new(
            new HttpClient(_api) { BaseAddress = new Uri("https://openrouter.test/api/v1/") },
            Options.Create(
                new OpenRouterOptions
                {
                    ApiKey = apiKey,
                    TimeoutSeconds = timeoutSeconds,
                    MaxAttempts = 3,
                }
            ),
            NullLogger<OpenRouterClient>.Instance
        );

    private Task<Answer> Ask(OpenRouterClient client, Func<Answer, string?>? validate = null) =>
        client.GenerateAsync(
            "test/model",
            "instructions",
            "prompt",
            imageDataUrl: null,
            validate ?? (_ => null),
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task ValidReply_IsReturned()
    {
        _api.Reply(new { text = "ok", number = 1 });

        var answer = await Ask(Client());

        Assert.Equal(new Answer("ok", 1), answer);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task Request_AsksForStrictJsonSchemaOfTheType()
    {
        _api.Reply(new { text = "ok", number = 1 });

        await Ask(Client());

        var body = _api.Requests[0].RootElement;
        Assert.Equal("test/model", body.GetProperty("model").GetString());
        var format = body.GetProperty("response_format").GetProperty("json_schema");
        Assert.Equal("Answer", format.GetProperty("name").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        var schema = format.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.True(schema.GetProperty("properties").TryGetProperty("number", out _));
    }

    [Fact]
    public async Task JsonWrappedInMarkdown_IsUnwrapped()
    {
        _api.ReplyText("Ось відповідь:\n```json\n{\"text\":\"ok\",\"number\":2}\n```");

        var answer = await Ask(Client());

        Assert.Equal(2, answer.Number);
    }

    [Fact]
    public async Task RejectedReply_IsSentBackForCorrection()
    {
        _api.Reply(new { text = "bad", number = -1 }).Reply(new { text = "good", number = 5 });

        var answer = await Ask(Client(), a => a.Number < 0 ? "число від'ємне" : null);

        Assert.Equal("good", answer.Text);
        Assert.Equal(2, _api.Requests.Count);
        // другий запит продовжує розмову і пояснює, що було не так
        Assert.Contains("число від'ємне", _api.LastMessage(1));
        Assert.Equal(4, _api.Requests[1].RootElement.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task NotJson_AndServerErrors_AreRetried()
    {
        _api.ReplyText("вибачте, не можу")
            .ReplyStatus(HttpStatusCode.ServiceUnavailable)
            .Reply(new { text = "ok", number = 3 });

        var answer = await Ask(Client());

        Assert.Equal(3, answer.Number);
        Assert.Equal(3, _api.Requests.Count);
    }

    [Fact]
    public async Task TooManyRequests_IsRetried()
    {
        _api.ReplyStatus(HttpStatusCode.TooManyRequests).Reply(new { text = "ok", number = 1 });

        await Ask(Client());

        Assert.Equal(2, _api.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] // неправильний ключ
    [InlineData(HttpStatusCode.PaymentRequired)] // закінчились кредити
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ClientErrors_FailAtOnce(HttpStatusCode status)
    {
        _api.ReplyStatus(status);

        var e = await Assert.ThrowsAsync<OpenRouterException>(() => Ask(Client()));

        Assert.Contains($"HTTP {(int)status}", e.Message);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task NoValidReplyInAllAttempts_Fails()
    {
        _api.ReplyText("{}").ReplyText("not json").ReplyStatus(HttpStatusCode.BadGateway);

        var e = await Assert.ThrowsAsync<OpenRouterException>(() =>
            Ask(Client(), _ => "always wrong")
        );

        Assert.Contains("in 3 attempts", e.Message);
        Assert.Equal(3, _api.Requests.Count);
    }

    [Fact]
    public async Task MissingApiKey_FailsWithoutCallingTheService()
    {
        await Assert.ThrowsAsync<OpenRouterException>(() => Ask(Client(apiKey: " ")));

        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task SlowModel_FailsAfterTheTimeLimit()
    {
        _api.Hang();

        var e = await Assert.ThrowsAsync<OpenRouterException>(() => Ask(Client(timeoutSeconds: 1)));

        Assert.Contains("did not answer in 1 s", e.Message);
    }

    [Fact]
    public async Task CancelledByTheUser_IsNotReportedAsAFailure()
    {
        _api.Hang();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client().GenerateAsync<Answer>("m", "i", "p", null, _ => null, cts.Token)
        );
    }

    [Fact]
    public async Task Photo_IsSentAsImageUrlPart()
    {
        _api.Reply(new { text = "ok", number = 1 });

        await Client()
            .GenerateAsync<Answer>(
                "m",
                "i",
                "prompt",
                "data:image/png;base64,AAAA",
                _ => null,
                TestContext.Current.CancellationToken
            );

        var content = _api.Requests[0]
            .RootElement.GetProperty("messages")[1]
            .GetProperty("content");
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal(
            "data:image/png;base64,AAAA",
            content[1].GetProperty("image_url").GetProperty("url").GetString()
        );
    }
}
