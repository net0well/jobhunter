using System.Globalization;
using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Extensions;
using JobHunter.Api.Common.Health;
using JobHunter.Api.Common.Observability;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Features;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddObservability(builder.Configuration);
    builder.Services.AddApiInfrastructure(builder.Configuration);
    builder.Services.AddPersistence();
    builder.Services.AddConnectors(builder.Configuration);
    builder.Services.AddFeatures();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    app.MapHealthEndpoints();
    app.MapApiDocumentation();
    app.MapEndpoints();

    await app.RunAsync();
}
// The EF Core tooling aborts the host on purpose after building it.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
