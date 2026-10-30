using System.Security.Cryptography;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Persistence;
using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Fakes;
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

    private static readonly string[] ConnectorKeys = [FakeConnector.Key, SecondFakeKey, SearchOnlyKey];

    private readonly PostgresFixture _postgres = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

    public FakeConnector Fake => Services.GetRequiredKeyedService<FakeConnector>(FakeConnector.Key);

    public FakeConnector SecondFake => Services.GetRequiredKeyedService<FakeConnector>(SecondFakeKey);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyEndpointFilter.HeaderName, ApiKey);
        return client;
    }

    // Clears state shared by every test in the collection: database rows and the scripted fake connectors.
    public async Task ResetAsync()
    {
        Fake.Reset();
        SecondFake.Reset();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(
            "TRUNCATE connector_states, scan_runs, posting_sources, job_postings, source_targets, companies, search_profiles CASCADE");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseOptions.ConnectionStringName}", _postgres.ConnectionString);
        builder.UseSetting("Api:ApiKey", ApiKey);

        foreach (var key in ConnectorKeys)
        {
            builder.UseSetting($"Connectors:{key}:RequestsPerMinute", "600");
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
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
