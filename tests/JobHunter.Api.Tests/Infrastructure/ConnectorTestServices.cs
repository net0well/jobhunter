using JobHunter.Api.Common.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobHunter.Api.Tests.Infrastructure;

// Builds the connector infrastructure without the web host, for tests that exercise registration rules in isolation.
internal static class ConnectorTestServices
{
    public static ServiceProvider Build(Action<IServiceCollection> register, Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddMetrics();
        services.AddConnectors(configuration);
        register(services);

        return services.BuildServiceProvider();
    }

    public static IHostedService RegistryValidator(this IServiceProvider services) =>
        services.GetServices<IHostedService>().OfType<ConnectorRegistryValidator>().Single();
}
