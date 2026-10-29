using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Sources;

public interface IJobDetailsProvider
{
    bool CanHandle(Uri postingUrl);

    Task<ConnectorResult<PostingDetails>> GetDetailsAsync(Uri postingUrl, CancellationToken ct);
}
