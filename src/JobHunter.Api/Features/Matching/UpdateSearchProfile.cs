using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Matching.Shared;
using Mediator;

namespace JobHunter.Api.Features.Matching;

internal static class UpdateSearchProfile
{
    internal sealed record Command(Guid Id, SearchProfileRequest Profile) : IRequest<Result<SearchProfileResponse>>;

    // Changing a profile does not rescore on its own; POST /api/search-profiles/rescore applies it to existing postings.
    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result<SearchProfileResponse>>
    {
        public async ValueTask<Result<SearchProfileResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var profile = await db.SearchProfiles.FindAsync([command.Id], cancellationToken);
            if (profile is null)
                return SearchProfileErrors.NotFound;

            profile.Update(command.Profile.ToSettings());
            await db.SaveChangesAsync(cancellationToken);
            return SearchProfileResponse.From(profile);
        }
    }
}
