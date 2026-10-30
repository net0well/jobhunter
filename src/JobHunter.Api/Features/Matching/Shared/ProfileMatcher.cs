using JobHunter.Api.Domain;

namespace JobHunter.Api.Features.Matching.Shared;

internal static class ProfileMatcher
{
    // With several active profiles the posting keeps its best outcome: a match beats a low score, which beats a discard.
    public static (MatchResult Result, SearchProfile Profile) Best(ScoringInput posting, IReadOnlyList<SearchProfile> profiles)
    {
        var outcomes = profiles.Select(profile => (Result: MatchScorer.Score(posting, profile), Profile: profile)).ToList();

        return outcomes
            .OrderByDescending(outcome => outcome.Result.DiscardReason is null && outcome.Result.Score >= outcome.Profile.MinScore)
            .ThenByDescending(outcome => outcome.Result.DiscardReason is null)
            .ThenByDescending(outcome => outcome.Result.Score)
            .First();
    }
}
