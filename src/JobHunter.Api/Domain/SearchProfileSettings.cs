using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Domain;

internal sealed record SearchProfileSettings(
    string Name,
    bool IsActive,
    IReadOnlyList<string> RequiredKeywords,
    IReadOnlyList<string> PreferredKeywords,
    IReadOnlyList<string> ExcludedKeywords,
    IReadOnlyList<string> ExcludedDescriptionKeywords,
    IReadOnlyList<string> Locations,
    IReadOnlyList<WorkModel> WorkModels,
    IReadOnlyList<string> Seniorities,
    int MinScore,
    ScoreWeights Weights);
