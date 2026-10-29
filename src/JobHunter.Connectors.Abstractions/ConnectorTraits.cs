namespace JobHunter.Connectors.Abstractions;

public sealed record ConnectorTraits(
    bool RequiresAuthentication,
    bool UsesBrowser,
    TimeSpan DefaultScanInterval,
    int DefaultMaxConcurrency,
    TimeSpan DefaultPolitenessDelayMin,
    TimeSpan DefaultPolitenessDelayMax);
