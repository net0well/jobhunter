using System.ComponentModel.DataAnnotations;

namespace JobHunter.Api.Common.Options;

internal sealed class WorkerSchedule
{
    public bool Enabled { get; init; } = true;

    public TimeSpan TickInterval { get; init; } = TimeSpan.FromMinutes(5);

    [Range(1, 1000)]
    public int BatchSize { get; init; } = 20;
}
