using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Features.Sources.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Sources;

internal static class ListSources
{
    internal sealed record Query(string? Connector, bool? Active) : IRequest<IReadOnlyList<SourceResponse>>;

    internal sealed class Handler(AppDbContext db, IConnectorRegistry registry) : IRequestHandler<Query, IReadOnlyList<SourceResponse>>
    {
        public async ValueTask<IReadOnlyList<SourceResponse>> Handle(Query query, CancellationToken cancellationToken)
        {
            var sources = db.SourceTargets.AsNoTracking();

            if (query.Connector is not null)
                sources = sources.Where(source => source.ConnectorKey == query.Connector);

            if (query.Active is not null)
                sources = sources.Where(source => source.IsActive == query.Active);

            var list = await sources.OrderBy(source => source.DisplayName).ToListAsync(cancellationToken);
            return [.. list.Select(source => SourceResponse.From(source, registry.Find(source.ConnectorKey) is not null))];
        }
    }
}
