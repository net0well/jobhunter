using System.Security.Cryptography;
using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Persistence;
using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire;
using JobHunter.Connectors.Testing.Fakes;
using JobHunter.Connectors.Testing.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace JobHunter.Api.Tests.Infrastructure;

public sealed class JobHunterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SecondFakeKey = "fake-b";

    // Declares only search, to exercise sources that need a capability the connector lacks.
    public const string SearchOnlyKey = "search-only";

    public static readonly string ApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static readonly string[] ConnectorKeys = [FakeConnector.Key, SecondFakeKey, SearchOnlyKey, InHireConnectorRegistration.Key];

    private readonly PostgresFixture _postgres = new();

    private readonly string _artifacts = Path.Combine(Path.GetTempPath(), "jobhunter-tests", Guid.NewGuid().ToString("N"));

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

    // The real InHire connector is enabled by appsettings; its HTTP calls are answered here so no test leaves the machine.
    public FixtureHttpHandler InHireHttp { get; } = new();

    public FakeConnector Fake => Services.GetRequiredKeyedService<FakeConnector>(FakeConnector.Key);

    public FakeConnector SecondFake => Services.GetRequiredKeyedService<FakeConnector>(SecondFakeKey);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public DbScope OpenDb() => new(Services.CreateAsyncScope());

    public HttpClient CreateApiClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyEndpointFilter.HeaderName, ApiKey);
        return client;
    }

    // Clears state shared by every test in the collection: database rows, scripted fake connectors and circuit breakers.
    public async Task ResetAsync()
    {
        Fake.Reset();
        SecondFake.Reset();
        InHireHttp.Clear();
        await Services.GetRequiredService<ConnectorCircuitStates>().CloseAllAsync(CancellationToken.None);

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
            "TRUNCATE connector_states, scan_runs, posting_sources, job_postings, source_targets, companies, search_profiles CASCADE");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseOptions.ConnectionStringName}", _postgres.ConnectionString);
        builder.UseSetting("Api:ApiKey", ApiKey);

        builder.UseSetting("Artifacts:RootPath", _artifacts);

        // Courtesy delays run on the fake clock and would never elapse; tests check spacing in the rate limiter tests.
        foreach (var key in ConnectorKeys)
        {
            builder.UseSetting($"Connectors:{key}:RequestsPerMinute", "600");
            builder.UseSetting($"Connectors:{key}:PolitenessDelayMin", "00:00:00");
            builder.UseSetting($"Connectors:{key}:PolitenessDelayMax", "00:00:00");
        }

        foreach (var worker in new[] { "SourceScan", "Matching", "ManualScan" })
        {
            builder.UseSetting($"Workers:{worker}:Enabled", "false");
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddFakeConnector();
            services.AddFakeConnector(SecondFakeKey);
            services.AddSingleton<IJobConnector>(new StubConnector(SearchOnlyKey, ConnectorCapabilities.Search));
            services.AddKeyedSingleton<ISearchJobSource>(SearchOnlyKey, (_, _) => new FakeConnector(TimeProvider.System, SearchOnlyKey));
            services.AddHttpClient(InHireConnectorRegistration.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => InHireHttp)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        InHireHttp.Dispose();

        if (Directory.Exists(_artifacts))
            Directory.Delete(_artifacts, recursive: true);
    }
}
