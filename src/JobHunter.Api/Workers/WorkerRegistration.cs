namespace JobHunter.Api.Workers;

internal static class WorkerRegistration
{
    public static IServiceCollection AddWorkers(this IServiceCollection services)
    {
        services.AddHostedService<SourceScanWorker>();
        services.AddHostedService<MatchingWorker>();
        services.AddHostedService<ManualScanWorker>();

        return services;
    }
}
