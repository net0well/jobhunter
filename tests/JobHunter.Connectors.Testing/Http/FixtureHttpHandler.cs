using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace JobHunter.Connectors.Testing.Http;

// Answers HTTP connector requests from fixtures. Any request without a matching route throws, so no test reaches the internet.
public sealed class FixtureHttpHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Route> _routes = new();

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public FixtureHttpHandler Respond(
        string url,
        HttpStatusCode status,
        string body,
        string contentType = "application/json",
        Func<HttpRequestMessage, bool>? when = null,
        IReadOnlyDictionary<string, string>? headers = null) =>
        Respond(url, when, _ => Build(status, body, contentType, headers));

    public FixtureHttpHandler RespondWithFile(string url, string path, Func<HttpRequestMessage, bool>? when = null) =>
        Respond(url, HttpStatusCode.OK, File.ReadAllText(path), "application/json", when);

    public FixtureHttpHandler Fail(string url, Exception exception, Func<HttpRequestMessage, bool>? when = null) =>
        Respond(url, when, _ => throw exception);

    public FixtureHttpHandler Respond(string url, Func<HttpRequestMessage, bool>? when, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes.Enqueue(new Route(new Uri(url), when ?? (_ => true), respond));
        return this;
    }

    public void Clear()
    {
        _routes.Clear();
        Requests.Clear();
    }

    // Matches a request by one of its headers, e.g. a tenant header on endpoints shared by every tenant.
    public static Func<HttpRequestMessage, bool> WithHeader(string name, string value) =>
        request => request.Headers.TryGetValues(name, out var values) && values.Contains(value, StringComparer.OrdinalIgnoreCase);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(RecordedRequest.From(request));

        // Later registrations win, so a test can override a default route.
        var route = _routes.Reverse().FirstOrDefault(candidate => candidate.Url == request.RequestUri && candidate.When(request))
            ?? throw new InvalidOperationException($"No fixture for {request.Method} {request.RequestUri}");

        return Task.FromResult(route.Respond(request));
    }

    private static HttpResponseMessage Build(HttpStatusCode status, string body, string contentType, IReadOnlyDictionary<string, string>? headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
                response.Content.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    private sealed record Route(Uri Url, Func<HttpRequestMessage, bool> When, Func<HttpRequestMessage, HttpResponseMessage> Respond);
}
