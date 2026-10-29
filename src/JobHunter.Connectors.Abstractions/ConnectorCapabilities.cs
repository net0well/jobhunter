namespace JobHunter.Connectors.Abstractions;

[Flags]
public enum ConnectorCapabilities
{
    None = 0,
    TenantListing = 1 << 0,
    Search = 1 << 1,
    Feed = 1 << 2,
    PostingDetails = 1 << 3,
    ApplicationInspect = 1 << 4,
    ApplicationSubmit = 1 << 5,
    AssistedApply = 1 << 6,
    TenantDiscovery = 1 << 7,
    HealthProbe = 1 << 8,
}
