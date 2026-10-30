using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ConnectorRateLimiter(IOptionsMonitor<ConnectorOptions> options, TimeProvider clock) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<RateLimiter>> _limiters = new();
    private readonly ConcurrentDictionary<(string Connector, ConnectorOperationKind Kind), DateTimeOffset> _lastCalls = new();

    public ValueTask<RateLimitLease> AcquireAsync(string connectorKey, CancellationToken ct) =>
        _limiters.GetOrAdd(connectorKey, key => new Lazy<RateLimiter>(() => Create(key))).Value.AcquireAsync(1, ct);

    // Courtesy spacing applies between consecutive calls of the same kind, so the first call never waits.
    public Task PolitenessDelayAsync(string connectorKey, ConnectorOperationKind kind, CancellationToken ct)
    {
        var settings = options.Get(connectorKey);
        var (min, max) = kind == ConnectorOperationKind.Submit
            ? (settings.SubmissionDelayMin, settings.SubmissionDelayMax)
            : (settings.PolitenessDelayMin, settings.PolitenessDelayMax);

        var now = clock.GetUtcNow();
        var spacing = min + ((max - min) * Random.Shared.NextDouble());
        var wait = _lastCalls.TryGetValue((connectorKey, kind), out var lastCall) ? lastCall + spacing - now : TimeSpan.Zero;

        if (wait <= TimeSpan.Zero)
        {
            _lastCalls[(connectorKey, kind)] = now;
            return Task.CompletedTask;
        }

        _lastCalls[(connectorKey, kind)] = now + wait;
        return Task.Delay(wait, clock, ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var limiter in _limiters.Values.Where(lazy => lazy.IsValueCreated))
        {
            await limiter.Value.DisposeAsync();
        }
    }

    private RateLimiter Create(string connectorKey)
    {
        var settings = options.Get(connectorKey);

        // Callers queue for a free slot instead of failing; the token bucket caps the request rate per minute.
        var concurrency = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = settings.MaxConcurrency,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

        var rate = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = settings.RequestsPerMinute,
            TokensPerPeriod = settings.RequestsPerMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = settings.RequestsPerMinute,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });

        return RateLimiter.CreateChained(concurrency, rate);
    }
}
