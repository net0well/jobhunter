namespace JobHunter.Connectors.Abstractions.Sources;

public sealed record SearchPage(IReadOnlyList<SourcedPosting> Items, string? NextCursor);
