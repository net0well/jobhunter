using System.Text.Json;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Connectors.InHire.Parsing;

internal static class InHireListingParser
{
    private const string Published = "published";

    public static ConnectorResult<IReadOnlyList<SourcedPosting>> Parse(string subdomain, string json)
    {
        InHireModels.Listing? listing;

        try
        {
            listing = JsonSerializer.Deserialize<InHireModels.Listing>(json, InHireModels.Json);
        }
        catch (JsonException ex)
        {
            return Fail($"Listing is not the expected JSON: {ex.Message}");
        }

        if (listing?.JobsPage is null)
            return Fail("Listing has no jobsPage array.");

        List<SourcedPosting> postings = [];

        foreach (var item in listing.JobsPage)
        {
            if (string.IsNullOrWhiteSpace(item.JobId) || string.IsNullOrWhiteSpace(item.DisplayName))
                return Fail("A listed job has no jobId or displayName.");

            // The career page only lists published jobs; anything else is skipped rather than reported.
            if (item.Status is not null && !item.Status.Equals(Published, StringComparison.OrdinalIgnoreCase))
                continue;

            var url = InHireUrls.JobPage(subdomain, item.JobId);
            postings.Add(new SourcedPosting(
                item.JobId,
                url,
                item.DisplayName.Trim(),
                listing.TenantName?.Trim(),
                item.Location?.Trim(),
                WorkModelOf(item.WorkplaceType),
                Seniority: null,
                EmploymentType: null,
                PublishedAt: null,
                DescriptionText: null,
                url,
                new Dictionary<string, string>()));
        }

        return ConnectorResult<IReadOnlyList<SourcedPosting>>.Ok(postings);
    }

    public static WorkModel? WorkModelOf(string? workplaceType) => workplaceType?.Trim().ToLowerInvariant() switch
    {
        "remote" => WorkModel.Remote,
        "hybrid" => WorkModel.Hybrid,
        "on-site" or "onsite" or "on site" => WorkModel.OnSite,
        _ => null,
    };

    private static ConnectorResult<IReadOnlyList<SourcedPosting>> Fail(string message) =>
        ConnectorResult<IReadOnlyList<SourcedPosting>>.Fail(InHireErrors.LayoutChanged(message));
}
