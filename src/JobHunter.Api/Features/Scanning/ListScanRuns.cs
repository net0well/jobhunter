using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Scanning.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Scanning;

internal static class ListScanRuns
{
    internal sealed record Query(Guid? SourceId, string? Connector, ScanRunStatus? Status, int? Page, int? PageSize)
        : IRequest<PagedResponse<ScanRunResponse>>;

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Query, PagedResponse<ScanRunResponse>>
    {
        public async ValueTask<PagedResponse<ScanRunResponse>> Handle(Query query, CancellationToken cancellationToken)
        {
            var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
            var runs = db.ScanRuns.AsNoTracking();

            if (query.SourceId is not null)
                runs = runs.Where(run => run.SourceTargetId == query.SourceId);

            if (query.Connector is not null)
                runs = runs.Where(run => run.ConnectorKey == query.Connector);

            if (query.Status is not null)
                runs = runs.Where(run => run.Status == query.Status);

            var total = await runs.CountAsync(cancellationToken);
            var items = await runs.OrderByDescending(run => run.StartedAt).Page(page, pageSize).ToListAsync(cancellationToken);

            return new PagedResponse<ScanRunResponse>([.. items.Select(ScanRunResponse.From)], page, pageSize, total);
        }
    }
}
