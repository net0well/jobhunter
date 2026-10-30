using JobHunter.Api.Common.Options;
using JobHunter.Api.Features.Matching;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Workers;

internal sealed class MatchingWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkersOptions> options,
    TimeProvider clock,
    ILogger<MatchingWorker> logger) : PeriodicCommandWorker(scopeFactory, options, clock, logger)
{
    protected override string Name => "Matching";

    protected override WorkerSchedule Schedule(WorkersOptions workers) => workers.Matching;

    protected override object CreateCommand(WorkerSchedule schedule) => new ScorePendingPostings.Command(schedule.BatchSize);
}
