namespace JobHunter.Connectors.Abstractions.Sources;

public sealed record PostingDetails(
    string ExternalId,
    string Title,
    string? DescriptionHtml,
    string DescriptionText,
    IReadOnlyList<string> Requirements,
    Uri? ApplyUrl,
    bool IsOpen,
    IReadOnlyDictionary<string, string> Extra);
