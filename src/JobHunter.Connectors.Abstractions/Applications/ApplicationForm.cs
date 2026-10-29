namespace JobHunter.Connectors.Abstractions.Applications;

public sealed record ApplicationForm(string FormHash, IReadOnlyList<FormField> Fields);
