using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace JobHunter.Api.Common.Health;

internal static class HealthEndpointExtensions
{
    public const string LivenessPath = "/health";

    public const string ReadinessPath = "/health/ready";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthTags.Ready) });

        return app;
    }
}
