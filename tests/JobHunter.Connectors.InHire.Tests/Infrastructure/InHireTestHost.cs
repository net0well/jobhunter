using JobHunter.Connectors.Abstractions.Evidence;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Evidence;
using JobHunter.Connectors.Testing.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Connectors.InHire.Tests.Infrastructure;

// The real connector wired to fixtures: every request is answered locally or fails the test.
internal sealed class InHireTestHost : IDisposable
{
    public const string Tenant = "eyxo";
    public const string EmptyTenant = "vazia";
    public const string MissingTenant = "nao-existe";
    public const string OpenJobId = "5f8b856e-f4a1-486c-94c2-385c18e483e7";
    public const string ClosedJobId = "00000000-0000-4000-8000-000000000000";

    public InHireTestHost()
    {
        var services = new ServiceCollection();
        Register(services, Http, Evidence);
        Services = services.BuildServiceProvider();
        MapDefaults();
    }

    public FixtureHttpHandler Http { get; } = new();

    public TemporaryEvidenceStore Evidence { get; } = new();

    public ServiceProvider Services { get; }

    public ITenantJobSource TenantSource => Services.GetRequiredKeyedService<ITenantJobSource>(InHireConnectorRegistration.Key);

    public IJobDetailsProvider Details => Services.GetRequiredKeyedService<IJobDetailsProvider>(InHireConnectorRegistration.Key);

    public static Uri JobUrl(string jobId, string tenant = Tenant) => new($"https://{tenant}.inhire.app/vagas/{jobId}");

    public static string Api(string path) => $"https://api.inhire.app/{path}";

    public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static void Register(IServiceCollection services, FixtureHttpHandler http, IConnectorEvidenceStore evidence)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"Connectors:{InHireConnectorRegistration.Key}:Enabled"] = "true" })
            .Build();

        services.AddInHireConnector(configuration);
        services.AddSingleton(evidence);
        services.AddHttpClient(InHireConnectorRegistration.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => http)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        Services.Dispose();
        Evidence.Dispose();
        Http.Dispose();
    }

    private void MapDefaults()
    {
        Http.RespondWithFile(Api($"tenants/public/config/resolve/{Tenant}"), Fixture("tenant-eyxo.json"));
        Http.RespondWithFile(Api("job-posts/public/pages"), Fixture("listing-eyxo-20261030.json"), FixtureHttpHandler.WithHeader("X-Tenant", Tenant));
        Http.RespondWithFile(Api($"job-posts/public/pages/{OpenJobId}"), Fixture("job-eyxo-5f8b856e.json"), FixtureHttpHandler.WithHeader("X-Tenant", Tenant));
        Http.Respond(Api($"job-posts/public/pages/{ClosedJobId}"), System.Net.HttpStatusCode.NotFound, File.ReadAllText(Fixture("job-not-found.json")));

        Http.Respond(Api($"tenants/public/config/resolve/{EmptyTenant}"), System.Net.HttpStatusCode.OK, """{"tenant":{"id":"vazia","name":"Vazia S.A."},"subdomain":{"current":"vazia","primarySubdomain":"vazia"}}""");
        Http.Respond(Api("job-posts/public/pages"), System.Net.HttpStatusCode.OK, """{"tenantName":"Vazia S.A.","jobsPage":[]}""", when: FixtureHttpHandler.WithHeader("X-Tenant", EmptyTenant));

        Http.Respond(Api($"tenants/public/config/resolve/{MissingTenant}"), System.Net.HttpStatusCode.NotFound, File.ReadAllText(Fixture("tenant-not-found.json")));
    }
}
