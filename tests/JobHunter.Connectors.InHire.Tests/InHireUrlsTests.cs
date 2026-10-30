using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.InHire.Tests;

public sealed class InHireUrlsTests
{
    [Theory]
    [InlineData("https://eyxo.inhire.app/vagas")]
    [InlineData("https://Eyxo.InHire.app/vagas/")]
    [InlineData("EYXO.inhire.app")]
    [InlineData("eyxo")]
    [InlineData("  eyxo  ")]
    [InlineData("https://eyxo.inhire.app/vagas/5f8b856e-f4a1-486c-94c2-385c18e483e7/analista")]
    public void ParseTenant_EveryWayOfWritingTheCompany_ReturnsTheSlug(string input)
    {
        var result = InHireUrls.ParseTenant(input);

        Assert.Equal("eyxo", result.Value?.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("https://inhire.app/vagas")]
    [InlineData("https://api.inhire.app")]
    [InlineData("https://files.inhire.app/og-images/eyxo.png")]
    [InlineData("eyxo empresa")]
    [InlineData("-eyxo")]
    [InlineData("https://eyxo.inhire.app.example.com/vagas")]
    public void ParseTenant_AnythingElse_ReturnsInvalidInput(string input)
    {
        var result = InHireUrls.ParseTenant(input);

        Assert.Equal(ConnectorErrorKind.InvalidInput, result.Error?.Kind);
    }

    [Theory]
    [InlineData("https://eyxo.inhire.app/vagas/5f8b856e-f4a1-486c-94c2-385c18e483e7", "eyxo", "5f8b856e-f4a1-486c-94c2-385c18e483e7")]
    [InlineData("https://DB1.inhire.app/vagas/abc-123/backend-developer", "db1", "abc-123")]
    public void TryParseJob_JobUrl_ExtractsTenantAndJobId(string url, string tenant, string jobId)
    {
        Assert.True(InHireUrls.TryParseJob(new Uri(url), out var parsedTenant, out var parsedJobId));
        Assert.Equal(tenant, parsedTenant);
        Assert.Equal(jobId, parsedJobId);
    }

    [Theory]
    [InlineData("https://eyxo.inhire.app/vagas")]
    [InlineData("https://eyxo.inhire.app/indique-uma-pessoa/123")]
    [InlineData("https://api.inhire.app/vagas/123")]
    [InlineData("https://eyxo.gupy.io/vagas/123")]
    public void TryParseJob_NotAJobPage_ReturnsFalse(string url)
    {
        Assert.False(InHireUrls.TryParseJob(new Uri(url), out _, out _));
    }
}
