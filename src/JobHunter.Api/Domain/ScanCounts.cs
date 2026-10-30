namespace JobHunter.Api.Domain;

internal sealed record ScanCounts(int Found, int Created, int Updated, int Deduplicated, int Closed);
