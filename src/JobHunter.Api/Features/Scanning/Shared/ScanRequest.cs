namespace JobHunter.Api.Features.Scanning.Shared;

internal sealed record ScanRequest(Guid SourceTargetId, Guid ScanRunId);
