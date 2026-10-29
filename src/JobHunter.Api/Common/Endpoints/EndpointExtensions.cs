using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobHunter.Api.Common.Endpoints;

internal static class EndpointExtensions
{
    public static IServiceCollection AddEndpointGroups(this IServiceCollection services)
    {
        var groups = typeof(EndpointExtensions).Assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpointGroup)))
            .Select(type => ServiceDescriptor.Singleton(typeof(IEndpointGroup), type));

        services.TryAddEnumerable(groups);
        return services;
    }

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        // Every feature endpoint hangs off this group, so none can be mapped without the API key check.
        var api = app.MapGroup("/api").AddEndpointFilter<ApiKeyEndpointFilter>();

        foreach (var group in app.ServiceProvider.GetServices<IEndpointGroup>())
        {
            group.MapEndpoints(api);
        }

        return app;
    }
}
