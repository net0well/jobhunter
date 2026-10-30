using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Text;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Postings.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Postings;

internal static class ListPostings
{
    internal sealed record Query(JobPostingStatus? Status, string? Connector, int? MinScore, string? Q, int? Page, int? PageSize)
        : IRequest<PagedResponse<PostingSummary>>;

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Query, PagedResponse<PostingSummary>>
    {
        public async ValueTask<PagedResponse<PostingSummary>> Handle(Query query, CancellationToken cancellationToken)
        {
            var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
            var postings = db.JobPostings.AsNoTracking();

            if (query.Status is not null)
                postings = postings.Where(posting => posting.Status == query.Status);

            if (query.Connector is not null)
                postings = postings.Where(posting => posting.Sources.Any(source => source.ConnectorKey == query.Connector));

            if (query.MinScore is not null)
                postings = postings.Where(posting => posting.MatchScore >= query.MinScore);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var term = Normalizer.Text(query.Q);
                postings = postings.Where(posting => posting.NormalizedTitle.Contains(term) || posting.Company!.NormalizedName.Contains(term));
            }

            var total = await postings.CountAsync(cancellationToken);

            // PostgreSQL sorts NULL first on descending order; unscored postings go last instead.
            var items = await postings
                .OrderByDescending(posting => posting.MatchScore.HasValue)
                .ThenByDescending(posting => posting.MatchScore)
                .ThenByDescending(posting => posting.FirstSeenAt)
                .Page(page, pageSize)
                .Select(posting => new PostingSummary(
                    posting.Id,
                    posting.Title,
                    posting.Company!.Name,
                    posting.Location,
                    posting.WorkModel,
                    posting.Status,
                    posting.MatchScore,
                    posting.ApplyChannelKey,
                    posting.ApplyUrl,
                    posting.FirstSeenAt,
                    posting.LastSeenAt))
                .ToListAsync(cancellationToken);

            return new PagedResponse<PostingSummary>(items, page, pageSize, total);
        }
    }
}
