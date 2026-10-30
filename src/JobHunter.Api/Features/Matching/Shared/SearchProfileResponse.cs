using JobHunter.Api.Domain;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Matching.Shared;

internal sealed record SearchProfileResponse(
    Guid Id,
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
    ScoreWeights Weights)
{
    public static SearchProfileResponse From(SearchProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.IsActive,
        profile.RequiredKeywords,
        profile.PreferredKeywords,
        profile.ExcludedKeywords,
        profile.ExcludedDescriptionKeywords,
        profile.Locations,
        profile.WorkModels,
        profile.Seniorities,
        profile.MinScore,
        profile.Weights);
}
