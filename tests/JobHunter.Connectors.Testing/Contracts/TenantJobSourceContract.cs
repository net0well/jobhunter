using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using Xunit;

namespace JobHunter.Connectors.Testing.Contracts;

public abstract class TenantJobSourceContract
{
    // The same tenant written in every form a user may paste (full URL, bare id...).
    protected abstract IReadOnlyList<string> EquivalentTenantInputs { get; }

    protected abstract IReadOnlyList<string> InvalidTenantInputs { get; }

    protected abstract TenantRef TenantWithPostings { get; }

    protected abstract TenantRef TenantWithoutPostings { get; }

    protected abstract ITenantJobSource CreateSource();

    [Fact]
    public void ParseTenant_EquivalentInputs_ReturnSameTenant()
    {
        var source = CreateSource();
        Assert.True(EquivalentTenantInputs.Count >= 2, "Provide at least a full URL and a bare identifier");

        var results = EquivalentTenantInputs.Select(source.ParseTenant).ToList();

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Single(results.Select(result => result.Value!.Value).Distinct());
    }

    [Fact]
    public void ParseTenant_InvalidInputs_ReturnInvalidInput()
    {
        var source = CreateSource();

        var results = InvalidTenantInputs.Select(source.ParseTenant);

        Assert.All(results, result => Assert.Equal(ConnectorErrorKind.InvalidInput, result.Error?.Kind));
    }

    [Fact]
    public void GetTenantUrl_ParsedTenant_RoundTripsThroughParseTenant()
    {
        var source = CreateSource();
        var tenant = source.ParseTenant(EquivalentTenantInputs[0]).Value!;

        var url = source.GetTenantUrl(tenant);

        Assert.True(url.IsAbsoluteUri);
        Assert.Equal(tenant, source.ParseTenant(url.ToString()).Value);
    }

    [Fact]
    public async Task ListAsync_TenantWithPostings_ReturnsPostingsWithRequiredFields()
    {
        var result = await CreateSource().ListAsync(TenantWithPostings, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEmpty(result.Value!);
        Assert.All(result.Value!, posting =>
        {
            Assert.False(string.IsNullOrWhiteSpace(posting.ExternalId));
            Assert.False(string.IsNullOrWhiteSpace(posting.Title));
            Assert.True(posting.SourceUrl.IsAbsoluteUri);
            Assert.NotNull(posting.Extra);
        });
    }

    [Fact]
    public async Task ListAsync_TenantWithPostings_ReturnsUniqueExternalIds()
    {
        var result = await CreateSource().ListAsync(TenantWithPostings, TestContext.Current.CancellationToken);

        var ids = result.Value!.Select(posting => posting.ExternalId).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ListAsync_CalledTwice_ReturnsStableExternalIds()
    {
        var source = CreateSource();

        var first = await source.ListAsync(TenantWithPostings, TestContext.Current.CancellationToken);
        var second = await source.ListAsync(TenantWithPostings, TestContext.Current.CancellationToken);

        Assert.Equal(
            first.Value!.Select(posting => posting.ExternalId).Order(StringComparer.Ordinal),
            second.Value!.Select(posting => posting.ExternalId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListAsync_TenantWithoutPostings_ReturnsEmptySuccess()
    {
        var result = await CreateSource().ListAsync(TenantWithoutPostings, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value!);
    }
}
