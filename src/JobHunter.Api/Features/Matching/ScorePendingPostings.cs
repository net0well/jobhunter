using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Matching.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Matching;

internal static class ScorePendingPostings
{
    internal sealed record Command(int BatchSize) : IRequest<Response>;

    internal sealed record Response(int Scored, int Matched, int Discarded);

    // Without an active profile there is nothing to compare against, so new postings simply wait.
    internal sealed class Handler(AppDbContext db, ILogger<Handler> logger) : IRequestHandler<Command, Response>
    {
        public async ValueTask<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var profiles = await db.SearchProfiles.AsNoTracking().Where(profile => profile.IsActive).ToListAsync(cancellationToken);
            if (profiles.Count == 0)
                return new Response(0, 0, 0);

            var postings = await db.JobPostings
                .Include(posting => posting.Company)
                .Where(posting => posting.Status == JobPostingStatus.New)
                .OrderBy(posting => posting.FirstSeenAt)
                .Take(command.BatchSize)
                .ToListAsync(cancellationToken);

            var response = Score(postings, profiles);
            await db.SaveChangesAsync(cancellationToken);

            if (response.Scored > 0)
                logger.LogInformation("Scored {Scored} postings: {Matched} matched, {Discarded} discarded", response.Scored, response.Matched, response.Discarded);

            return response;
        }

        internal static Response Score(IReadOnlyList<JobPosting> postings, IReadOnlyList<SearchProfile> profiles)
        {
            foreach (var posting in postings)
            {
                var (result, profile) = ProfileMatcher.Best(ScoringInput.From(posting), profiles);
                posting.ApplyMatch(result, profile.Id, profile.MinScore);
            }

            return new Response(
                postings.Count,
                postings.Count(posting => posting.Status == JobPostingStatus.Matched),
                postings.Count(posting => posting.Status == JobPostingStatus.Discarded));
        }
    }
}
