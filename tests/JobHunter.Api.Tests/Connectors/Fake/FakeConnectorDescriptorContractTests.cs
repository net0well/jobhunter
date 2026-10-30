using JobHunter.Connectors.Testing.Contracts;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Connectors.Fake;

public sealed class FakeConnectorDescriptorContractTests : ConnectorDescriptorContract
{
    protected override string ExpectedKey => FakeConnector.Key;

    protected override void Register(IServiceCollection services) => services.AddFakeConnector();
}
