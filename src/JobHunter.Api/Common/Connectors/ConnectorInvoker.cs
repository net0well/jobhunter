using JobHunter.Api.Common.Observability;
using JobHunter.Api.Common.Persistence;
using JobHunter.Connectors.Abstractions.Results;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace JobHunter.Api.Common.Connectors;

// The only path from the core to a connector: pause check, rate limit, courtesy delay, resilience and metrics.
internal sealed class ConnectorInvoker(
    AppDbContext db,
    ConnectorRateLimiter rateLimiter,
    ResiliencePipelineProvider<string> pipelines,
    JobHunterMetrics metrics,
    TimeProvider clock,
    ILogger<ConnectorInvoker> logger)
{
    public const string PausedCode = "Connector.Paused";

    public const string LocalRateLimitCode = "Connector.RateLimited";

    public async Task<ConnectorResult<T>> InvokeAsync<T>(
        string connectorKey,
        Func<CancellationToken, Task<ConnectorResult<T>>> operation,
        CancellationToken ct,
        ConnectorOperationKind kind = ConnectorOperationKind.Read)
    {
        var pausedUntil = await db.ConnectorStates.AsNoTracking()
            .Where(state => state.ConnectorKey == connectorKey)
            .Select(state => state.PausedUntil)
            .FirstOrDefaultAsync(ct);

        var now = clock.GetUtcNow();
        if (pausedUntil > now)
            return Fail<T>(PausedCode, $"Connector {connectorKey} is paused until {pausedUntil:O}.", pausedUntil - now);

        using var lease = await rateLimiter.AcquireAsync(connectorKey, ct);
        if (!lease.IsAcquired)
            return Fail<T>(LocalRateLimitCode, "Local rate limit reached.");

        await rateLimiter.PolitenessDelayAsync(connectorKey, kind, ct);

        var pipeline = pipelines.GetPipeline<ConnectorCallOutcome>(ConnectorPipelines.KeyFor(connectorKey, kind));
        var started = clock.GetTimestamp();

        var result = await ExecuteAsync(connectorKey, pipeline, operation, ct);

        metrics.RecordConnectorCall(connectorKey, kind, result.Error?.Kind, clock.GetElapsedTime(started));
        return result;
    }

    private async Task<ConnectorResult<T>> ExecuteAsync<T>(
        string connectorKey,
        ResiliencePipeline<ConnectorCallOutcome> pipeline,
        Func<CancellationToken, Task<ConnectorResult<T>>> operation,
        CancellationToken ct)
    {
        try
        {
            var outcome = await pipeline.ExecuteAsync(
                static async (call, token) =>
                {
                    var result = await call(token);
                    return new ConnectorCallOutcome(result.Error, result);
                },
                operation,
                ct);

            return (ConnectorResult<T>)outcome.BoxedResult;
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning("Circuit open for connector {Connector}", connectorKey);
            return ConnectorResult<T>.Fail(new(ConnectorErrorKind.Transient, "Connector.CircuitOpen", $"Connector {connectorKey} is failing; calls are suspended.", RetryAfter: ex.RetryAfter));
        }
        catch (TimeoutRejectedException)
        {
            logger.LogWarning("Connector {Connector} timed out", connectorKey);
            return ConnectorResult<T>.Fail(new(ConnectorErrorKind.Transient, "Connector.Timeout", $"Connector {connectorKey} did not answer in time."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Unhandled exception from connector {Connector}", connectorKey);
            return ConnectorResult<T>.Fail(new(ConnectorErrorKind.Transient, "Connector.Unhandled", $"Connector {connectorKey} failed unexpectedly."));
        }
    }

    private static ConnectorResult<T> Fail<T>(string code, string message, TimeSpan? retryAfter = null) =>
        ConnectorResult<T>.Fail(new(ConnectorErrorKind.RateLimited, code, message, RetryAfter: retryAfter));
}
