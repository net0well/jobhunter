using JobHunter.Connectors.Abstractions;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

// Options are named by connector key. The connector's traits are the defaults and Connectors:<key> overrides them.
internal sealed class ConfigureConnectorOptions(IConfiguration configuration, IEnumerable<IJobConnector> connectors)
    : IConfigureNamedOptions<ConnectorOptions>
{
    public void Configure(string? name, ConnectorOptions options)
    {
        var traits = connectors.FirstOrDefault(connector => connector.Descriptor.Key == name)?.Descriptor.Traits;
        if (traits is not null)
        {
            options.ScanInterval = traits.DefaultScanInterval;
            options.MaxConcurrency = traits.DefaultMaxConcurrency;
            options.PolitenessDelayMin = traits.DefaultPolitenessDelayMin;
            options.PolitenessDelayMax = traits.DefaultPolitenessDelayMax;
        }

        configuration.GetSection($"{ConnectorOptions.Section}:{name}").Bind(options);
    }

    public void Configure(ConnectorOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
}
