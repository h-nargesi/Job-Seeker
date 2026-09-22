using System.Net;
using System.Text;

namespace AiWorker.Tests;

public sealed class FakeHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> script = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public List<string> Bodies { get; } = new();

    public int Remaining => script.Count;

    public void RespondJson(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        script.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }

    public void RespondNetworkError(string message = "unreachable")
    {
        script.Enqueue(_ => throw new HttpRequestException(message));
    }

    public void RespondTimeout()
    {
        script.Enqueue(_ => throw new TaskCanceledException("request canceled",
            new TimeoutException("the operation timed out")));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        if (request.Content is not null)
            Bodies.Add(await request.Content.ReadAsStringAsync(ct));

        if (script.Count == 0)
            throw new InvalidOperationException("no scripted response remaining");
        return script.Dequeue()(request);
    }
}

public static class FakeHttp
{
    public static HttpClient Client(FakeHandler handler, string baseAddress = "http://core.test/")
    {
        return new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
    }
}
