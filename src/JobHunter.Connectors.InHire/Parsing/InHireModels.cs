using System.Text.Json;

namespace JobHunter.Connectors.InHire.Parsing;

// Shapes of the public career page API, limited to the fields the connector reads.
internal static class InHireModels
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal sealed record TenantConfig(TenantInfo? Tenant, SubdomainInfo? Subdomain);

    internal sealed record TenantInfo(string? Id, string? Name);

    internal sealed record SubdomainInfo(string? Current, string? PrimarySubdomain);

    internal sealed record Listing(string? TenantName, IReadOnlyList<ListingItem>? JobsPage);

    internal sealed record ListingItem(string? JobId, string? DisplayName, string? Status, string? WorkplaceType, string? Location);

    internal sealed record JobPage(
        string? JobId,
        string? DisplayName,
        string? Description,
        string? Status,
        IReadOnlyList<string>? ContractType,
        string? WorkplaceType,
        string? Location,
        string? LocationComplement);
}
