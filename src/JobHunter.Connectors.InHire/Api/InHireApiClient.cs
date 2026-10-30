using System.Net;
using System.Text;
using JobHunter.Connectors.Abstractions.Evidence;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.InHire.Parsing;

namespace JobHunter.Connectors.InHire.Api;

// Reads the public endpoints the career page itself calls and turns every HTTP outcome into a typed connector result.
internal sealed class InHireApiClient(IHttpClientFactory httpClients, IConnectorEvidenceStore evidence, TimeProvider clock)
{
    private const string TenantHeader = "X-Tenant";

    public async Task<ConnectorResult<T>> ReadAsync<T>(
        string path,
        string? tenant,
        string evidenceName,
        ConnectorError notFound,
        Func<string, ConnectorResult<T>> parse,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (tenant is not null)
            request.Headers.Add(TenantHeader, tenant);

        HttpResponseMessage response;
        try
        {
            response = await httpClients.CreateClient(InHireConnectorRegistration.HttpClientName).SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return Fail<T>(ConnectorErrorKind.Transient, "InHire.Network", $"InHire could not be reached: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail<T>(ConnectorErrorKind.Transient, "InHire.Timeout", "InHire did not answer in time.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (IsChallenge(response))
                return await WithEvidenceAsync<T>(new ConnectorError(ConnectorErrorKind.CaptchaDetected, "InHire.Challenge", "InHire answered with an anti-bot challenge instead of data."), evidenceName + ".html", body, ct);

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return ConnectorResult<T>.Fail(notFound);
                case HttpStatusCode.TooManyRequests:
                    return ConnectorResult<T>.Fail(new ConnectorError(ConnectorErrorKind.RateLimited, "InHire.RateLimited", "InHire asked to slow down.", RetryAfter: RetryAfter(response)));
                case >= HttpStatusCode.InternalServerError:
                    return Fail<T>(ConnectorErrorKind.Transient, "InHire.ServerError", $"InHire answered {(int)response.StatusCode}.");
                case var status when !response.IsSuccessStatusCode:
                    return await WithEvidenceAsync<T>(InHireErrors.LayoutChanged($"Unexpected status {(int)status} from {path}."), evidenceName, body, ct);
            }

            var parsed = parse(body);
            return parsed.Error is { Kind: ConnectorErrorKind.LayoutChanged } layout
                ? await WithEvidenceAsync<T>(layout, evidenceName, body, ct)
                : parsed;
        }
    }

    // Cloudflare marks challenges with cf-mitigated; any HTML where JSON is expected is treated the same way.
    private static bool IsChallenge(HttpResponseMessage response) =>
        response.Headers.Contains("cf-mitigated")
        || response.Content.Headers.ContentType?.MediaType?.Equals("text/html", StringComparison.OrdinalIgnoreCase) == true;

    private TimeSpan? RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } when date > clock.GetUtcNow() => date - clock.GetUtcNow(),
        _ => null,
    };

    private async Task<ConnectorResult<T>> WithEvidenceAsync<T>(ConnectorError error, string name, string body, CancellationToken ct)
    {
        var path = await evidence.SaveAsync(InHireConnectorRegistration.Key, name, Encoding.UTF8.GetBytes(body), ct);
        return ConnectorResult<T>.Fail(error with { EvidencePath = path });
    }

    private static ConnectorResult<T> Fail<T>(ConnectorErrorKind kind, string code, string message) =>
        ConnectorResult<T>.Fail(new ConnectorError(kind, code, message));
}
