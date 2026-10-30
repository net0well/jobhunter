using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Testing.Builders;
using JobHunter.Connectors.Testing.Contracts;
using JobHunter.Connectors.Testing.Fakes;

namespace JobHunter.Api.Tests.Connectors.Fake;

public sealed class FakeApplicationChannelContractTests : ApplicationChannelContract
{
    private readonly FakeConnector _fake = new(TimeProvider.System);

    protected override string ConnectorKey => FakeConnector.Key;

    protected override Uri ApplyUrl => FakeConnector.ApplyUrl("acme", "backend-developer");

    protected override IApplicationChannel CreateChannel()
    {
        _fake.Forms[ApplyUrl] = ApplicationFormBuilder.Default()
            .WithFullName()
            .WithEmail()
            .WithResume()
            .WithSingleChoice("english", "Nível de inglês", "Básico", "Intermediário", "Fluente")
            .WithCustomQuestion("Por que você quer trabalhar aqui?")
            .Build();

        return _fake;
    }

    protected override int CountSubmissions() => _fake.Submissions.Count;
}
