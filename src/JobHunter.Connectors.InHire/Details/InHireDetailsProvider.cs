using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Api;
using JobHunter.Connectors.InHire.Parsing;

namespace JobHunter.Connectors.InHire.Details;

internal sealed class InHireDetailsProvider(InHireApiClient api) : IJobDetailsProvider
{
    public bool CanHandle(Uri postingUrl) => InHireUrls.TryParseJob(postingUrl, out _, out _);

    // The subdomain doubles as the tenant header: on every career page observed the tenant id is the slug itself.
    public async Task<ConnectorResult<PostingDetails>> GetDetailsAsync(Uri postingUrl, CancellationToken ct)
    {
        if (!InHireUrls.TryParseJob(postingUrl, out var tenant, out var jobId))
            return ConnectorResult<PostingDetails>.Fail(new ConnectorError(ConnectorErrorKind.InvalidInput, "InHire.InvalidJobUrl", $"'{postingUrl}' is not an InHire job URL."));

        return await api.ReadAsync($"job-posts/public/pages/{jobId}", tenant, $"job-{jobId}.json", InHireErrors.PostingClosed(jobId),
            json => InHireDetailsParser.Parse(tenant, json), ct);
    }
}
