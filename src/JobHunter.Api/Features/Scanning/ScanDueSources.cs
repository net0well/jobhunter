using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Options;
using JobHunter.Api.Common.Persistence;
using JobHunter.Connectors.Abstractions.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Features.Scanning;

internal static class ScanDueSources
{
    internal sealed record Command(int BatchSize) : IRequest<Response>;

    internal sealed record Response(int Scanned);

    internal sealed class Handler(
        AppDbContext db,
        IConnectorRegistry registry,
        IServiceScopeFactory scopes,
        IOptions<ScanningOptions> scanningOptions,
        TimeProvider clock,
        ILogger<Handler> logger) : IRequestHandler<Command, Response>
    {
        private static readonly ConnectorErrorKind[] StopsTheConnector =
            [ConnectorErrorKind.LayoutChanged, ConnectorErrorKind.CaptchaDetected, ConnectorErrorKind.AuthenticationRequired, ConnectorErrorKind.RateLimited];

        public async ValueTask<Response> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = clock.GetUtcNow();
            List<string> enabled = [.. registry.Connectors.Select(descriptor => descriptor.Key)];
            var paused = await db.ConnectorStates.AsNoTracking()
                .Where(state => state.PausedUntil > now)
                .Select(state => state.ConnectorKey)
                .ToListAsync(cancellationToken);

            var due = await db.SourceTargets.AsNoTracking()
                .Where(source => source.IsActive && source.NextScanAt <= now)
                .Where(source => enabled.Contains(source.ConnectorKey) && !paused.Contains(source.ConnectorKey))
                .OrderBy(source => source.NextScanAt)
                .Take(command.BatchSize)
                .Select(source => new { source.Id, source.ConnectorKey })
                .ToListAsync(cancellationToken);

            var scanned = 0;
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = scanningOptions.Value.MaxParallelConnectors, CancellationToken = cancellationToken };

            // Connectors run side by side; sources of the same connector run one after another to respect its limits.
            await Parallel.ForEachAsync(due.GroupBy(source => source.ConnectorKey), parallel, async (connector, token) =>
            {
                foreach (var source in connector)
                {
                    var result = await ScanAsync(source.Id, connector.Key, token);
                    if (result is null)
                        continue;

                    Interlocked.Increment(ref scanned);
                    if (result.ErrorKind is { } kind && StopsTheConnector.Contains(kind))
                        break;
                }
            });

            return new Response(scanned);
        }

        private async Task<Shared.ScanRunResponse?> ScanAsync(Guid sourceId, string connectorKey, CancellationToken ct)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ScanSource.Command(sourceId), ct);
                return result.IsSuccess ? result.Value : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scan of {SourceId} on {Connector} crashed", sourceId, connectorKey);
                return null;
            }
        }
    }
}
