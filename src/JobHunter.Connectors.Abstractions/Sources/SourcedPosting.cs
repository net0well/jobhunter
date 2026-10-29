namespace JobHunter.Connectors.Abstractions.Sources;

public sealed record SourcedPosting(
    string ExternalId,
    Uri SourceUrl,
    string Title,
    string? CompanyName,
    string? Location,
    WorkModel? WorkModel,
    string? Seniority,
    string? EmploymentType,
    DateTimeOffset? PublishedAt,
    string? DescriptionText,
    Uri? ApplyUrl,
    IReadOnlyDictionary<string, string> Extra);
