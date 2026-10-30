using JobHunter.Connectors.Abstractions;

namespace JobHunter.Connectors.InHire;

internal sealed class InHireConnector : IJobConnector
{
    public ConnectorDescriptor Descriptor { get; } = new(
        InHireConnectorRegistration.Key,
        "InHire",
        ConnectorCapabilities.TenantListing | ConnectorCapabilities.PostingDetails
            | ConnectorCapabilities.ApplicationInspect | ConnectorCapabilities.ApplicationSubmit,
        new ConnectorTraits(
            RequiresAuthentication: false,
            UsesBrowser: false,
            DefaultScanInterval: TimeSpan.FromHours(6),
            DefaultMaxConcurrency: 1,
            DefaultPolitenessDelayMin: TimeSpan.FromSeconds(4),
            DefaultPolitenessDelayMax: TimeSpan.FromSeconds(10)));
}
