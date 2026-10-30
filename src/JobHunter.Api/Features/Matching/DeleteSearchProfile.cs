using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Features.Matching.Shared;
using Mediator;

namespace JobHunter.Api.Features.Matching;

internal static class DeleteSearchProfile
{
    internal sealed record Command(Guid Id) : IRequest<Result>;

    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, Result>
    {
        public async ValueTask<Result> Handle(Command command, CancellationToken cancellationToken)
        {
            var profile = await db.SearchProfiles.FindAsync([command.Id], cancellationToken);
            if (profile is null)
                return SearchProfileErrors.NotFound;

            db.SearchProfiles.Remove(profile);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
