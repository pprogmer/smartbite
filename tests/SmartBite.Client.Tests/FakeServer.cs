using System.Net;
using System.Net.Http;
using System.Text;
using SmartBite.Client.Services;

namespace SmartBite.Client.Tests;

// підробка нашого api: на кожен запит віддає наступну відповідь з черги
public class FakeServer : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _replies = new();

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    public ApiClient Client() =>
        new(new HttpClient(this) { BaseAddress = new Uri("http://api.test/") });

    public FakeServer Reply(
        HttpStatusCode status,
        string json = "{}",
        string mediaType = "application/json"
    )
    {
        _replies.Enqueue(() =>
            new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, mediaType),
            }
        );
        return this;
    }

    public FakeServer Throw(Exception e)
    {
        _replies.Enqueue(() => throw e);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        return _replies.Dequeue()();
    }
}
