using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Health;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Connectors.Abstractions;

public static class ConnectorCapabilityServices
{
    // Inspect and submit share IApplicationChannel, so one service type may back several capabilities.
    private static readonly Dictionary<ConnectorCapabilities, Type> ServiceTypes = new()
    {
        [ConnectorCapabilities.TenantListing] = typeof(ITenantJobSource),
        [ConnectorCapabilities.Search] = typeof(ISearchJobSource),
        [ConnectorCapabilities.Feed] = typeof(IFeedJobSource),
        [ConnectorCapabilities.PostingDetails] = typeof(IJobDetailsProvider),
        [ConnectorCapabilities.ApplicationInspect] = typeof(IApplicationChannel),
        [ConnectorCapabilities.ApplicationSubmit] = typeof(IApplicationChannel),
        [ConnectorCapabilities.AssistedApply] = typeof(IAssistedApplicationChannel),
        [ConnectorCapabilities.TenantDiscovery] = typeof(ITenantDiscovery),
        [ConnectorCapabilities.HealthProbe] = typeof(IConnectorHealthProbe),
    };

    public static IReadOnlyCollection<Type> All { get; } = [.. ServiceTypes.Values.Distinct()];

    public static Type ServiceTypeFor(ConnectorCapabilities capability) =>
        ServiceTypes.TryGetValue(capability, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(capability), capability, "Expected a single capability flag.");

    public static IEnumerable<ConnectorCapabilities> CapabilitiesBackedBy(Type serviceType) =>
        ServiceTypes.Where(pair => pair.Value == serviceType).Select(pair => pair.Key);

    public static IEnumerable<ConnectorCapabilities> Flags(ConnectorCapabilities capabilities) =>
        ServiceTypes.Keys.Where(flag => capabilities.HasFlag(flag));
}
