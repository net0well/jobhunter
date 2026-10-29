using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Sources;

public interface IFeedJobSource
{
    Task<ConnectorResult<FeedBatch>> PullAsync(string? checkpoint, CancellationToken ct);
}
