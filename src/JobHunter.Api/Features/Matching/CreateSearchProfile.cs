using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;
using JobHunter.Api.Features.Matching.Shared;
using Mediator;

namespace JobHunter.Api.Features.Matching;

internal static class CreateSearchProfile
{
    internal sealed record Command(SearchProfileRequest Profile) : IRequest<SearchProfileResponse>;

    internal sealed class Handler(AppDbContext db, TimeProvider clock) : IRequestHandler<Command, SearchProfileResponse>
    {
        public async ValueTask<SearchProfileResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var profile = SearchProfile.Create(command.Profile.ToSettings(), clock.GetUtcNow());
            db.SearchProfiles.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
            return SearchProfileResponse.From(profile);
        }
    }
}
