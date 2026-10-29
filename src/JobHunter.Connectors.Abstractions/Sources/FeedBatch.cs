namespace JobHunter.Connectors.Abstractions.Sources;

public sealed record FeedBatch(IReadOnlyList<SourcedPosting> Items, string? NextCheckpoint);
