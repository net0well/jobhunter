using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Features.Postings.Shared;

internal sealed record PostingDetail(
    Guid Id,
    string Title,
    Guid CompanyId,
    string Company,
    string? Location,
    WorkModel? WorkModel,
    string? Seniority,
    string? EmploymentType,
    string? DescriptionText,
    IReadOnlyList<string> Requirements,
    JobPostingStatus Status,
    int? MatchScore,
    IReadOnlyList<MatchReason> MatchReasons,
    string? DiscardReason,
    string? ApplyUrl,
    string? ApplyChannelKey,
    bool ManualApplyOnly,
    DedupConfidence DedupConfidence,
    DateTimeOffset? PublishedAt,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<PostingDetail.SourceItem> Sources)
{
    internal sealed record SourceItem(string ConnectorKey, string ExternalId, string SourceUrl, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, int MissedScans);

    // Requires Company and Sources to be loaded.
    public static PostingDetail From(JobPosting posting) => new(
        posting.Id,
        posting.Title,
        posting.CompanyId,
        posting.Company!.Name,
        posting.Location,
        posting.WorkModel,
        posting.Seniority,
        posting.EmploymentType,
        posting.DescriptionText,
        posting.Requirements,
        posting.Status,
        posting.MatchScore,
        posting.MatchReasons,
        posting.DiscardReason,
        posting.ApplyUrl,
        posting.ApplyChannelKey,
        posting.ApplyChannelKey is null,
        posting.DedupConfidence,
        posting.PublishedAt,
        posting.FirstSeenAt,
        posting.LastSeenAt,
        posting.ClosedAt,
        [.. posting.Sources.OrderBy(source => source.FirstSeenAt).Select(source =>
            new SourceItem(source.ConnectorKey, source.ExternalId, source.SourceUrl, source.FirstSeenAt, source.LastSeenAt, source.MissedScans))]);
}
