using JobHunter.Api.Common.Options;
using Mediator;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Workers;

// Runs one command per tick in a fresh scope; business rules stay in the handlers.
internal abstract class PeriodicCommandWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkersOptions> options,
    TimeProvider clock,
    ILogger logger) : BackgroundService
{
    protected abstract string Name { get; }

    protected abstract WorkerSchedule Schedule(WorkersOptions workers);

    protected abstract object CreateCommand(WorkerSchedule schedule);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = Schedule(options.CurrentValue);
        if (!schedule.Enabled)
        {
            logger.LogInformation("{Worker} worker is disabled", Name);
            return;
        }

        using var timer = new PeriodicTimer(schedule.TickInterval, clock);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(CreateCommand(Schedule(options.CurrentValue)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Worker} cycle failed", Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
