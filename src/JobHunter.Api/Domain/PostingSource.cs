namespace JobHunter.Api.Domain;

internal sealed class PostingSource
{
    // Postings imported by URL that no connector recognizes.
    public const string ManualConnectorKey = "manual";

    private PostingSource(
        Guid id,
        Guid jobPostingId,
        Guid? sourceTargetId,
        string connectorKey,
        string externalId,
        string sourceUrl,
        DateTimeOffset firstSeenAt,
        DateTimeOffset lastSeenAt)
    {
        Id = id;
        JobPostingId = jobPostingId;
        SourceTargetId = sourceTargetId;
        ConnectorKey = connectorKey;
        ExternalId = externalId;
        SourceUrl = sourceUrl;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = lastSeenAt;
    }

    public Guid Id { get; private set; }

    public Guid JobPostingId { get; private set; }

    public Guid? SourceTargetId { get; private set; }

    public string ConnectorKey { get; private set; }

    public string ExternalId { get; private set; }

    public string SourceUrl { get; private set; }

    public Dictionary<string, string> Extra { get; private set; } = [];

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public int MissedScans { get; private set; }

    public static PostingSource Create(
        Guid jobPostingId,
        Guid? sourceTargetId,
        string connectorKey,
        string externalId,
        Uri sourceUrl,
        IReadOnlyDictionary<string, string> extra,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), jobPostingId, sourceTargetId, connectorKey, externalId, sourceUrl.ToString(), now, now)
        {
            Extra = new Dictionary<string, string>(extra),
        };

    public void Seen(Uri sourceUrl, IReadOnlyDictionary<string, string> extra, DateTimeOffset now)
    {
        SourceUrl = sourceUrl.ToString();
        Extra = new Dictionary<string, string>(extra);
        LastSeenAt = now;
        MissedScans = 0;
    }
}
