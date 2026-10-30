using System.Text.RegularExpressions;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Connectors.InHire;

internal static partial class InHireUrls
{
    private const string Domain = "inhire.app";

    // Subdomains used by InHire itself; they never identify a company career page.
    private static readonly HashSet<string> ServiceSubdomains = ["api", "app", "auth", "docs", "files", "form-app", "help", "www"];

    public static Uri CareerPage(TenantRef tenant) => new($"https://{tenant.Value}.{Domain}/vagas");

    public static Uri JobPage(string tenant, string jobId) => new($"https://{tenant}.{Domain}/vagas/{jobId}");

    // Accepts what a user pastes: the career page or a job URL, the bare host, or just the company slug.
    public static ConnectorResult<TenantRef> ParseTenant(string? input)
    {
        var text = input?.Trim() ?? string.Empty;

        var slug = text.IndexOfAny(['.', '/', ':']) < 0
            ? text.ToLowerInvariant()
            : Uri.TryCreate(text.Contains("://", StringComparison.Ordinal) ? text : $"https://{text}", UriKind.Absolute, out var url) && TryTenant(url.Host, out var host)
                ? host
                : null;

        return slug is not null && IsCompanySlug(slug)
            ? ConnectorResult<TenantRef>.Ok(new TenantRef(slug))
            : ConnectorResult<TenantRef>.Fail(new ConnectorError(ConnectorErrorKind.InvalidInput, "InHire.InvalidTenant", $"'{input}' is not an InHire career page (https://<company>.{Domain}/vagas) or company slug."));
    }

    public static bool TryParseJob(Uri url, out string tenant, out string jobId)
    {
        tenant = jobId = string.Empty;

        if (!url.IsAbsoluteUri || url.Scheme is not ("https" or "http") || !TryTenant(url.Host, out var host))
            return false;

        var segments = url.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2 || !segments[0].Equals("vagas", StringComparison.OrdinalIgnoreCase) || !JobId().IsMatch(segments[1]))
            return false;

        tenant = host;
        jobId = segments[1];
        return true;
    }

    private static bool TryTenant(string host, out string slug)
    {
        slug = string.Empty;
        var suffix = $".{Domain}";

        if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        slug = host[..^suffix.Length].ToLowerInvariant();
        return IsCompanySlug(slug);
    }

    private static bool IsCompanySlug(string slug) => Slug().IsMatch(slug) && !ServiceSubdomains.Contains(slug);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")]
    private static partial Regex Slug();

    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex JobId();
}
