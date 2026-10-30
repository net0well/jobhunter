namespace JobHunter.Api.Domain;

internal sealed record MatchReason(string Rule, int Weight, bool Matched);
