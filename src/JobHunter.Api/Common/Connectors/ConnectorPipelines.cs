using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Results;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace JobHunter.Api.Common.Connectors;

internal static class ConnectorPipelines
{
    private const int MaxReadRetries = 2;

    // Longer waits requested by the platform are left to the next scan cycle instead of blocking the caller.
    private static readonly TimeSpan MaxInlineRetryAfter = TimeSpan.FromSeconds(30);

    public static string KeyFor(string connectorKey, ConnectorOperationKind kind) =>
        $"connector:{connectorKey}:{kind.ToString().ToLowerInvariant()}";

    public static IServiceCollection AddConnectorPipelines(this IServiceCollection services)
    {
        services.AddResiliencePipelines<string>(context =>
        {
            foreach (var key in context.ServiceProvider.GetServices<IJobConnector>().Select(connector => connector.Descriptor.Key).Distinct())
            {
                context.AddResiliencePipeline<ConnectorCallOutcome>(KeyFor(key, ConnectorOperationKind.Read), (builder, pipeline) =>
                    ConfigureRead(builder, key, pipeline.ServiceProvider));

                context.AddResiliencePipeline<ConnectorCallOutcome>(KeyFor(key, ConnectorOperationKind.Submit), (builder, pipeline) =>
                    ConfigureSubmit(builder, Options(pipeline.ServiceProvider, key)));
            }
        });

        return services;
    }

    private static void ConfigureRead(ResiliencePipelineBuilder<ConnectorCallOutcome> builder, string connectorKey, IServiceProvider services)
    {
        var options = Options(services, connectorKey);

        builder
            .AddTimeout(options.ReadTimeout * (MaxReadRetries + 2))
            .AddRetry(new RetryStrategyOptions<ConnectorCallOutcome>
            {
                MaxRetryAttempts = MaxReadRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder<ConnectorCallOutcome>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(outcome => IsRetryable(outcome.Error)),
                DelayGenerator = args => ValueTask.FromResult(args.Outcome.Result.Error?.RetryAfter),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ConnectorCallOutcome>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromMinutes(10),
                BreakDuration = TimeSpan.FromMinutes(15),
                StateProvider = services.GetRequiredService<ConnectorCircuitStates>().ProviderFor(connectorKey),
                ManualControl = services.GetRequiredService<ConnectorCircuitStates>().ControlFor(connectorKey),
                ShouldHandle = new PredicateBuilder<ConnectorCallOutcome>()
                    .Handle<Exception>(exception => exception is not OperationCanceledException)
                    .HandleResult(outcome => outcome.Error?.Kind is ConnectorErrorKind.Transient or ConnectorErrorKind.LayoutChanged),
            })
            .AddTimeout(options.ReadTimeout);
    }

    // Retrying a submission could apply twice, so the submit pipeline only bounds its duration.
    private static void ConfigureSubmit(ResiliencePipelineBuilder<ConnectorCallOutcome> builder, ConnectorOptions options) =>
        builder.AddTimeout(options.SubmitTimeout);

    private static bool IsRetryable(ConnectorError? error) => error switch
    {
        { Kind: ConnectorErrorKind.Transient } => true,
        { Kind: ConnectorErrorKind.RateLimited, RetryAfter: { } retryAfter } => retryAfter <= MaxInlineRetryAfter,
        _ => false,
    };

    private static ConnectorOptions Options(IServiceProvider services, string connectorKey) =>
        services.GetRequiredService<IOptionsMonitor<ConnectorOptions>>().Get(connectorKey);
}
