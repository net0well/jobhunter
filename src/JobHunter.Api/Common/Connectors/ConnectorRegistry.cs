using System.Diagnostics.CodeAnalysis;
using JobHunter.Connectors.Abstractions;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ConnectorRegistry(IEnumerable<IJobConnector> connectors, IServiceProvider services) : IConnectorRegistry
{
    // Duplicated keys are reported by ConnectorRegistryValidator at startup; here the first registration wins.
    public IReadOnlyList<ConnectorDescriptor> Connectors { get; } =
        [.. connectors.Select(connector => connector.Descriptor).DistinctBy(descriptor => descriptor.Key)];

    public ConnectorDescriptor? Find(string key) =>
        Connectors.FirstOrDefault(descriptor => descriptor.Key == key);

    public bool TryGet<TCapability>(string key, [NotNullWhen(true)] out TCapability? capability)
        where TCapability : class
    {
        capability = null;

        var descriptor = Find(key);
        if (descriptor is null || !Declares(descriptor, typeof(TCapability)))
            return false;

        capability = services.GetKeyedService<TCapability>(key);
        return capability is not null;
    }

    private static bool Declares(ConnectorDescriptor descriptor, Type serviceType) =>
        ConnectorCapabilityServices.CapabilitiesBackedBy(serviceType).Any(capability => descriptor.Capabilities.HasFlag(capability));
}
