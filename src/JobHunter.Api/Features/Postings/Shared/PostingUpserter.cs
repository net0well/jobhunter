using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Common.Text;
using JobHunter.Api.Domain;
using JobHunter.Connectors.Abstractions.Sources;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Features.Postings.Shared;

// Merges what a connector returned into canonical postings. Matching order: same source item, same application URL,
// then same company/title/city fingerprint seen recently (flagged as a probable duplicate).
internal sealed class PostingUpserter(AppDbContext db, ApplyChannelResolver channels, TimeProvider clock)
{
    public static readonly TimeSpan FingerprintWindow = TimeSpan.FromDays(30);

    private readonly Dictionary<string, Company> _companies = [];
    private readonly Dictionary<string, JobPosting> _byApplyUrl = [];
    private readonly Dictionary<string, JobPosting> _byFingerprint = [];

    public async Task<UpsertOutcome> UpsertAsync(
        string connectorKey,
        Guid? sourceTargetId,
        string fallbackCompanyName,
        IReadOnlyList<SourcedPosting> items,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var batch = items.DistinctBy(item => item.ExternalId).ToList();
        var externalIds = batch.Select(item => item.ExternalId).ToList();

        var known = await db.PostingSources
            .Where(source => source.ConnectorKey == connectorKey && externalIds.Contains(source.ExternalId))
            .ToDictionaryAsync(source => source.ExternalId, ct);

        var knownPostingIds = known.Values.Select(source => source.JobPostingId).Distinct().ToList();
        var knownPostings = await db.JobPostings.Include(posting => posting.Company)
            .Where(posting => knownPostingIds.Contains(posting.Id))
            .ToDictionaryAsync(posting => posting.Id, ct);

        int created = 0, updated = 0, deduplicated = 0;
        List<CreatedPosting> createdPostings = [];
        HashSet<Guid> seen = [];

        foreach (var item in batch)
        {
            var snapshot = ToSnapshot(item);

            if (known.TryGetValue(item.ExternalId, out var knownSource))
            {
                knownSource.Seen(item.SourceUrl, item.Extra, now);
                Merge(knownPostings[knownSource.JobPostingId], snapshot, now);
                seen.Add(knownSource.Id);
                updated++;
                continue;
            }

            var company = await CompanyAsync(item.CompanyName ?? fallbackCompanyName, now, ct);
            var duplicate = await FindDuplicateAsync(company, snapshot, now, ct);

            if (duplicate is not null)
            {
                Merge(duplicate, snapshot, now);
                seen.Add(AttachSource(duplicate, sourceTargetId, connectorKey, item, now));
                deduplicated++;
                continue;
            }

            var posting = JobPosting.Create(company, snapshot, now);
            posting.AssignChannel(ResolveChannel(posting));
            db.JobPostings.Add(posting);
            seen.Add(AttachSource(posting, sourceTargetId, connectorKey, item, now));
            Remember(posting);

            createdPostings.Add(new CreatedPosting(posting, item.SourceUrl));
            created++;
        }

        return new UpsertOutcome(created, updated, deduplicated, createdPostings, seen);
    }

    public string? ResolveChannel(JobPosting posting) =>
        posting.ApplyUrl is null ? null : channels.Resolve(new Uri(posting.ApplyUrl));

    private void Merge(JobPosting posting, PostingSnapshot snapshot, DateTimeOffset now)
    {
        if (posting.Refresh(snapshot, posting.Company!.NormalizedName, now))
            posting.AssignChannel(ResolveChannel(posting));

        posting.Reopen();
        Remember(posting);
    }

    private Guid AttachSource(JobPosting posting, Guid? sourceTargetId, string connectorKey, SourcedPosting item, DateTimeOffset now)
    {
        var source = posting.AddSource(sourceTargetId, connectorKey, item.ExternalId, item.SourceUrl, item.Extra, now);
        db.PostingSources.Add(source);
        return source.Id;
    }

    private async Task<JobPosting?> FindDuplicateAsync(Company company, PostingSnapshot snapshot, DateTimeOffset now, CancellationToken ct)
    {
        var applyUrl = UrlNormalizer.Normalize(snapshot.ApplyUrl);
        if (applyUrl is not null)
        {
            var sameApplyUrl = _byApplyUrl.GetValueOrDefault(applyUrl)
                ?? await db.JobPostings.Include(posting => posting.Company).FirstOrDefaultAsync(posting => posting.NormalizedApplyUrl == applyUrl, ct);

            if (sameApplyUrl is not null)
                return sameApplyUrl;
        }

        var fingerprint = Normalizer.Fingerprint(company.NormalizedName, snapshot.Title, snapshot.Location);
        var windowStart = now - FingerprintWindow;
        var sameFingerprint = _byFingerprint.GetValueOrDefault(fingerprint)
            ?? await db.JobPostings.Include(posting => posting.Company)
                .Where(posting => posting.Fingerprint == fingerprint && posting.FirstSeenAt >= windowStart)
                .OrderByDescending(posting => posting.FirstSeenAt)
                .FirstOrDefaultAsync(ct);

        sameFingerprint?.MarkProbableDuplicate();
        return sameFingerprint;
    }

    private async Task<Company> CompanyAsync(string name, DateTimeOffset now, CancellationToken ct)
    {
        var normalized = Normalizer.CompanyName(name);
        if (_companies.TryGetValue(normalized, out var cached))
            return cached;

        var company = await db.Companies.FirstOrDefaultAsync(company => company.NormalizedName == normalized, ct);
        if (company is null)
        {
            company = Company.Create(name, now);
            db.Companies.Add(company);
        }

        _companies[normalized] = company;
        return company;
    }

    private void Remember(JobPosting posting)
    {
        if (posting.NormalizedApplyUrl is not null)
            _byApplyUrl[posting.NormalizedApplyUrl] = posting;

        _byFingerprint[posting.Fingerprint] = posting;
    }

    private static PostingSnapshot ToSnapshot(SourcedPosting item) => new(
        item.Title,
        item.Location,
        item.WorkModel,
        item.Seniority,
        item.EmploymentType,
        item.DescriptionText,
        item.ApplyUrl,
        item.PublishedAt);
}
