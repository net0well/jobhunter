namespace JobHunter.Connectors.Testing.Http;

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers)
{
    public static RecordedRequest From(HttpRequestMessage request) => new(
        request.Method,
        request.RequestUri!,
        request.Headers.ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase));
}
