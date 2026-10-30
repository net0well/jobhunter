using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain.Enums;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Matching;

internal static class RescoreAll
{
    private const int BatchSize = 500;

    internal sealed record Command : IRequest<ScorePendingPostings.Response>;

    // Postings in an application, closed or discarded by hand keep their state.
    internal sealed class Handler(AppDbContext db) : IRequestHandler<Command, ScorePendingPostings.Response>
    {
        public async ValueTask<ScorePendingPostings.Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var profiles = await db.SearchProfiles.AsNoTracking().Where(profile => profile.IsActive).ToListAsync(cancellationToken);
            if (profiles.Count == 0)
                return new ScorePendingPostings.Response(0, 0, 0);

            int scored = 0, matched = 0, discarded = 0;
            Guid? after = null;

            while (true)
            {
                var batch = await db.JobPostings
                    .Include(posting => posting.Company)
                    .Where(posting => posting.Status == JobPostingStatus.New
                        || posting.Status == JobPostingStatus.Matched
                        || (posting.Status == JobPostingStatus.Discarded && !posting.IsManuallyDiscarded))
                    .Where(posting => after == null || posting.Id > after)
                    .OrderBy(posting => posting.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                    break;

                var result = ScorePendingPostings.Handler.Score(batch, profiles);
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();

                scored += result.Scored;
                matched += result.Matched;
                discarded += result.Discarded;
                after = batch[^1].Id;
            }

            return new ScorePendingPostings.Response(scored, matched, discarded);
        }
    }
}
