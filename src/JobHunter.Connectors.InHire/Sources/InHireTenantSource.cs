using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Api;
using JobHunter.Connectors.InHire.Parsing;

namespace JobHunter.Connectors.InHire.Sources;

internal sealed class InHireTenantSource(InHireApiClient api) : ITenantJobSource
{
    public ConnectorResult<TenantRef> ParseTenant(string input) => InHireUrls.ParseTenant(input);

    public Uri GetTenantUrl(TenantRef tenant) => InHireUrls.CareerPage(tenant);

    // Same two calls the career page makes: resolve the company, then load every published job at once.
    public async Task<ConnectorResult<IReadOnlyList<SourcedPosting>>> ListAsync(TenantRef tenant, CancellationToken ct)
    {
        var notFound = new ConnectorError(ConnectorErrorKind.NotFound, "InHire.TenantNotFound", $"There is no InHire career page for '{tenant.Value}'.");

        var resolved = await api.ReadAsync($"tenants/public/config/resolve/{tenant.Value}", null, $"tenant-{tenant.Value}.json", notFound,
            json => InHireTenantParser.Parse(tenant.Value, json), ct);

        if (!resolved.IsSuccess)
            return ConnectorResult<IReadOnlyList<SourcedPosting>>.Fail(resolved.Error);

        var (tenantId, subdomain) = resolved.Value;
        return await api.ReadAsync("job-posts/public/pages", tenantId, $"listing-{tenant.Value}.json", notFound,
            json => InHireListingParser.Parse(subdomain, json), ct);
    }
}
