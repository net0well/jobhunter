using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Domain;

internal sealed class SearchProfile
{
    public const int DefaultMinScore = 60;

    private SearchProfile(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public List<string> RequiredKeywords { get; private set; } = [];

    public List<string> PreferredKeywords { get; private set; } = [];

    public List<string> ExcludedKeywords { get; private set; } = [];

    public List<string> ExcludedDescriptionKeywords { get; private set; } = [];

    public List<string> Locations { get; private set; } = [];

    public List<WorkModel> WorkModels { get; private set; } = [];

    public List<string> Seniorities { get; private set; } = [];

    public int MinScore { get; private set; } = DefaultMinScore;

    public ScoreWeights Weights { get; private set; } = new();

    public static SearchProfile Create(SearchProfileSettings settings, DateTimeOffset now)
    {
        var profile = new SearchProfile(Guid.CreateVersion7(now), settings.Name);
        profile.Update(settings);
        return profile;
    }

    public void Update(SearchProfileSettings settings)
    {
        Name = settings.Name.Trim();
        IsActive = settings.IsActive;
        RequiredKeywords = Clean(settings.RequiredKeywords);
        PreferredKeywords = Clean(settings.PreferredKeywords);
        ExcludedKeywords = Clean(settings.ExcludedKeywords);
        ExcludedDescriptionKeywords = Clean(settings.ExcludedDescriptionKeywords);
        Locations = Clean(settings.Locations);
        WorkModels = [.. settings.WorkModels.Distinct()];
        Seniorities = Clean(settings.Seniorities);
        MinScore = settings.MinScore;
        Weights = settings.Weights;
    }

    private static List<string> Clean(IReadOnlyList<string> terms) =>
        [.. terms.Select(term => term.Trim()).Where(term => term.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
}
