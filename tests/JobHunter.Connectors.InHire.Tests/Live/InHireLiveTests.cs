using JobHunter.Connectors.Abstractions.Evidence;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Evidence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Connectors.InHire.Tests.Live;

// Talks to the real InHire. Excluded from the default run; execute by hand with --filter-trait "Category=Live".
[Trait("Category", "Live")]
public sealed class InHireLiveTests
{
    private static readonly TimeSpan Courtesy = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("eyxo")]
    [InlineData("db1")]
    public async Task ListAsync_RealCareerPage_ReturnsPublishedJobsWithDetails(string tenant)
    {
        var ct = TestContext.Current.CancellationToken;
        using var evidence = new TemporaryEvidenceStore();
        await using var services = Services(evidence);
        var source = services.GetRequiredKeyedService<ITenantJobSource>(InHireConnectorRegistration.Key);
        var details = services.GetRequiredKeyedService<IJobDetailsProvider>(InHireConnectorRegistration.Key);

        var listing = await source.ListAsync(new TenantRef(tenant), ct);

        Assert.True(listing.IsSuccess, listing.Error?.Message);
        if (listing.Value!.Count == 0)
            return;

        await Task.Delay(Courtesy, ct);
        var first = await details.GetDetailsAsync(listing.Value[0].SourceUrl, ct);

        Assert.True(first.IsSuccess || first.Error.Kind == ConnectorErrorKind.PostingClosed, first.Error?.Message);
    }

    private static ServiceProvider Services(IConnectorEvidenceStore evidence)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"Connectors:{InHireConnectorRegistration.Key}:Enabled"] = "true" })
            .Build();

        var services = new ServiceCollection();
        services.AddInHireConnector(configuration);
        services.AddSingleton(evidence);
        return services.BuildServiceProvider();
    }
}
