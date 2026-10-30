using JobHunter.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Connectors.Testing.Fakes;

public static class FakeConnectorRegistration
{
    public static IServiceCollection AddFakeConnector(this IServiceCollection services)
    {
        services.AddSingleton(provider => new FakeConnector(provider.GetService<TimeProvider>() ?? TimeProvider.System));
        services.AddSingleton<IJobConnector>(provider => provider.GetRequiredService<FakeConnector>());

        foreach (var serviceType in ConnectorCapabilityServices.All)
        {
            services.AddKeyedSingleton(serviceType, FakeConnector.Key, (provider, _) => provider.GetRequiredService<FakeConnector>());
        }

        return services;
    }
}
