using JobHunter.Connectors.Abstractions;

namespace JobHunter.Api.Tests.Infrastructure;

internal sealed class StubConnector(string key, ConnectorCapabilities capabilities) : IJobConnector
{
    public ConnectorDescriptor Descriptor { get; } = new(
        key,
        $"Stub {key}",
        capabilities,
        new ConnectorTraits(false, false, TimeSpan.FromHours(1), 1, TimeSpan.Zero, TimeSpan.Zero));
}
