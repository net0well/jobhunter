using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.Abstractions.Sources;

public interface ISearchJobSource
{
    Task<ConnectorResult<SearchPage>> SearchAsync(SearchCriteria criteria, string? cursor, CancellationToken ct);
}
