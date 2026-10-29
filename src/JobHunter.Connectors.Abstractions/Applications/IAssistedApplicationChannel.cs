using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Applications;

public interface IAssistedApplicationChannel
{
    Task<ConnectorResult<bool>> OpenAssistedAsync(ApplicationSubmission submission, CancellationToken ct);
}
