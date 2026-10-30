using System.ComponentModel.DataAnnotations;

namespace JobHunter.Connectors.InHire;

// Bound from Connectors:inhire, the same section the host reads for rate limits and courtesy delays.
public sealed class InHireOptions
{
    public const string DefaultUserAgent = "JobHunter/1.0 (+https://github.com/net0well/jobhunter)";

    [Required]
    public Uri ApiBaseUrl { get; set; } = new("https://api.inhire.app/");

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [Required]
    public string UserAgent { get; set; } = DefaultUserAgent;
}
