using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Postings.Shared;
using Mediator;

namespace JobHunter.Api.Features.Postings;

internal static class GetPosting
{
    internal sealed record Query(Guid Id) : IRequest<Result<PostingDetail>>;

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Query, Result<PostingDetail>>
    {
        public async ValueTask<Result<PostingDetail>> Handle(Query query, CancellationToken cancellationToken)
        {
            var posting = await db.FindWithDetailsAsync(query.Id, cancellationToken);
            return posting is null ? PostingErrors.NotFound : PostingDetail.From(posting);
        }
    }
}
