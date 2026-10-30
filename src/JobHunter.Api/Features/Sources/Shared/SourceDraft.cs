using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Sources.Shared;

internal sealed record SourceDraft(
    string ConnectorKey,
    SourceKind Kind,
    string? Input,
    SearchCriteria? SearchCriteria,
    string? DisplayName,
    TimeSpan? ScanInterval);
