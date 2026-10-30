using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Features.Scanning.Shared;

internal sealed record ScanRunResponse(
    Guid Id,
    Guid SourceTargetId,
    string ConnectorKey,
    ScanRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int Found,
    int Created,
    int Updated,
    int Deduplicated,
    int Closed,
    ConnectorErrorKind? ErrorKind,
    string? ErrorMessage,
    string? EvidencePath)
{
    public static ScanRunResponse From(ScanRun run) => new(
        run.Id,
        run.SourceTargetId,
        run.ConnectorKey,
        run.Status,
        run.StartedAt,
        run.FinishedAt,
        run.Found,
        run.Created,
        run.Updated,
        run.Deduplicated,
        run.Closed,
        run.ErrorKind,
        run.ErrorMessage,
        run.EvidencePath);
}
