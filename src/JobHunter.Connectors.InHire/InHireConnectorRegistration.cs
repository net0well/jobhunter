using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Api;
using JobHunter.Connectors.InHire.Applications;
using JobHunter.Connectors.InHire.Details;
using JobHunter.Connectors.InHire.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace JobHunter.Connectors.InHire;

public static class InHireConnectorRegistration
{
    public const string Key = "inhire";

    public const string HttpClientName = "inhire";

    public static IServiceCollection AddInHireConnector(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection($"Connectors:{Key}");
        if (!section.GetValue<bool>("Enabled"))
            return services;

        services.AddOptions<InHireOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();

        services.AddHttpClient(HttpClientName, (provider, http) =>
        {
            var options = provider.GetRequiredService<IOptions<InHireOptions>>().Value;
            http.BaseAddress = options.ApiBaseUrl;
            http.Timeout = options.RequestTimeout;
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<InHireApiClient>();
        services.AddSingleton<InHireConnector>();
        services.AddSingleton<IJobConnector>(provider => provider.GetRequiredService<InHireConnector>());
        services.AddKeyedSingleton<ITenantJobSource, InHireTenantSource>(Key);
        services.AddKeyedSingleton<IJobDetailsProvider, InHireDetailsProvider>(Key);
        services.AddKeyedSingleton<IApplicationChannel, InHireApplicationChannel>(Key);

        return services;
    }
}
