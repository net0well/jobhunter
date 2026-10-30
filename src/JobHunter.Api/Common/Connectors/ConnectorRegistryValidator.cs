using System.Text.RegularExpressions;
using JobHunter.Connectors.Abstractions;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

internal sealed partial class ConnectorRegistryValidator(
    IEnumerable<IJobConnector> connectors,
    IServiceProvider services,
    IOptionsMonitor<ConnectorOptions> options,
    ILogger<ConnectorRegistryValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var problems = FindProblems();
        if (problems.Count > 0)
            throw new InvalidOperationException($"Connector registration is invalid:{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", problems)}");

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Connectors enabled: {Connectors}", connectors.Select(connector => connector.Descriptor.Key).ToList());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private List<string> FindProblems()
    {
        var descriptors = connectors.Select(connector => connector.Descriptor).ToList();
        var keyed = (IKeyedServiceProvider)services;

        List<string> problems =
        [
            .. descriptors.GroupBy(descriptor => descriptor.Key).Where(group => group.Count() > 1)
                .Select(group => $"Connector key '{group.Key}' is registered {group.Count()} times."),
            .. descriptors.Where(descriptor => !ValidKey().IsMatch(descriptor.Key))
                .Select(descriptor => $"Connector key '{descriptor.Key}' must match [a-z0-9-]+."),
        ];

        foreach (var descriptor in descriptors.DistinctBy(descriptor => descriptor.Key))
        {
            problems.AddRange(ConnectorCapabilityServices.Flags(descriptor.Capabilities)
                .Select(capability => (capability, serviceType: ConnectorCapabilityServices.ServiceTypeFor(capability)))
                .Where(pair => keyed.GetKeyedService(pair.serviceType, descriptor.Key) is null)
                .Select(pair => $"Connector '{descriptor.Key}' declares {pair.capability} but registers no {pair.serviceType.Name} with its key."));

            problems.AddRange(ConnectorCapabilityServices.All
                .Where(serviceType => keyed.GetKeyedService(serviceType, descriptor.Key) is not null)
                .Where(serviceType => !ConnectorCapabilityServices.CapabilitiesBackedBy(serviceType).Any(capability => descriptor.Capabilities.HasFlag(capability)))
                .Select(serviceType => $"Connector '{descriptor.Key}' registers {serviceType.Name} without declaring the matching capability."));

            try
            {
                options.Get(descriptor.Key);
            }
            catch (OptionsValidationException ex)
            {
                problems.AddRange(ex.Failures);
            }
        }

        return problems;
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex ValidKey();
}
