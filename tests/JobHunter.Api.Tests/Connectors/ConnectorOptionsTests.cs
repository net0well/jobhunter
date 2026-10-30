using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Tests.Connectors;

public sealed class ConnectorOptionsTests
{
    [Fact]
    public async Task Get_NoConfiguration_UsesConnectorTraits()
    {
        await using var services = ConnectorTestServices.Build(registry => registry.AddFakeConnector());
        var traits = services.GetRequiredService<FakeConnector>().Descriptor.Traits;

        var options = services.GetRequiredService<IOptionsMonitor<ConnectorOptions>>().Get(FakeConnector.Key);

        Assert.Equal(traits.DefaultMaxConcurrency, options.MaxConcurrency);
        Assert.Equal(traits.DefaultScanInterval, options.ScanInterval);
        Assert.Equal(traits.DefaultPolitenessDelayMax, options.PolitenessDelayMax);
    }

    [Fact]
    public async Task Get_SectionNamedByKeyInAnyCase_OverridesTraits()
    {
        await using var services = ConnectorTestServices.Build(
            registry => registry.AddFakeConnector(),
            new() { ["Connectors:Fake:MaxConcurrency"] = "3", ["Connectors:Fake:DailySubmissionLimit"] = "2" });

        var options = services.GetRequiredService<IOptionsMonitor<ConnectorOptions>>().Get(FakeConnector.Key);

        Assert.Equal(3, options.MaxConcurrency);
        Assert.Equal(2, options.DailySubmissionLimit);
        Assert.Equal(TimeSpan.Zero, options.PolitenessDelayMax);
    }
}
