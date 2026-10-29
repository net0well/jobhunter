using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Sources;

public interface ITenantDiscovery
{
    Task<ConnectorResult<IReadOnlyList<DiscoveredTenant>>> DiscoverAsync(string query, CancellationToken ct);
}
