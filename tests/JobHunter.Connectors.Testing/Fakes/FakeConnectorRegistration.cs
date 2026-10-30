using JobHunter.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Connectors.Testing.Fakes;

public static class FakeConnectorRegistration
{
    // Each key registers an independent fake with its own host, so tests can simulate several platforms at once.
    public static IServiceCollection AddFakeConnector(this IServiceCollection services, string key = FakeConnector.Key)
    {
        services.AddKeyedSingleton(key, (provider, _) => new FakeConnector(provider.GetService<TimeProvider>() ?? TimeProvider.System, key));
        services.AddSingleton<IJobConnector>(provider => provider.GetRequiredKeyedService<FakeConnector>(key));

        if (key == FakeConnector.Key)
            services.AddSingleton(provider => provider.GetRequiredKeyedService<FakeConnector>(key));

        foreach (var serviceType in ConnectorCapabilityServices.All)
        {
            services.AddKeyedSingleton(serviceType, key, (provider, _) => provider.GetRequiredKeyedService<FakeConnector>(key));
        }

        return services;
    }
}
