using JobHunter.Api.Domain;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Matching.Shared;

internal sealed record ScoringInput(
    string Title,
    string? DescriptionText,
    string? Location,
    WorkModel? WorkModel,
    string? Seniority,
    bool CompanyBlocked)
{
    // Requires Company to be loaded.
    public static ScoringInput From(JobPosting posting) => new(
        posting.Title,
        posting.DescriptionText,
        posting.Location,
        posting.WorkModel,
        posting.Seniority,
        posting.Company!.IsBlocked);
}
