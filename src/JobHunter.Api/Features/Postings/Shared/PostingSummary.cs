using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Postings.Shared;

internal sealed record PostingSummary(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    WorkModel? WorkModel,
    JobPostingStatus Status,
    int? MatchScore,
    string? ApplyChannelKey,
    string? ApplyUrl,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
