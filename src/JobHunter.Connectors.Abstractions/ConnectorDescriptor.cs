namespace JobHunter.Connectors.Abstractions;

public sealed record ConnectorDescriptor(
    string Key,
    string DisplayName,
    ConnectorCapabilities Capabilities,
    ConnectorTraits Traits);
