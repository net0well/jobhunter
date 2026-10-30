namespace JobHunter.Api.Features.Postings.Shared;

internal sealed record UpsertOutcome(
    int Created,
    int Updated,
    int Deduplicated,
    IReadOnlyList<CreatedPosting> CreatedPostings,
    IReadOnlySet<Guid> SeenSourceIds);
