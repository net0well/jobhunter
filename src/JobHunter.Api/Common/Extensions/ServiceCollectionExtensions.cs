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
