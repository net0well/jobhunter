using JobHunter.Api.Common.Results;
using JobHunter.Api.Common.Text;
using JobHunter.Api.Domain.Enums;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Api.Domain;

internal sealed class JobPosting
{
    public const string LowScoreReason = "LowScore";

    private readonly List<PostingSource> _sources = [];

    private JobPosting(
        Guid id,
        Guid companyId,
        string title,
        string normalizedTitle,
        string fingerprint,
        JobPostingStatus status,
        DedupConfidence dedupConfidence,
        DateTimeOffset firstSeenAt,
        DateTimeOffset lastSeenAt)
    {
        Id = id;
        CompanyId = companyId;
        Title = title;
        NormalizedTitle = normalizedTitle;
        Fingerprint = fingerprint;
        Status = status;
        DedupConfidence = dedupConfidence;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = lastSeenAt;
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Company? Company { get; private set; }

    public string Title { get; private set; }

    public string NormalizedTitle { get; private set; }

    public string? Location { get; private set; }

    public string? NormalizedCity { get; private set; }

    public WorkModel? WorkModel { get; private set; }

    public string? Seniority { get; private set; }

    public string? EmploymentType { get; private set; }

    public string? DescriptionText { get; private set; }

    public string? DescriptionHtml { get; private set; }

    public List<string> Requirements { get; private set; } = [];

    public string? ApplyUrl { get; private set; }

    public string? NormalizedApplyUrl { get; private set; }

    public string? ApplyChannelKey { get; private set; }

    public string Fingerprint { get; private set; }

    public DedupConfidence DedupConfidence { get; private set; }

    public JobPostingStatus Status { get; private set; }

    public JobPostingStatus? StatusBeforeClose { get; private set; }

    public int? MatchScore { get; private set; }

    public List<MatchReason> MatchReasons { get; private set; } = [];

    public Guid? ScoredWithProfileId { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? DiscardReason { get; private set; }

    public bool IsManuallyDiscarded { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<PostingSource> Sources => _sources;

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public static JobPosting Create(Company company, PostingSnapshot snapshot, DateTimeOffset now)
    {
        var posting = new JobPosting(
            Guid.CreateVersion7(now),
            company.Id,
            snapshot.Title,
            Normalizer.Text(snapshot.Title),
            Normalizer.Fingerprint(company.NormalizedName, snapshot.Title, snapshot.Location),
            JobPostingStatus.New,
            DedupConfidence.Exact,
            now,
            now)
        {
            Company = company,
        };

        posting.Apply(snapshot, company.NormalizedName);
        return posting;
    }

    public PostingSource AddSource(Guid? sourceTargetId, string connectorKey, string externalId, Uri sourceUrl, IReadOnlyDictionary<string, string> extra, DateTimeOffset now)
    {
        var source = PostingSource.Create(Id, sourceTargetId, connectorKey, externalId, sourceUrl, extra, now);
        _sources.Add(source);
        return source;
    }

    // Merges what a source reports now; returns true when the application URL changed and the channel must be resolved again.
    public bool Refresh(PostingSnapshot snapshot, string normalizedCompanyName, DateTimeOffset now)
    {
        var previousApplyUrl = NormalizedApplyUrl;
        Apply(snapshot, normalizedCompanyName);
        LastSeenAt = now;
        return previousApplyUrl != NormalizedApplyUrl;
    }

    public bool UpdateFrom(PostingDetails details, DateTimeOffset now)
    {
        var previousApplyUrl = NormalizedApplyUrl;

        DescriptionHtml = details.DescriptionHtml ?? DescriptionHtml;
        DescriptionText = string.IsNullOrWhiteSpace(details.DescriptionText) ? DescriptionText : details.DescriptionText;
        Requirements = details.Requirements.Count > 0 ? [.. details.Requirements] : Requirements;
        SetApplyUrl(details.ApplyUrl);

        if (!details.IsOpen)
            Close(now);

        return previousApplyUrl != NormalizedApplyUrl;
    }

    public void MarkProbableDuplicate() => DedupConfidence = DedupConfidence.Probable;

    public void AssignChannel(string? channelKey) => ApplyChannelKey = channelKey;

    public bool CanBeScored => Status is JobPostingStatus.New or JobPostingStatus.Matched
        || (Status == JobPostingStatus.Discarded && !IsManuallyDiscarded);

    public Result ApplyMatch(MatchResult match, Guid profileId, int minScore)
    {
        if (!CanBeScored)
            return InvalidStatus("scored");

        MatchScore = match.Score;
        MatchReasons = [.. match.Reasons];
        ScoredWithProfileId = profileId;

        var discardReason = match.DiscardReason ?? (match.Score < minScore ? LowScoreReason : null);
        Status = discardReason is null ? JobPostingStatus.Matched : JobPostingStatus.Discarded;
        DiscardReason = discardReason;
        return Result.Success();
    }

    // Sends the posting back to scoring, e.g. after its company was blocked or unblocked.
    public bool RequestRescore()
    {
        if (!CanBeScored)
            return false;

        Status = JobPostingStatus.New;
        return true;
    }

    public Result Discard(string reason)
    {
        if (Status is not (JobPostingStatus.New or JobPostingStatus.Matched))
            return InvalidStatus("discarded");

        Status = JobPostingStatus.Discarded;
        DiscardReason = reason;
        IsManuallyDiscarded = true;
        return Result.Success();
    }

    public Result Restore()
    {
        if (Status != JobPostingStatus.Discarded)
            return InvalidStatus("restored");

        Status = JobPostingStatus.Matched;
        DiscardReason = null;
        IsManuallyDiscarded = false;
        return Result.Success();
    }

    public bool Close(DateTimeOffset now)
    {
        if (Status == JobPostingStatus.Closed)
            return false;

        StatusBeforeClose = Status;
        Status = JobPostingStatus.Closed;
        ClosedAt = now;
        return true;
    }

    // A posting that comes back is scored again, unless an application was already started for it.
    public bool Reopen()
    {
        if (Status != JobPostingStatus.Closed)
            return false;

        Status = StatusBeforeClose == JobPostingStatus.InApplication ? JobPostingStatus.InApplication : JobPostingStatus.New;
        StatusBeforeClose = null;
        ClosedAt = null;
        return true;
    }

    private void Apply(PostingSnapshot snapshot, string normalizedCompanyName)
    {
        Title = snapshot.Title;
        NormalizedTitle = Normalizer.Text(snapshot.Title);
        Location = snapshot.Location ?? Location;
        NormalizedCity = Normalizer.City(Location);
        WorkModel = snapshot.WorkModel ?? WorkModel ?? (Normalizer.MentionsRemote(Location) ? Connectors.Abstractions.Sources.WorkModel.Remote : null);
        Seniority = snapshot.Seniority ?? Seniority;
        EmploymentType = snapshot.EmploymentType ?? EmploymentType;
        DescriptionText = string.IsNullOrWhiteSpace(snapshot.DescriptionText) ? DescriptionText : snapshot.DescriptionText;
        PublishedAt = snapshot.PublishedAt ?? PublishedAt;
        Fingerprint = Normalizer.Fingerprint(normalizedCompanyName, Title, Location);
        SetApplyUrl(snapshot.ApplyUrl);
    }

    private void SetApplyUrl(Uri? applyUrl)
    {
        if (applyUrl is null)
            return;

        ApplyUrl = applyUrl.ToString();
        NormalizedApplyUrl = UrlNormalizer.Normalize(applyUrl);
    }

    private Result InvalidStatus(string action) =>
        Error.Conflict("Posting.InvalidStatus", $"A {Status} posting cannot be {action}.");
}
