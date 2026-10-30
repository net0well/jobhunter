using System.Net;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Tests.Infrastructure;

namespace JobHunter.Connectors.InHire.Tests.Api;

public sealed class InHireHttpBehaviorTests : IDisposable
{
    private readonly InHireTestHost _host = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAsync_Tenant_SendsHonestIdentityAndOnlyTheTenantHeader()
    {
        await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.All(_host.Http.Requests, request =>
        {
            Assert.StartsWith("JobHunter/", request.Headers["User-Agent"], StringComparison.Ordinal);
            Assert.False(request.Headers.ContainsKey("X-Inhire-Client"));
            Assert.False(request.Headers.ContainsKey("Origin"));
        });
        Assert.Equal(InHireTestHost.Tenant, _host.Http.Requests.Last().Headers["X-Tenant"]);
    }

    [Fact]
    public async Task ListAsync_UnknownTenant_ReturnsNotFound()
    {
        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.MissingTenant), Ct);

        Assert.Equal(ConnectorErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task GetDetailsAsync_RemovedJob_ReturnsPostingClosed()
    {
        var result = await _host.Details.GetDetailsAsync(InHireTestHost.JobUrl(InHireTestHost.ClosedJobId), Ct);

        Assert.Equal(ConnectorErrorKind.PostingClosed, result.Error?.Kind);
    }

    [Fact]
    public async Task ListAsync_TooManyRequests_ReturnsRateLimitedWithRetryAfter()
    {
        _host.Http.Respond(InHireTestHost.Api($"tenants/public/config/resolve/{InHireTestHost.Tenant}"), HttpStatusCode.TooManyRequests, "{}", headers: new Dictionary<string, string> { ["Retry-After"] = "120" });

        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.Equal(ConnectorErrorKind.RateLimited, result.Error?.Kind);
        Assert.Equal(TimeSpan.FromSeconds(120), result.Error?.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ListAsync_ServerError_ReturnsTransient(HttpStatusCode status)
    {
        _host.Http.Respond(InHireTestHost.Api($"tenants/public/config/resolve/{InHireTestHost.Tenant}"), status, "{}");

        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.Equal(ConnectorErrorKind.Transient, result.Error?.Kind);
    }

    [Fact]
    public async Task ListAsync_Timeout_ReturnsTransient()
    {
        _host.Http.Fail(InHireTestHost.Api($"tenants/public/config/resolve/{InHireTestHost.Tenant}"), new TaskCanceledException("The request timed out."));

        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.Equal("InHire.Timeout", result.Error?.Code);
    }

    [Fact]
    public async Task ListAsync_NetworkFailure_ReturnsTransient()
    {
        _host.Http.Fail(InHireTestHost.Api($"tenants/public/config/resolve/{InHireTestHost.Tenant}"), new HttpRequestException("Connection refused"));

        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.Equal(ConnectorErrorKind.Transient, result.Error?.Kind);
    }

    [Fact]
    public async Task ListAsync_ChallengePage_ReturnsCaptchaDetectedWithSavedEvidence()
    {
        _host.Http.Respond(
            InHireTestHost.Api("job-posts/public/pages"),
            HttpStatusCode.Forbidden,
            File.ReadAllText(InHireTestHost.Fixture("challenge.html")),
            "text/html",
            headers: new Dictionary<string, string> { ["cf-mitigated"] = "challenge" });

        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.Equal(ConnectorErrorKind.CaptchaDetected, result.Error?.Kind);
        Assert.True(File.Exists(result.Error?.EvidencePath));
    }

    [Fact]
    public async Task ListAsync_UnexpectedStatus_ReturnsLayoutChangedWithEvidence()
    {
        _host.Http.Respond(InHireTestHost.Api("job-posts/public/pages"), HttpStatusCode.Gone, """{"message":"moved"}""");

        var result = await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), Ct);

        Assert.Equal(ConnectorErrorKind.LayoutChanged, result.Error?.Kind);
        Assert.Contains("moved", File.ReadAllText(result.Error!.EvidencePath!), StringComparison.Ordinal);
    }

    public void Dispose() => _host.Dispose();
}
