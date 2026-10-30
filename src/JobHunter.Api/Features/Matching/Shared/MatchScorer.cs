using JobHunter.Api.Common.Text;
using JobHunter.Api.Domain;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Matching.Shared;

// Rules that lack data on either side (no location in the posting, no seniorities in the profile...) are left out,
// and the score is normalized over the weights that did apply, so incomplete postings are not punished.
internal static class MatchScorer
{
    // Matching this many preferred keywords in the description already earns the full description weight.
    private const int PreferredKeywordsForFullScore = 3;

    private const int MaxPenalty = 30;

    private static readonly string[] SeniorityTerms =
        ["estagio", "estagiario", "trainee", "junior", "jr", "pleno", "senior", "sr", "especialista", "lead", "staff", "principal"];

    public static MatchResult Score(ScoringInput posting, SearchProfile profile)
    {
        if (posting.CompanyBlocked)
            return Discard("CompanyBlocked");

        var excluded = profile.ExcludedKeywords.FirstOrDefault(term => Normalizer.ContainsTerm(posting.Title, term));
        if (excluded is not null)
            return Discard($"ExcludedKeyword:{excluded}");

        if (profile.RequiredKeywords.Count > 0 && !profile.RequiredKeywords.Any(term => Mentions(posting, term)))
            return Discard("MissingRequired");

        var card = new ScoreCard(profile.Weights);

        ScoreTitle(card, posting, profile);
        ScoreDescription(card, posting, profile);

        if (!ScoreLocation(card, posting, profile))
            return Discard("LocationMismatch");

        ScoreWorkModel(card, posting, profile);
        ScoreSeniority(card, posting, profile);
        Penalize(card, posting, profile);

        return new MatchResult(card.Total, card.Reasons, null);
    }

    private static void ScoreTitle(ScoreCard card, ScoringInput posting, SearchProfile profile)
    {
        List<string> terms = [.. profile.RequiredKeywords, .. profile.PreferredKeywords];
        if (terms.Count > 0)
            card.Apply("Title", card.Weights.Title, terms.Any(term => Normalizer.ContainsTerm(posting.Title, term)) ? 1 : 0);
    }

    private static void ScoreDescription(ScoreCard card, ScoringInput posting, SearchProfile profile)
    {
        if (profile.PreferredKeywords.Count == 0 || string.IsNullOrWhiteSpace(posting.DescriptionText))
            return;

        var matched = profile.PreferredKeywords.Count(term => Normalizer.ContainsTerm(posting.DescriptionText, term));
        var needed = Math.Min(profile.PreferredKeywords.Count, PreferredKeywordsForFullScore);
        card.Apply("Description", card.Weights.Description, Math.Min(1.0, matched / (double)needed));
    }

    // Returns false when the posting is on-site or hybrid in a city the profile does not accept.
    private static bool ScoreLocation(ScoreCard card, ScoringInput posting, SearchProfile profile)
    {
        var city = Normalizer.City(posting.Location);
        var remote = IsRemote(posting);

        if (profile.Locations.Count == 0 || (city is null && !remote))
            return true;

        var cityAccepted = city is not null && profile.Locations.Any(location => (Normalizer.City(location) ?? Normalizer.Text(location)) == city);
        var remoteAccepted = profile.WorkModels.Count == 0 || profile.WorkModels.Contains(WorkModel.Remote);

        if (!cityAccepted && !remote)
            return false;

        card.Apply("Location", card.Weights.Location, cityAccepted || remoteAccepted ? 1 : 0);
        return true;
    }

    private static void ScoreWorkModel(ScoreCard card, ScoringInput posting, SearchProfile profile)
    {
        var workModel = posting.WorkModel ?? (IsRemote(posting) ? WorkModel.Remote : null);
        if (profile.WorkModels.Count > 0 && workModel is { } model)
            card.Apply("WorkModel", card.Weights.WorkModel, profile.WorkModels.Contains(model) ? 1 : 0);
    }

    private static void ScoreSeniority(ScoreCard card, ScoringInput posting, SearchProfile profile)
    {
        var text = posting.Seniority ?? (SeniorityTerms.Any(term => Normalizer.ContainsTerm(posting.Title, term)) ? posting.Title : null);
        if (profile.Seniorities.Count > 0 && text is not null)
            card.Apply("Seniority", card.Weights.Seniority, profile.Seniorities.Any(term => Normalizer.ContainsTerm(text, term)) ? 1 : 0);
    }

    private static void Penalize(ScoreCard card, ScoringInput posting, SearchProfile profile)
    {
        var penalized = profile.ExcludedDescriptionKeywords.Where(term => Normalizer.ContainsTerm(posting.DescriptionText, term)).ToList();
        var each = card.Weights.DescriptionPenalty;
        var remaining = MaxPenalty;

        foreach (var term in penalized)
        {
            var applied = Math.Min(each, remaining);
            card.Penalize($"ExcludedDescriptionKeyword:{term}", applied);
            remaining -= applied;
        }
    }

    private static bool Mentions(ScoringInput posting, string term) =>
        Normalizer.ContainsTerm(posting.Title, term) || Normalizer.ContainsTerm(posting.DescriptionText, term);

    private static bool IsRemote(ScoringInput posting) =>
        posting.WorkModel == WorkModel.Remote || Normalizer.MentionsRemote(posting.Location);

    private static MatchResult Discard(string reason) => new(0, [new MatchReason(reason, 0, true)], reason);

    private sealed class ScoreCard(ScoreWeights weights)
    {
        private double _earned;
        private int _applicable;
        private int _penalty;

        public ScoreWeights Weights { get; } = weights;

        public List<MatchReason> Reasons { get; } = [];

        public int Total =>
            Math.Clamp((_applicable == 0 ? 100 : (int)Math.Round(100 * _earned / _applicable)) - _penalty, 0, 100);

        public void Apply(string rule, int weight, double fraction)
        {
            _applicable += weight;
            _earned += weight * fraction;
            Reasons.Add(new MatchReason(rule, weight, fraction > 0));
        }

        public void Penalize(string rule, int points)
        {
            _penalty += points;
            Reasons.Add(new MatchReason(rule, -points, true));
        }
    }
}
