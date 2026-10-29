using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Sources;

public interface ITenantJobSource
{
    ConnectorResult<TenantRef> ParseTenant(string input);

    Uri GetTenantUrl(TenantRef tenant);

    Task<ConnectorResult<IReadOnlyList<SourcedPosting>>> ListAsync(TenantRef tenant, CancellationToken ct);
}
