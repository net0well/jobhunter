using System.Security.Cryptography;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Persistence;
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
    public static readonly string ApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private readonly PostgresFixture _postgres = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

    public FakeConnector Fake => Services.GetRequiredService<FakeConnector>();

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

    // Clears state shared by every test in the collection: database rows and the scripted fake connector.
    public async Task ResetAsync()
    {
        Fake.Reset();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().ConnectorStates.ExecuteDeleteAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseOptions.ConnectionStringName}", _postgres.ConnectionString);
        builder.UseSetting("Api:ApiKey", ApiKey);
        builder.UseSetting($"Connectors:{FakeConnector.Key}:RequestsPerMinute", "600");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddFakeConnector();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
