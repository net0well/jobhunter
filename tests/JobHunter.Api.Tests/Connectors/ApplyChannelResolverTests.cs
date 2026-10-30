using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Testing.Builders;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Connectors;

public sealed class ApplyChannelResolverTests
{
    [Fact]
    public async Task Resolve_UrlHandledByFakeChannel_ReturnsFakeKey()
    {
        await using var services = ConnectorTestServices.Build(registry => registry.AddFakeConnector());

        var key = services.GetRequiredService<ApplyChannelResolver>().Resolve(FakeConnector.ApplyUrl("acme", "backend-developer"));

        Assert.Equal(FakeConnector.Key, key);
    }

    [Fact]
    public async Task Resolve_UrlFromAnotherPlatform_ReturnsNull()
    {
        await using var services = ConnectorTestServices.Build(registry => registry.AddFakeConnector());

        var key = services.GetRequiredService<ApplyChannelResolver>().Resolve(SampleUrls.Postings["inhire"]);

        Assert.Null(key);
    }

    [Fact]
    public async Task Resolve_TwoChannelsClaimUrl_ReturnsFirstRegistered()
    {
        const ConnectorCapabilities apply = ConnectorCapabilities.ApplicationInspect | ConnectorCapabilities.ApplicationSubmit;
        string[] claimants = ["first", "second"];
        await using var services = ConnectorTestServices.Build(registry =>
        {
            foreach (var key in claimants)
            {
                registry.AddSingleton<IJobConnector>(new StubConnector(key, apply));
                registry.AddKeyedSingleton<IApplicationChannel>(key, new ClaimEverythingChannel());
            }
        });

        var key = services.GetRequiredService<ApplyChannelResolver>().Resolve(SampleUrls.Postings["gupy"]);

        Assert.Equal("first", key);
    }
}
