using JobHunter.Connectors.Abstractions.Applications;

namespace JobHunter.Api.Common.Connectors;

internal sealed class ApplyChannelResolver(IConnectorRegistry registry, ILogger<ApplyChannelResolver> logger)
{
    public string? Resolve(Uri applyUrl)
    {
        List<string> claimants =
        [
            .. registry.Connectors
                .Where(descriptor => registry.TryGet<IApplicationChannel>(descriptor.Key, out var channel) && channel.CanHandle(applyUrl))
                .Select(descriptor => descriptor.Key),
        ];

        if (claimants.Count > 1)
            logger.LogWarning("Apply URLs on {Host} are claimed by {Connectors}; using {Connector}", applyUrl.Host, claimants, claimants[0]);

        return claimants.FirstOrDefault();
    }
}
