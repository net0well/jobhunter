namespace JobHunter.Connectors.Abstractions.Sources;

public sealed record SearchCriteria(
    IReadOnlyList<string> Keywords,
    string? Location,
    IReadOnlyList<WorkModel> WorkModels,
    int MaxResults);
