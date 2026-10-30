namespace JobHunter.Api.Common.Options;

internal sealed class WorkersOptions
{
    public const string Section = "Workers";

    public WorkerSchedule SourceScan { get; init; } = new();

    public WorkerSchedule Matching { get; init; } = new() { TickInterval = TimeSpan.FromMinutes(2), BatchSize = 200 };

    public WorkerSchedule ManualScan { get; init; } = new();
}
