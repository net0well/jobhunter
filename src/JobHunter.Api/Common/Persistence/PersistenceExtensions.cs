using JobHunter.Api.Common.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Persistence;

internal static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"ConnectionStrings:{DatabaseOptions.ConnectionStringName} is not configured (set it with dotnet user-secrets).")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(
                provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString,
                npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddNpgSql(
            provider => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString!,
            name: "postgres",
            tags: [HealthTags.Ready]);

        return services;
    }
}
