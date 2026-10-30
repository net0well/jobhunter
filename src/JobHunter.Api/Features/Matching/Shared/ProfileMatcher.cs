using JobHunter.Api.Domain;

namespace JobHunter.Api.Features.Matching.Shared;

internal static class ProfileMatcher
{
    // With several active profiles the posting keeps its best outcome: a match beats a low score, which beats a discard.
    public static (MatchResult Match, SearchProfile Profile) Best(ScoringInput posting, IReadOnlyList<SearchProfile> profiles)
    {
        var outcomes = profiles.Select(profile => (Match: MatchScorer.Score(posting, profile), Profile: profile)).ToList();

        return outcomes
            .OrderByDescending(outcome => outcome.Match.DiscardReason is null && outcome.Match.Score >= outcome.Profile.MinScore)
            .ThenByDescending(outcome => outcome.Match.DiscardReason is null)
            .ThenByDescending(outcome => outcome.Match.Score)
            .First();
    }
}
