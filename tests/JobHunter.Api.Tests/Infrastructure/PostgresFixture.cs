using Testcontainers.PostgreSql;

namespace JobHunter.Api.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    // Keep in sync with the image in docker-compose.yml.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
