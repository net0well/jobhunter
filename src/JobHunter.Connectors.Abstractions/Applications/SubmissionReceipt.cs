namespace JobHunter.Connectors.Abstractions.Applications;

public sealed record SubmissionReceipt(
    DateTimeOffset SubmittedAt,
    string? ConfirmationText,
    string? ExternalApplicationId,
    string? EvidencePath);
