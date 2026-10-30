using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Domain;

internal sealed class SourceTarget
{
    public const int DisplayNameMaxLength = 200;

    private SourceTarget(Guid id, string connectorKey, SourceKind kind, string? tenantRef, string displayName, DateTimeOffset nextScanAt)
    {
        Id = id;
        ConnectorKey = connectorKey;
        Kind = kind;
        TenantRef = tenantRef;
        DisplayName = displayName;
        NextScanAt = nextScanAt;
    }

    public Guid Id { get; private set; }

    public string ConnectorKey { get; private set; }

    public SourceKind Kind { get; private set; }

    public string? TenantRef { get; private set; }

    public SearchCriteria? SearchCriteria { get; private set; }

    public string? Checkpoint { get; private set; }

    public Guid? CompanyId { get; private set; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; }

    public TimeSpan? ScanInterval { get; private set; }

    public DateTimeOffset? LastScanAt { get; private set; }

    public DateTimeOffset NextScanAt { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public string? LastErrorCode { get; private set; }

    public uint Version { get; private set; }

    public static SourceTarget Create(
        string connectorKey,
        SourceKind kind,
        string? tenantRef,
        SearchCriteria? searchCriteria,
        string displayName,
        TimeSpan? scanInterval,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), connectorKey, kind, tenantRef, displayName, now)
        {
            SearchCriteria = searchCriteria,
            ScanInterval = scanInterval,
            IsActive = true,
        };

    public void Rename(string displayName) => DisplayName = displayName;

    public void ChangeScanInterval(TimeSpan? scanInterval) => ScanInterval = scanInterval;

    public void AssignCompany(Guid companyId) => CompanyId ??= companyId;

    public void UpdateCheckpoint(string? checkpoint) => Checkpoint = checkpoint;

    public void RecordSuccess(DateTimeOffset now, TimeSpan interval)
    {
        LastScanAt = now;
        NextScanAt = now + interval;
        ConsecutiveFailures = 0;
        LastErrorCode = null;
    }

    // Returns true when this failure is the one that deactivates the source.
    public bool RecordFailure(DateTimeOffset now, string errorCode, TimeSpan interval, int deactivateAfter)
    {
        LastScanAt = now;
        NextScanAt = now + interval;
        LastErrorCode = errorCode;
        ConsecutiveFailures++;

        if (!IsActive || ConsecutiveFailures < deactivateAfter)
            return false;

        IsActive = false;
        return true;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        ConsecutiveFailures = 0;
        NextScanAt = now;
    }

    public void ScheduleNow(DateTimeOffset now) => NextScanAt = now;
}
