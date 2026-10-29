namespace JobHunter.Connectors.Abstractions.Applications;

public sealed record ApplicationSubmission(
    Uri ApplyUrl,
    string FormHash,
    IReadOnlyList<FieldValue> Values,
    string EvidenceDirectory);
