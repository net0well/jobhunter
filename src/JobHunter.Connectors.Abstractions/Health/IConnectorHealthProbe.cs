using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Health;

public interface IConnectorHealthProbe
{
    Task<ConnectorResult<bool>> ProbeAsync(CancellationToken ct);
}
