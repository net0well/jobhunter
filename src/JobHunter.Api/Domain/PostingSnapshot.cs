using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Domain;

// What a source said about a posting at one point in time, before it is merged into the canonical JobPosting.
internal sealed record PostingSnapshot(
    string Title,
    string? Location,
    WorkModel? WorkModel,
    string? Seniority,
    string? EmploymentType,
    string? DescriptionText,
    Uri? ApplyUrl,
    DateTimeOffset? PublishedAt);
