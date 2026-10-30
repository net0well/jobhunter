using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Features.Matching.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Matching;

internal static class ListSearchProfiles
{
    internal sealed record Query : IRequest<IReadOnlyList<SearchProfileResponse>>;

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Query, IReadOnlyList<SearchProfileResponse>>
    {
        public async ValueTask<IReadOnlyList<SearchProfileResponse>> Handle(Query query, CancellationToken cancellationToken)
        {
            var profiles = await db.SearchProfiles.AsNoTracking().OrderBy(profile => profile.Name).ToListAsync(cancellationToken);
            return [.. profiles.Select(SearchProfileResponse.From)];
        }
    }
}
