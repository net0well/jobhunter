using JobHunter.Api.Common.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Infrastructure;

// A DbContext with its own DI scope, so tests can arrange and assert without leaking scopes.
public sealed class DbScope(AsyncServiceScope scope) : IAsyncDisposable
{
    internal AppDbContext Db { get; } = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
