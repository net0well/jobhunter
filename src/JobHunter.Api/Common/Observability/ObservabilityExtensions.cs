using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace JobHunter.Api.Common.Observability;

internal static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((provider, logger) => logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(provider)
            .Enrich.FromLogContext());

        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(JobHunterTelemetry.ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(JobHunterTelemetry.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddMeter(JobHunterTelemetry.ServiceName)
                .AddNpgsqlInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            telemetry.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(otlpEndpoint));

        return services;
    }
}
