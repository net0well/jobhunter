using System.Collections.Concurrent;
using Polly.CircuitBreaker;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ConnectorCircuitStates
{
    private readonly ConcurrentDictionary<string, CircuitBreakerStateProvider> _providers = new();
    private readonly ConcurrentDictionary<string, CircuitBreakerManualControl> _controls = new();

    public CircuitBreakerStateProvider ProviderFor(string connectorKey) =>
        _providers.GetOrAdd(connectorKey, _ => new CircuitBreakerStateProvider());

    public CircuitBreakerManualControl ControlFor(string connectorKey) =>
        _controls.GetOrAdd(connectorKey, _ => new CircuitBreakerManualControl());

    // Pipelines are built on first use, so a connector that was never called reports a closed circuit.
    public CircuitState StateOf(string connectorKey) =>
        _providers.TryGetValue(connectorKey, out var provider) ? provider.CircuitState : CircuitState.Closed;

    // Lets a connector be retried right away, e.g. after someone fixed what made it fail.
    public async Task CloseAllAsync(CancellationToken ct)
    {
        foreach (var control in _controls.Values)
        {
            await control.CloseAsync(ct);
        }
    }
}
