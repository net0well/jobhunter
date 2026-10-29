using System.Net;
using JobHunter.Api.Common.Health;
using JobHunter.Api.Tests.Infrastructure;

namespace JobHunter.Api.Tests.Common.Health;

[Collection(nameof(ApiCollection))]
public sealed class HealthEndpointTests(JobHunterApiFactory factory)
{
    [Theory]
    [InlineData(HealthEndpointExtensions.LivenessPath)]
    [InlineData(HealthEndpointExtensions.ReadinessPath)]
    public async Task Get_DatabaseAvailable_ReturnsHealthyWithoutApiKey(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Get_ApiDocumentationOutsideDevelopment_ReturnsNotFound(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
