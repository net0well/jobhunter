using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Results;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Sources;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Sources.Shared;

// Builds a source after checking that the connector exists, has the capability its kind needs and understands the input.
internal sealed class SourceFactory(AppDbContext db, IConnectorRegistry registry, TimeProvider clock)
{
    public async Task<Result<SourceTarget>> CreateAsync(SourceDraft draft, CancellationToken ct)
    {
        var descriptor = registry.Find(draft.ConnectorKey);
        if (descriptor is null)
            return Error.NotFound("Connector.NotFound", $"Connector {draft.ConnectorKey} is not enabled.");

        string? tenantRef = null;
        string displayName;

        switch (draft.Kind)
        {
            case SourceKind.Tenant:
                if (!registry.TryGet<ITenantJobSource>(draft.ConnectorKey, out var tenants))
                    return CapabilityMissing(draft);

                var parsed = tenants.ParseTenant(draft.Input ?? string.Empty);
                if (!parsed.IsSuccess)
                    return Error.Validation("Source.InvalidInput", parsed.Error.Message);

                tenantRef = parsed.Value!.Value;
                displayName = draft.DisplayName ?? tenantRef;
                break;

            case SourceKind.Search:
                if (!registry.TryGet<ISearchJobSource>(draft.ConnectorKey, out _))
                    return CapabilityMissing(draft);

                displayName = draft.DisplayName ?? $"Search: {string.Join(", ", draft.SearchCriteria!.Keywords)}";
                break;

            default:
                if (!registry.TryGet<IFeedJobSource>(draft.ConnectorKey, out _))
                    return CapabilityMissing(draft);

                displayName = draft.DisplayName ?? descriptor.DisplayName;
                break;
        }

        if (await IsDuplicateAsync(draft, tenantRef, ct))
            return Error.Conflict("Source.Duplicate", $"This {draft.Kind} source is already registered for {draft.ConnectorKey}.");

        return SourceTarget.Create(draft.ConnectorKey, draft.Kind, tenantRef, draft.SearchCriteria, displayName, draft.ScanInterval, clock.GetUtcNow());
    }

    // A connector has a single feed, and a tenant is registered once per connector; searches may repeat with other criteria.
    private Task<bool> IsDuplicateAsync(SourceDraft draft, string? tenantRef, CancellationToken ct) => draft.Kind switch
    {
        SourceKind.Tenant => db.SourceTargets.AnyAsync(source => source.ConnectorKey == draft.ConnectorKey && source.Kind == SourceKind.Tenant && source.TenantRef == tenantRef, ct),
        SourceKind.Feed => db.SourceTargets.AnyAsync(source => source.ConnectorKey == draft.ConnectorKey && source.Kind == SourceKind.Feed, ct),
        _ => Task.FromResult(false),
    };

    private static Error CapabilityMissing(SourceDraft draft) =>
        Error.Conflict("Connector.CapabilityMissing", $"Connector {draft.ConnectorKey} cannot handle {draft.Kind} sources.");
}
