using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain;
using JobHunter.Api.Features.Scanning.Shared;
using JobHunter.Connectors.Abstractions.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Sources;

internal static class TriggerSourceScan
{
    internal sealed record Command(Guid SourceTargetId) : IRequest<Result<Response>>;

    internal sealed record Response(Guid ScanRunId);

    internal sealed class Handler(AppDbContext db, IConnectorRegistry registry, ScanRequestQueue queue, TimeProvider clock)
        : IRequestHandler<Command, Result<Response>>
    {
        public async ValueTask<Result<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var source = await db.SourceTargets.FindAsync([command.SourceTargetId], cancellationToken);
            if (source is null)
                return Error.NotFound("Source.NotFound", "Source not found.");

            if (registry.Find(source.ConnectorKey) is null)
                return Error.Unavailable("Connector.Disabled", $"Connector {source.ConnectorKey} is not enabled.");

            var now = clock.GetUtcNow();
            var pausedUntil = await db.ConnectorStates.AsNoTracking()
                .Where(state => state.ConnectorKey == source.ConnectorKey)
                .Select(state => state.PausedUntil)
                .FirstOrDefaultAsync(cancellationToken);

            if (pausedUntil > now)
                return Error.Unavailable(ConnectorInvoker.PausedCode, $"Connector {source.ConnectorKey} is paused until {pausedUntil:O}.");

            var run = ScanRun.Start(source, now);
            db.ScanRuns.Add(run);

            if (!queue.TryEnqueue(new ScanRequest(source.Id, run.Id)))
            {
                run.Fail(new ConnectorError(ConnectorErrorKind.RateLimited, "Scan.QueueFull", "Too many scans waiting; try again later."), now);
                await db.SaveChangesAsync(cancellationToken);
                return Error.Unavailable("Scan.QueueFull", "Too many scans waiting; try again later.");
            }

            await db.SaveChangesAsync(cancellationToken);
            return new Response(run.Id);
        }
    }
}
