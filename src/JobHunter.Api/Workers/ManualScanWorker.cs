using JobHunter.Api.Common.Options;
using JobHunter.Api.Features.Scanning;
using JobHunter.Api.Features.Scanning.Shared;
using Mediator;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Workers;

internal sealed class ManualScanWorker(
    ScanRequestQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkersOptions> options,
    ILogger<ManualScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.ManualScan.Enabled)
        {
            logger.LogInformation("Manual scan worker is disabled");
            return;
        }

        await foreach (var request in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new ScanSource.Command(request.SourceTargetId, request.ScanRunId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual scan {ScanRunId} of {SourceId} failed", request.ScanRunId, request.SourceTargetId);
            }
        }
    }
}
