using System.ComponentModel.DataAnnotations;

namespace JobHunter.Api.Common.Options;

internal sealed class ScanningOptions
{
    public const string Section = "Scanning";

    [Range(1, 10)]
    public int MaxParallelConnectors { get; init; } = 2;

    [Range(1, 50)]
    public int DeactivateAfterFailures { get; init; } = 5;

    [Range(1, 3650)]
    public int RunRetentionDays { get; init; } = 90;

    public TimeSpan BlockedPauseDuration { get; init; } = TimeSpan.FromHours(6);
}
