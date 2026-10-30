using JobHunter.Api.Common.Observability;
using JobHunter.Api.Common.Options;
using JobHunter.Connectors.Abstractions.Evidence;
using JobHunter.Connectors.InHire;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Connectors;

// The only place in the host that knows concrete connectors.
internal static class ConnectorRegistration
{
    public static IServiceCollection AddConnectors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInHireConnector(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<JobHunterMetrics>();

        services.AddSingleton<IConfigureOptions<ConnectorOptions>>(provider =>
            ActivatorUtilities.CreateInstance<ConfigureConnectorOptions>(provider, configuration));
        services.AddSingleton<IValidateOptions<ConnectorOptions>, ValidateConnectorOptions>();

        services.AddOptions<ArtifactsOptions>()
            .Bind(configuration.GetSection(ArtifactsOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton<IConnectorEvidenceStore, FileEvidenceStore>();

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
