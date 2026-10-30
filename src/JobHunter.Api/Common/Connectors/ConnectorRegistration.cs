using JobHunter.Api.Common.Observability;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

// The only place in the host that knows concrete connectors.
internal static class ConnectorRegistration
{
    public static IServiceCollection AddConnectors(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<JobHunterMetrics>();

        services.AddSingleton<IConfigureOptions<ConnectorOptions>>(provider =>
            ActivatorUtilities.CreateInstance<ConfigureConnectorOptions>(provider, configuration));
        services.AddSingleton<IValidateOptions<ConnectorOptions>, ValidateConnectorOptions>();

        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.AddSingleton<ConnectorRateLimiter>();
        services.AddSingleton<ConnectorCircuitStates>();
        services.AddSingleton<ApplyChannelResolver>();
        services.AddScoped<ConnectorInvoker>();
        services.AddConnectorPipelines();
        services.AddHostedService<ConnectorRegistryValidator>();

        return services;
    }
}
