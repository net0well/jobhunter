using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Scanning.Shared;
using JobHunter.Api.Features.Sources.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Sources;

internal static class GetSource
{
    public const int RecentRuns = 10;

    internal sealed record Query(Guid Id) : IRequest<Result<Response>>;

    internal sealed record Response(SourceResponse Source, IReadOnlyList<ScanRunResponse> RecentRuns);

    internal sealed class Handler(AppDbContext db, IConnectorRegistry registry) : IRequestHandler<Query, Result<Response>>
    {
        public async ValueTask<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var source = await db.SourceTargets.AsNoTracking().FirstOrDefaultAsync(source => source.Id == query.Id, cancellationToken);
            if (source is null)
                return Error.NotFound("Source.NotFound", "Source not found.");

            var runs = await db.ScanRuns.AsNoTracking()
                .Where(run => run.SourceTargetId == query.Id)
                .OrderByDescending(run => run.StartedAt)
                .Take(RecentRuns)
                .ToListAsync(cancellationToken);

            return new Response(SourceResponse.From(source, registry.Find(source.ConnectorKey) is not null), [.. runs.Select(ScanRunResponse.From)]);
        }
    }
}
