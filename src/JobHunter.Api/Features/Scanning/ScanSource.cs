using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Extensions;
using JobHunter.Api.Common.Notifications;
using JobHunter.Api.Common.Options;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Postings.Shared;
using JobHunter.Api.Features.Scanning.Shared;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Features.Scanning;

internal static class ScanSource
{
    internal sealed record Command(Guid SourceTargetId, Guid? ScanRunId = null) : IRequest<Result<ScanRunResponse>>;

    private sealed record Listing(IReadOnlyList<SourcedPosting> Items, string? Checkpoint);

    internal sealed class Handler(
        AppDbContext db,
        IConnectorRegistry registry,
        ConnectorInvoker invoker,
        PostingUpserter upserter,
        IOptionsMonitor<ConnectorOptions> connectorOptions,
        IOptions<ScanningOptions> scanningOptions,
        IOptions<AppOptions> appOptions,
        INotifier notifier,
        TimeProvider clock,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<ScanRunResponse>>
    {
        public async ValueTask<Result<ScanRunResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var source = await db.SourceTargets.FindAsync([command.SourceTargetId], cancellationToken);
            if (source is null)
                return Error.NotFound("Source.NotFound", "Source not found.");

            if (registry.Find(source.ConnectorKey) is null)
                return Error.Unavailable("Connector.Disabled", $"Connector {source.ConnectorKey} is not enabled.");

            var run = await StartRunAsync(source, command.ScanRunId, cancellationToken);
            var listing = await ListAsync(source, cancellationToken);

            if (listing.IsSuccess)
                await CompleteAsync(source, run, listing.Value!, cancellationToken);
            else
                await FailAsync(source, run, listing.Error, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            return ScanRunResponse.From(run);
        }

        private async Task<ScanRun> StartRunAsync(SourceTarget source, Guid? scanRunId, CancellationToken ct)
        {
            var run = scanRunId is { } id ? await db.ScanRuns.FindAsync([id], ct) : null;
            if (run is not null)
                return run;

            // Saved right away so a manual trigger or the next worker tick sees this source is already being scanned.
            run = ScanRun.Start(source, clock.GetUtcNow());
            db.ScanRuns.Add(run);
            await db.SaveChangesAsync(ct);
            return run;
        }

        private async Task<ConnectorResult<Listing>> ListAsync(SourceTarget source, CancellationToken ct)
        {
            var key = source.ConnectorKey;

            switch (source.Kind)
            {
                case SourceKind.Tenant when registry.TryGet<ITenantJobSource>(key, out var tenants):
                    var tenantItems = await invoker.InvokeAsync(key, token => tenants.ListAsync(new TenantRef(source.TenantRef!), token), ct);
                    return Map(tenantItems, items => new Listing(items, null));

                case SourceKind.Search when registry.TryGet<ISearchJobSource>(key, out var search):
                    var page = await invoker.InvokeAsync(key, token => search.SearchAsync(source.SearchCriteria!, source.Checkpoint, token), ct);
                    return Map(page, result => new Listing(result.Items, result.NextCursor));

                case SourceKind.Feed when registry.TryGet<IFeedJobSource>(key, out var feed):
                    var batch = await invoker.InvokeAsync(key, token => feed.PullAsync(source.Checkpoint, token), ct);
                    return Map(batch, result => new Listing(result.Items, result.NextCheckpoint ?? source.Checkpoint));

                default:
                    return ConnectorResult<Listing>.Fail(new(ConnectorErrorKind.Unsupported, "Connector.CapabilityMissing", $"Connector {key} cannot handle {source.Kind} sources."));
            }
        }

        private async Task CompleteAsync(SourceTarget source, ScanRun run, Listing listing, CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            var outcome = await upserter.UpsertAsync(source.ConnectorKey, source.Id, source.DisplayName, listing.Items, ct);
            var (closedByDetails, detailsFailure) = await FetchMissingDetailsAsync(source.ConnectorKey, outcome.CreatedPostings, ct);

            await db.SaveChangesAsync(ct);

            var closedAsMissing = source.Kind == SourceKind.Tenant ? await CloseMissingAsync(source, outcome.SeenSourceIds, ct) : 0;

            if (source.Kind != SourceKind.Tenant)
                source.UpdateCheckpoint(listing.Checkpoint);

            source.RecordSuccess(now, ScanInterval(source));
            (await StateAsync(source.ConnectorKey, ct)).RecordSuccess(now);

            if (detailsFailure is not null)
                await PauseIfBlockedAsync(source.ConnectorKey, detailsFailure, ct);

            var counts = new ScanCounts(listing.Items.Count, outcome.Created, outcome.Updated, outcome.Deduplicated, closedByDetails + closedAsMissing);
            run.Complete(counts, detailsFailure, now);

            logger.LogInformation(
                "Scan of {SourceId} on {Connector} found {Found} postings: {Created} new, {Deduplicated} merged, {Closed} closed",
                source.Id, source.ConnectorKey, counts.Found, counts.Created, counts.Deduplicated, counts.Closed);
        }

        // Listings often carry only a summary; scoring needs the description, so new postings are completed right away.
        private async Task<(int Closed, ConnectorError? Failure)> FetchMissingDetailsAsync(string connectorKey, IReadOnlyList<CreatedPosting> created, CancellationToken ct)
        {
            if (!registry.TryGet<IJobDetailsProvider>(connectorKey, out var details))
                return (0, null);

            var closed = 0;

            foreach (var (posting, sourceUrl) in created.Where(item => !item.Posting.HasDescription && details.CanHandle(item.SourceUrl)))
            {
                var result = await invoker.InvokeAsync(connectorKey, token => details.GetDetailsAsync(sourceUrl, token), ct);

                if (result.IsSuccess)
                {
                    if (posting.UpdateFrom(result.Value!, clock.GetUtcNow()))
                        posting.AssignChannel(upserter.ResolveChannel(posting));

                    closed += posting.Status == JobPostingStatus.Closed ? 1 : 0;
                }
                else if (result.Error.Kind == ConnectorErrorKind.PostingClosed)
                {
                    closed += posting.Close(clock.GetUtcNow()) ? 1 : 0;
                }
                else
                {
                    return (closed, result.Error);
                }
            }

            return (closed, null);
        }

        // A posting closes only when every source that listed it stopped doing so for long enough.
        private async Task<int> CloseMissingAsync(SourceTarget source, IReadOnlySet<Guid> seenSourceIds, CancellationToken ct)
        {
            List<Guid> seen = [.. seenSourceIds];

            await db.PostingSources
                .Where(posting => posting.SourceTargetId == source.Id && !seen.Contains(posting.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(posting => posting.MissedScans, posting => posting.MissedScans + 1), ct);

            var threshold = connectorOptions.Get(source.ConnectorKey).ClosedAfterMissedScans;
            var gone = await db.JobPostings
                .Where(posting => posting.Status != JobPostingStatus.Closed)
                .Where(posting => posting.Sources.Any(item => item.SourceTargetId == source.Id))
                .Where(posting => posting.Sources.All(item => item.MissedScans >= threshold))
                .ToListAsync(ct);

            var now = clock.GetUtcNow();
            return gone.Count(posting => posting.Close(now));
        }

        private async Task FailAsync(SourceTarget source, ScanRun run, ConnectorError error, CancellationToken ct)
        {
            var now = clock.GetUtcNow();
            run.Fail(error, now);

            if (ConnectorPausePolicy.IsLocal(error))
                return;

            logger.LogWarning("Scan of {SourceId} on {Connector} failed with {ErrorKind} {ErrorCode}", source.Id, source.ConnectorKey, error.Kind, error.Code);

            (await StateAsync(source.ConnectorKey, ct)).RecordFailure(now);
            await PauseIfBlockedAsync(source.ConnectorKey, error, ct);

            if (source.RecordFailure(now, error.Code, ScanInterval(source), scanningOptions.Value.DeactivateAfterFailures))
                await notifier.NotifyAsync(new Notification("SourceDeactivated", $"Source {source.DisplayName} on {source.ConnectorKey} was deactivated after {source.ConsecutiveFailures} failed scans ({error.Code})."), ct);
        }

        private async Task PauseIfBlockedAsync(string connectorKey, ConnectorError error, CancellationToken ct)
        {
            if (ConnectorPausePolicy.PauseFor(error, scanningOptions.Value.BlockedPauseDuration) is not { } pause)
                return;

            var now = clock.GetUtcNow();
            var paused = (await StateAsync(connectorKey, ct)).Pause(now + pause, $"{error.Kind}: {error.Message}", now);

            if (paused.IsSuccess)
                await notifier.NotifyAsync(new Notification("ConnectorPaused", $"Connector {connectorKey} paused until {now + pause:O} after {error.Kind}."), ct);
        }

        private Task<ConnectorState> StateAsync(string connectorKey, CancellationToken ct) =>
            db.GetOrCreateConnectorStateAsync(connectorKey, clock.GetLocalToday(appOptions.Value.Zone), ct);

        private TimeSpan ScanInterval(SourceTarget source) =>
            source.ScanInterval ?? connectorOptions.Get(source.ConnectorKey).ScanInterval ?? TimeSpan.FromHours(6);

        private static ConnectorResult<Listing> Map<T>(ConnectorResult<T> result, Func<T, Listing> map) =>
            result.IsSuccess ? ConnectorResult<Listing>.Ok(map(result.Value!)) : ConnectorResult<Listing>.Fail(result.Error);
    }
}
