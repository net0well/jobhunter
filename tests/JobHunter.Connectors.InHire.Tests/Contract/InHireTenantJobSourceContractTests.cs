using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.InHire.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Contracts;

namespace JobHunter.Connectors.InHire.Tests.Contract;

public sealed class InHireTenantJobSourceContractTests : TenantJobSourceContract, IDisposable
{
    private readonly InHireTestHost _host = new();

    protected override IReadOnlyList<string> EquivalentTenantInputs => ["https://eyxo.inhire.app/vagas", "eyxo", "EYXO.inhire.app"];

    protected override IReadOnlyList<string> InvalidTenantInputs => ["", "https://example.com", "https://inhire.app/vagas", "https://api.inhire.app"];

    protected override TenantRef TenantWithPostings => new(InHireTestHost.Tenant);

    protected override TenantRef TenantWithoutPostings => new(InHireTestHost.EmptyTenant);

    protected override ITenantJobSource CreateSource() => _host.TenantSource;

    public void Dispose() => _host.Dispose();
}
