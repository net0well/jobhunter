namespace JobHunter.Connectors.Abstractions.Results;

public sealed record ConnectorError(
    ConnectorErrorKind Kind,
    string Code,
    string Message,
    string? EvidencePath = null,
    TimeSpan? RetryAfter = null);
