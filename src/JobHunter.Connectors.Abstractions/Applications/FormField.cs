namespace JobHunter.Connectors.Abstractions.Applications;

public sealed record FormField(
    string Key,
    string Label,
    FieldKind Kind,
    FieldSemantic Semantic,
    bool Required,
    IReadOnlyList<FieldOption> Options,
    int? MaxLength,
    string? HelpText);
