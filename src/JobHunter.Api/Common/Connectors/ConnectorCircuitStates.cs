using System.Collections.Concurrent;
using Polly.CircuitBreaker;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ConnectorCircuitStates
{
    private readonly ConcurrentDictionary<string, CircuitBreakerStateProvider> _providers = new();

    public CircuitBreakerStateProvider ProviderFor(string connectorKey) =>
        _providers.GetOrAdd(connectorKey, _ => new CircuitBreakerStateProvider());

    // Pipelines are built on first use, so a connector that was never called reports a closed circuit.
    public CircuitState StateOf(string connectorKey) =>
        _providers.TryGetValue(connectorKey, out var provider) ? provider.CircuitState : CircuitState.Closed;
}
