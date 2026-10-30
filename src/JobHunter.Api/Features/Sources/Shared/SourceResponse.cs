using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;

namespace JobHunter.Api.Features.Sources.Shared;

internal sealed record SourceResponse(
    Guid Id,
    string ConnectorKey,
    SourceKind Kind,
    string? TenantRef,
    string DisplayName,
    bool IsActive,
    bool ConnectorEnabled,
    TimeSpan? ScanInterval,
    DateTimeOffset? LastScanAt,
    DateTimeOffset NextScanAt,
    int ConsecutiveFailures,
    string? LastErrorCode)
{
    public static SourceResponse From(SourceTarget source, bool connectorEnabled) => new(
        source.Id,
        source.ConnectorKey,
        source.Kind,
        source.TenantRef,
        source.DisplayName,
        source.IsActive,
        connectorEnabled,
        source.ScanInterval,
        source.LastScanAt,
        source.NextScanAt,
        source.ConsecutiveFailures,
        source.LastErrorCode);
}
