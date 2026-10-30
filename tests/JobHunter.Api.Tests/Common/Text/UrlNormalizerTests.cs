using JobHunter.Api.Common.Text;

namespace JobHunter.Api.Tests.Common.Text;

public sealed class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://Acme.InHire.app/vagas/123/", "https://acme.inhire.app/vagas/123")]
    [InlineData("https://acme.inhire.app/vagas/123?utm_source=linkedin&utm_medium=social", "https://acme.inhire.app/vagas/123")]
    [InlineData("https://jobs.example.com/apply?id=9&gclid=abc", "https://jobs.example.com/apply?id=9")]
    [InlineData("https://jobs.example.com:443/apply#form", "https://jobs.example.com/apply")]
    [InlineData("http://localhost:8080/jobs/1", "http://localhost:8080/jobs/1")]
    public void Normalize_TrackingAndFormattingDifferences_AreRemoved(string input, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(new Uri(input)));
    }

    [Fact]
    public void Normalize_Null_ReturnsNull()
    {
        Assert.Null(UrlNormalizer.Normalize(null));
    }
}
