using System.Text.Json.Serialization;
using FluentValidation;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Errors;
using JobHunter.Api.Common.Options;
using Mediator;

namespace JobHunter.Api.Common.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.Section))
            .Validate(
                options => !options.RequireApiKey || options.ApiKey?.Length >= ApiOptions.MinimumApiKeyLength,
                $"Api:ApiKey must have at least {ApiOptions.MinimumApiKeyLength} characters when Api:RequireApiKey is true (set it with dotnet user-secrets).")
            .ValidateOnStart();

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.Section))
            .Validate(options => TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _), "App:TimeZone is not a known time zone id.")
            .ValidateOnStart();

        services.AddOptions<ScanningOptions>()
            .Bind(configuration.GetSection(ScanningOptions.Section))
            .ValidateDataAnnotations()
            .Validate(options => options.BlockedPauseDuration > TimeSpan.Zero, "Scanning:BlockedPauseDuration must be positive.")
            .ValidateOnStart();

        services.AddOptions<WorkersOptions>()
            .Bind(configuration.GetSection(WorkersOptions.Section))
            .Validate(
                options => new[] { options.SourceScan, options.Matching, options.ManualScan }.All(schedule => schedule.TickInterval > TimeSpan.Zero && schedule.BatchSize > 0),
                "Workers:*:TickInterval and BatchSize must be positive.")
            .ValidateOnStart();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddMediator((MediatorOptions options) =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.GenerateTypesAsInternal = true;
        });

        services.AddValidatorsFromAssemblyContaining<ApiOptions>(includeInternalTypes: true);
        services.AddEndpointGroups();
        services.AddOpenApi(options => options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>());

        return services;
    }
}
