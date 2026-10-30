using System.Net;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Contracts;

namespace JobHunter.Connectors.InHire.Tests.Contract;

public sealed class InHireErrorHandlingContractTests : ErrorHandlingContract, IDisposable
{
    private readonly InHireTestHost _host = new();

    protected override async Task<ConnectorError?> RunAgainstUnexpectedLayoutAsync(CancellationToken ct)
    {
        _host.Http.Respond(InHireTestHost.Api("job-posts/public/pages"), HttpStatusCode.OK, """{"tenantName":"Eyxo","jobs":[]}""");
        return (await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), ct)).Error;
    }

    protected override async Task<ConnectorError?> RunAgainstCaptchaPageAsync(CancellationToken ct)
    {
        _host.Http.Respond(
            InHireTestHost.Api($"tenants/public/config/resolve/{InHireTestHost.Tenant}"),
            HttpStatusCode.Forbidden,
            File.ReadAllText(InHireTestHost.Fixture("challenge.html")),
            "text/html");
        return (await _host.TenantSource.ListAsync(new TenantRef(InHireTestHost.Tenant), ct)).Error;
    }

    public void Dispose() => _host.Dispose();
}
