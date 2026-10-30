using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Connectors;

public sealed class ConnectorRegistryValidatorTests
{
    [Fact]
    public async Task StartAsync_ValidRegistration_Succeeds()
    {
        await using var services = ConnectorTestServices.Build(registry => registry.AddFakeConnector());

        await services.RegistryValidator().StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_DuplicateKey_FailsCitingTheKey()
    {
        await using var services = ConnectorTestServices.Build(registry =>
        {
            registry.AddSingleton<IJobConnector>(new StubConnector("acme", ConnectorCapabilities.None));
            registry.AddSingleton<IJobConnector>(new StubConnector("acme", ConnectorCapabilities.None));
        });

        var error = await StartAsync(services);

        Assert.Contains("'acme' is registered 2 times", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_DeclaredCapabilityWithoutService_FailsCitingConnectorAndCapability()
    {
        await using var services = ConnectorTestServices.Build(registry =>
            registry.AddSingleton<IJobConnector>(new StubConnector("broken", ConnectorCapabilities.TenantListing)));

        var error = await StartAsync(services);

        Assert.Contains("'broken' declares TenantListing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_ServiceWithoutDeclaredCapability_Fails()
    {
        await using var services = ConnectorTestServices.Build(registry =>
        {
            registry.AddSingleton<IJobConnector>(new StubConnector("sneaky", ConnectorCapabilities.None));
            registry.AddKeyedSingleton<ITenantJobSource>("sneaky", (_, _) => new FakeConnector(TimeProvider.System));
        });

        var error = await StartAsync(services);

        Assert.Contains("'sneaky' registers ITenantJobSource without declaring", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("InHire")]
    [InlineData("in_hire")]
    [InlineData("")]
    public async Task StartAsync_KeyOutsideSlugFormat_Fails(string key)
    {
        await using var services = ConnectorTestServices.Build(registry =>
            registry.AddSingleton<IJobConnector>(new StubConnector(key, ConnectorCapabilities.None)));

        var error = await StartAsync(services);

        Assert.Contains("must match [a-z0-9-]+", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_InvalidConnectorOptions_FailsCitingTheSection()
    {
        await using var services = ConnectorTestServices.Build(
            registry => registry.AddFakeConnector(),
            new() { ["Connectors:fake:PolitenessDelayMin"] = "00:00:10", ["Connectors:fake:PolitenessDelayMax"] = "00:00:01" });

        var error = await StartAsync(services);

        Assert.Contains("Connectors:fake: PolitenessDelayMin", error.Message, StringComparison.Ordinal);
    }

    private static async Task<InvalidOperationException> StartAsync(IServiceProvider services) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.RegistryValidator().StartAsync(TestContext.Current.CancellationToken));
}
