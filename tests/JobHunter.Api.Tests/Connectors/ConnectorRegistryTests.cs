using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Connectors;

public sealed class ConnectorRegistryTests
{
    [Fact]
    public async Task TryGet_DeclaredCapability_ReturnsImplementation()
    {
        await using var services = ConnectorTestServices.Build(registry => registry.AddFakeConnector());
        var registry = services.GetRequiredService<IConnectorRegistry>();

        var found = registry.TryGet<ITenantJobSource>(FakeConnector.Key, out var source);

        Assert.True(found);
        Assert.Same(services.GetRequiredService<FakeConnector>(), source);
    }

    [Fact]
    public async Task TryGet_UnknownConnector_ReturnsFalse()
    {
        await using var services = ConnectorTestServices.Build(registry => registry.AddFakeConnector());

        var found = services.GetRequiredService<IConnectorRegistry>().TryGet<ISearchJobSource>("inexistente", out var source);

        Assert.False(found);
        Assert.Null(source);
    }

    [Fact]
    public async Task TryGet_ServiceRegisteredButCapabilityNotDeclared_ReturnsFalse()
    {
        await using var services = ConnectorTestServices.Build(registry =>
        {
            registry.AddSingleton<IJobConnector>(new StubConnector("search-only", ConnectorCapabilities.Search));
            registry.AddKeyedSingleton<ITenantJobSource>("search-only", (_, _) => new FakeConnector(TimeProvider.System));
        });

        var found = services.GetRequiredService<IConnectorRegistry>().TryGet<ITenantJobSource>("search-only", out _);

        Assert.False(found);
    }

    [Fact]
    public async Task Connectors_DuplicatedKeys_KeepsFirstRegistration()
    {
        await using var services = ConnectorTestServices.Build(registry =>
        {
            registry.AddSingleton<IJobConnector>(new StubConnector("acme", ConnectorCapabilities.Search));
            registry.AddSingleton<IJobConnector>(new StubConnector("acme", ConnectorCapabilities.Feed));
        });

        var descriptor = Assert.Single(services.GetRequiredService<IConnectorRegistry>().Connectors);

        Assert.Equal(ConnectorCapabilities.Search, descriptor.Capabilities);
    }
}
