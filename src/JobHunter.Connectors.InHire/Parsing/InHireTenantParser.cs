using System.Text.Json;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Connectors.InHire.Parsing;

internal static class InHireTenantParser
{
    // The tenant id goes in the X-Tenant header; the primary subdomain is the canonical host for job URLs.
    public static ConnectorResult<(string TenantId, string Subdomain)> Parse(string slug, string json)
    {
        InHireModels.TenantConfig? config;

        try
        {
            config = JsonSerializer.Deserialize<InHireModels.TenantConfig>(json, InHireModels.Json);
        }
        catch (JsonException ex)
        {
            return ConnectorResult<(string, string)>.Fail(InHireErrors.LayoutChanged($"Tenant configuration is not the expected JSON: {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(config?.Tenant?.Id))
            return ConnectorResult<(string, string)>.Fail(InHireErrors.LayoutChanged("Tenant configuration has no tenant.id."));

        var subdomain = config.Subdomain?.PrimarySubdomain ?? config.Subdomain?.Current ?? slug;
        return ConnectorResult<(string, string)>.Ok((config.Tenant.Id, subdomain));
    }
}
