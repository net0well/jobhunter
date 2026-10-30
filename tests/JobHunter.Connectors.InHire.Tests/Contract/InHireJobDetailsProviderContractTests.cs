using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Contracts;

namespace JobHunter.Connectors.InHire.Tests.Contract;

public sealed class InHireJobDetailsProviderContractTests : JobDetailsProviderContract, IDisposable
{
    private readonly InHireTestHost _host = new();

    protected override string ConnectorKey => InHireConnectorRegistration.Key;

    protected override Uri OpenPostingUrl => InHireTestHost.JobUrl(InHireTestHost.OpenJobId);

    protected override Uri ClosedPostingUrl => InHireTestHost.JobUrl(InHireTestHost.ClosedJobId);

    protected override IJobDetailsProvider CreateProvider() => _host.Details;

    public void Dispose() => _host.Dispose();
}
