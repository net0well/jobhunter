using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Contracts;
using JobHunter.Connectors.Testing.Fakes;

namespace JobHunter.Api.Tests.Connectors.Fake;

public sealed class FakeJobDetailsProviderContractTests : JobDetailsProviderContract
{
    protected override string ConnectorKey => FakeConnector.Key;

    protected override Uri OpenPostingUrl => FakeConnector.PostingUrl("acme", "backend-developer");

    protected override Uri ClosedPostingUrl => FakeConnector.PostingUrl("acme", "closed-role");

    protected override IJobDetailsProvider CreateProvider()
    {
        var fake = new FakeConnector(TimeProvider.System);
        fake.Details[OpenPostingUrl] = Details("backend-developer", isOpen: true);
        fake.Details[ClosedPostingUrl] = Details("closed-role", isOpen: false);
        return fake;
    }

    private static PostingDetails Details(string externalId, bool isOpen) =>
        new(externalId, "Backend Developer", "<p>Build .NET services</p>", "Build .NET services", ["C#", "PostgreSQL"], null, isOpen, new Dictionary<string, string>());
}
