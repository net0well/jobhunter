using JobHunter.Connectors.InHire.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Contracts;
using JobHunter.Connectors.Testing.Evidence;
using JobHunter.Connectors.Testing.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Connectors.InHire.Tests.Contract;

public sealed class InHireDescriptorContractTests : ConnectorDescriptorContract
{
    protected override string ExpectedKey => InHireConnectorRegistration.Key;

    protected override void Register(IServiceCollection services) =>
        InHireTestHost.Register(services, new FixtureHttpHandler(), new TemporaryEvidenceStore());
}
