using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Domain;

internal sealed class ScanRun
{
    public const int ErrorMessageMaxLength = 1000;

    private ScanRun(Guid id, Guid sourceTargetId, string connectorKey, DateTimeOffset startedAt, ScanRunStatus status)
    {
        Id = id;
        SourceTargetId = sourceTargetId;
        ConnectorKey = connectorKey;
        StartedAt = startedAt;
        Status = status;
    }

    public Guid Id { get; private set; }

    public Guid SourceTargetId { get; private set; }

    public string ConnectorKey { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public ScanRunStatus Status { get; private set; }

    public int Found { get; private set; }

    public int Created { get; private set; }

    public int Updated { get; private set; }

    public int Deduplicated { get; private set; }

    public int Closed { get; private set; }

    public ConnectorErrorKind? ErrorKind { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? EvidencePath { get; private set; }

    public static ScanRun Start(SourceTarget source, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), source.Id, source.ConnectorKey, now, ScanRunStatus.Running);

    // Partial means the listing worked but follow-up calls (such as posting details) failed.
    public void Complete(ScanCounts counts, ConnectorError? partialFailure, DateTimeOffset now)
    {
        Found = counts.Found;
        Created = counts.Created;
        Updated = counts.Updated;
        Deduplicated = counts.Deduplicated;
        Closed = counts.Closed;
        Status = partialFailure is null ? ScanRunStatus.Succeeded : ScanRunStatus.PartiallySucceeded;
        RecordError(partialFailure);
        FinishedAt = now;
    }

    public void Fail(ConnectorError error, DateTimeOffset now)
    {
        Status = ScanRunStatus.Failed;
        RecordError(error);
        FinishedAt = now;
    }

    private void RecordError(ConnectorError? error)
    {
        if (error is null)
            return;

        ErrorKind = error.Kind;
        ErrorMessage = error.Message.Length <= ErrorMessageMaxLength ? error.Message : error.Message[..ErrorMessageMaxLength];
        EvidencePath = error.EvidencePath;
    }
}
