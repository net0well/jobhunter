namespace JobHunter.Connectors.Abstractions.Applications;

public sealed record FieldValue(
    string Key,
    string? Text,
    IReadOnlyList<string>? SelectedOptions,
    string? FilePath);
