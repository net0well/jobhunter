namespace JobHunter.Api.Common.Text;

internal static class UrlNormalizer
{
    private static readonly string[] TrackingParameters = ["gclid", "fbclid", "mc_cid", "mc_eid", "ref", "source", "trk"];

    // Two links to the same application page must compare equal regardless of tracking parameters or a trailing slash.
    public static string? Normalize(Uri? url)
    {
        if (url is null || !url.IsAbsoluteUri)
            return null;

        var query = url.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !IsTracking(pair.Split('=', 2)[0]))
            .ToList();

        var path = url.AbsolutePath.TrimEnd('/');
        var authority = url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
        var normalized = $"{url.Scheme}://{authority}{path}{(query.Count > 0 ? "?" + string.Join('&', query) : string.Empty)}";

        return normalized.ToLowerInvariant();
    }

    private static bool IsTracking(string parameter) =>
        parameter.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
        TrackingParameters.Contains(parameter, StringComparer.OrdinalIgnoreCase);
}
