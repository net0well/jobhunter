using System.Text.Json;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Connectors.InHire.Parsing;

internal static class InHireDetailsParser
{
    private const int MaxExtraLength = 200;

    public static ConnectorResult<PostingDetails> Parse(string subdomain, string json)
    {
        InHireModels.JobPage? job;

        try
        {
            job = JsonSerializer.Deserialize<InHireModels.JobPage>(json, InHireModels.Json);
        }
        catch (JsonException ex)
        {
            return Fail($"Job page is not the expected JSON: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(job?.JobId) || string.IsNullOrWhiteSpace(job.DisplayName))
            return Fail("Job page has no jobId or displayName.");

        if (job.Status is not null && !job.Status.Equals("published", StringComparison.OrdinalIgnoreCase))
            return ConnectorResult<PostingDetails>.Fail(InHireErrors.PostingClosed(job.JobId));

        var extra = new Dictionary<string, string>();
        AddExtra(extra, "contractType", job.ContractType is { Count: > 0 } types ? string.Join(", ", types) : null);
        AddExtra(extra, "workplaceType", job.WorkplaceType);
        AddExtra(extra, "location", job.Location);
        AddExtra(extra, "locationComplement", job.LocationComplement);

        return ConnectorResult<PostingDetails>.Ok(new PostingDetails(
            job.JobId,
            job.DisplayName.Trim(),
            job.Description,
            HtmlText.ToPlainText(job.Description),
            [],
            InHireUrls.JobPage(subdomain, job.JobId),
            IsOpen: true,
            extra));
    }

    private static void AddExtra(Dictionary<string, string> extra, string key, string? value)
    {
        var trimmed = value?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            extra[key] = trimmed.Length <= MaxExtraLength ? trimmed : trimmed[..MaxExtraLength];
    }

    private static ConnectorResult<PostingDetails> Fail(string message) =>
        ConnectorResult<PostingDetails>.Fail(InHireErrors.LayoutChanged(message));
}
