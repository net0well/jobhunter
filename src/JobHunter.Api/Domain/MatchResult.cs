namespace JobHunter.Api.Domain;

internal sealed record MatchResult(int Score, IReadOnlyList<MatchReason> Reasons, string? DiscardReason);
