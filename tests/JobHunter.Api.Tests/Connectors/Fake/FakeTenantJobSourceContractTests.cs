using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Builders;
using JobHunter.Connectors.Testing.Contracts;
using JobHunter.Connectors.Testing.Fakes;

namespace JobHunter.Api.Tests.Connectors.Fake;

public sealed class FakeTenantJobSourceContractTests : TenantJobSourceContract
{
    protected override IReadOnlyList<string> EquivalentTenantInputs => ["https://jobs.fake.test/acme", "acme", "  acme  "];

    protected override IReadOnlyList<string> InvalidTenantInputs => ["", "   ", "Acme Corp", "https://acme.inhire.app/vagas", "https://jobs.fake.test/acme/backend-developer"];

    protected override TenantRef TenantWithPostings => new("acme");

    protected override TenantRef TenantWithoutPostings => new("empty-co");

    protected override ITenantJobSource CreateSource()
    {
        var fake = new FakeConnector(TimeProvider.System);
        fake.Tenants["acme"] =
        [
            SourcedPostingBuilder.Default("acme", "backend-developer").Build(),
            SourcedPostingBuilder.Default("acme", "data-engineer").WithTitle("Data Engineer").Build(),
        ];
        fake.Tenants["empty-co"] = [];
        return fake;
    }
}
