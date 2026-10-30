using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Parsing;
using JobHunter.Connectors.InHire.Tests.Infrastructure;

namespace JobHunter.Connectors.InHire.Tests.Parsing;

public sealed class InHireListingParserTests
{
    [Fact]
    public Task Parse_EyxoListing_MapsEveryPublishedJob()
    {
        var result = InHireListingParser.Parse("eyxo", Read("listing-eyxo-20261030.json"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.All(result.Value!, posting =>
        {
            Assert.False(string.IsNullOrWhiteSpace(posting.ExternalId));
            Assert.False(string.IsNullOrWhiteSpace(posting.Title));
            Assert.Equal("Eyxo Content Co.", posting.CompanyName);
            Assert.NotNull(posting.Location);
            Assert.NotNull(posting.WorkModel);
            Assert.Equal(posting.SourceUrl, posting.ApplyUrl);
        });

        return Verify(result.Value);
    }

    [Fact]
    public void Parse_Db1Listing_ReadsAnotherCompanyWithTheSameShape()
    {
        var result = InHireListingParser.Parse("db1", Read("listing-db1-20261030.json"));

        Assert.Equal(8, result.Value?.Count);
        Assert.All(result.Value!, posting => Assert.StartsWith("https://db1.inhire.app/vagas/", posting.SourceUrl.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_SameListingTwice_KeepsExternalIds()
    {
        var json = Read("listing-eyxo-20261030.json");

        var first = InHireListingParser.Parse("eyxo", json).Value!.Select(posting => posting.ExternalId);
        var second = InHireListingParser.Parse("eyxo", json).Value!.Select(posting => posting.ExternalId);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("""{"tenantName":"Eyxo"}""")]
    [InlineData("""{"tenantName":"Eyxo","jobsPage":[{"displayName":"Sem id"}]}""")]
    [InlineData("""{"tenantName":"Eyxo","jobsPage":"not a list"}""")]
    [InlineData("<html>not json</html>")]
    public void Parse_UnexpectedShape_ReturnsLayoutChanged(string json)
    {
        var result = InHireListingParser.Parse("eyxo", json);

        Assert.Equal(ConnectorErrorKind.LayoutChanged, result.Error?.Kind);
    }

    [Fact]
    public void Parse_UnpublishedJob_IsSkipped()
    {
        var result = InHireListingParser.Parse("eyxo", """{"tenantName":"Eyxo","jobsPage":[{"jobId":"a","displayName":"A","status":"published"},{"jobId":"b","displayName":"B","status":"closed"}]}""");

        Assert.Equal("a", Assert.Single(result.Value!).ExternalId);
    }

    [Theory]
    [InlineData("Remote", WorkModel.Remote)]
    [InlineData("Hybrid", WorkModel.Hybrid)]
    [InlineData("On-site", WorkModel.OnSite)]
    [InlineData("Presencial", null)]
    [InlineData(null, null)]
    public void WorkModelOf_PlatformValues_MapToWorkModel(string? workplaceType, WorkModel? expected)
    {
        Assert.Equal(expected, InHireListingParser.WorkModelOf(workplaceType));
    }

    private static string Read(string fixture) => File.ReadAllText(InHireTestHost.Fixture(fixture));
}
