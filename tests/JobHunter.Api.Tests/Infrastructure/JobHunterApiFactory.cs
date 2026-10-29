using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Infrastructure;

public sealed class JobHunterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ApiKey = "integration-tests-api-key-0123456789abcdef";

    private readonly PostgresFixture _postgres = new();

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DatabaseOptions.ConnectionStringName}", _postgres.ConnectionString);
        builder.UseSetting("Api:ApiKey", ApiKey);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
