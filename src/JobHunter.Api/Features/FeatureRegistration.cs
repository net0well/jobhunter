using JobHunter.Api.Common.Notifications;
using JobHunter.Api.Features.Sources.Shared;

namespace JobHunter.Api.Features;

internal static class FeatureRegistration
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddSingleton<INotifier, LogNotifier>();
        services.AddScoped<SourceFactory>();

        return services;
    }
}
