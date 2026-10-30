using System.ComponentModel.DataAnnotations;

namespace JobHunter.Api.Common.Options;

internal sealed class ArtifactsOptions
{
    public const string Section = "Artifacts";

    // Relative paths are resolved against the content root, so the default lands in the repository's ignored Artifacts folder.
    [Required]
    public string RootPath { get; init; } = "../../Artifacts";

    [Range(1, 3650)]
    public int RetentionDays { get; init; } = 30;
}
