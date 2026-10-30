using JobHunter.Api.Common.Options;
using JobHunter.Api.Features.Scanning;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Workers;

internal sealed class SourceScanWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkersOptions> options,
    TimeProvider clock,
    ILogger<SourceScanWorker> logger) : PeriodicCommandWorker(scopeFactory, options, clock, logger)
{
    protected override string Name => "Source scan";

    protected override WorkerSchedule Schedule(WorkersOptions workers) => workers.SourceScan;

    protected override object CreateCommand(WorkerSchedule schedule) => new ScanDueSources.Command(schedule.BatchSize);
}
