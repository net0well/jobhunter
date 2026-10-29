namespace JobHunter.Connectors.Abstractions.Sources;

public sealed record DiscoveredTenant(TenantRef Tenant, string? CompanyName, Uri Url);
