using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using JobHunter.Connectors.Abstractions;
using JobHunter.Connectors.Abstractions.Applications;
using JobHunter.Connectors.Abstractions.Health;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;

namespace JobHunter.Connectors.Testing.Fakes;

public sealed partial class FakeConnector(TimeProvider clock, string key = FakeConnector.Key) :
    IJobConnector,
    ITenantJobSource,
    ISearchJobSource,
    IFeedJobSource,
    IJobDetailsProvider,
    ITenantDiscovery,
    IApplicationChannel,
    IAssistedApplicationChannel,
    IConnectorHealthProbe
{
    public const string Key = "fake";

    private const ConnectorCapabilities AllCapabilities =
        ConnectorCapabilities.TenantListing | ConnectorCapabilities.Search | ConnectorCapabilities.Feed |
        ConnectorCapabilities.PostingDetails | ConnectorCapabilities.ApplicationInspect | ConnectorCapabilities.ApplicationSubmit |
        ConnectorCapabilities.AssistedApply | ConnectorCapabilities.TenantDiscovery | ConnectorCapabilities.HealthProbe;

    private readonly ConcurrentQueue<object> _scripted = new();

    public ConnectorDescriptor Descriptor { get; } = new(
        key,
        key == Key ? "Fake" : $"Fake {key}",
        AllCapabilities,
        new ConnectorTraits(
            RequiresAuthentication: false,
            UsesBrowser: false,
            DefaultScanInterval: TimeSpan.FromHours(1),
            DefaultMaxConcurrency: 1,
            DefaultPolitenessDelayMin: TimeSpan.Zero,
            DefaultPolitenessDelayMax: TimeSpan.Zero));

    public ConcurrentDictionary<string, IReadOnlyList<SourcedPosting>> Tenants { get; } = new();

    public ConcurrentDictionary<Uri, PostingDetails> Details { get; } = new();

    public ConcurrentDictionary<Uri, ApplicationForm> Forms { get; } = new();

    public IReadOnlyList<SourcedPosting> SearchResults { get; set; } = [];

    public IReadOnlyList<SourcedPosting> FeedItems { get; set; } = [];

    public IReadOnlyList<DiscoveredTenant> DiscoveredTenants { get; set; } = [];

    public ConnectorResult<SubmissionReceipt>? NextSubmitResult { get; set; }

    public ConcurrentQueue<ApplicationSubmission> Submissions { get; } = new();

    public ConcurrentQueue<ApplicationSubmission> AssistedSessions { get; } = new();

    public Uri OwnBaseUrl { get; } = BaseUrlFor(key);

    public static Uri BaseUrlFor(string key) => new($"https://jobs.{key}.test/");

    public static Uri TenantUrl(string tenant, string key = Key) => new(BaseUrlFor(key), tenant);

    public static Uri PostingUrl(string tenant, string externalId, string key = Key) => new(BaseUrlFor(key), $"{tenant}/{externalId}");

    public static Uri ApplyUrl(string tenant, string externalId, string key = Key) => new(BaseUrlFor(key), $"{tenant}/{externalId}/apply");

    public void FailNext(ConnectorError error) => _scripted.Enqueue(error);

    public void ThrowNext(Exception exception) => _scripted.Enqueue(exception);

    public void Reset()
    {
        _scripted.Clear();
        Tenants.Clear();
        Details.Clear();
        Forms.Clear();
        Submissions.Clear();
        AssistedSessions.Clear();
        SearchResults = [];
        FeedItems = [];
        DiscoveredTenants = [];
        NextSubmitResult = null;
    }

    public ConnectorResult<TenantRef> ParseTenant(string input)
    {
        var candidate = input?.Trim() ?? string.Empty;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var url))
        {
            if (!CanHandle(url) || url.Segments.Length != 2)
                return InvalidTenant(input);

            candidate = url.Segments[1].TrimEnd('/');
        }

        return TenantSlug().IsMatch(candidate)
            ? ConnectorResult<TenantRef>.Ok(new TenantRef(candidate))
            : InvalidTenant(input);
    }

    public Uri GetTenantUrl(TenantRef tenant) => new(OwnBaseUrl, tenant.Value);

    public bool CanHandle(Uri postingUrl) =>
        postingUrl.IsAbsoluteUri && string.Equals(postingUrl.Host, OwnBaseUrl.Host, StringComparison.OrdinalIgnoreCase);

    public Task<ConnectorResult<IReadOnlyList<SourcedPosting>>> ListAsync(TenantRef tenant, CancellationToken ct) =>
        Respond(() => Tenants.TryGetValue(tenant.Value, out var postings)
            ? ConnectorResult<IReadOnlyList<SourcedPosting>>.Ok(postings)
            : ConnectorResult<IReadOnlyList<SourcedPosting>>.Fail(new(ConnectorErrorKind.NotFound, "Fake.TenantNotFound", $"Tenant '{tenant.Value}' does not exist")));

    public Task<ConnectorResult<SearchPage>> SearchAsync(SearchCriteria criteria, string? cursor, CancellationToken ct) =>
        Respond(() => ConnectorResult<SearchPage>.Ok(new SearchPage([.. SearchResults.Take(criteria.MaxResults)], null)));

    public Task<ConnectorResult<FeedBatch>> PullAsync(string? checkpoint, CancellationToken ct) =>
        Respond(() => ConnectorResult<FeedBatch>.Ok(new FeedBatch(FeedItems, null)));

    public Task<ConnectorResult<PostingDetails>> GetDetailsAsync(Uri postingUrl, CancellationToken ct) =>
        Respond(() => Details.TryGetValue(postingUrl, out var details)
            ? details.IsOpen
                ? ConnectorResult<PostingDetails>.Ok(details)
                : ConnectorResult<PostingDetails>.Fail(new(ConnectorErrorKind.PostingClosed, "Fake.PostingClosed", "Posting is closed"))
            : ConnectorResult<PostingDetails>.Fail(NotFound(postingUrl)));

    public Task<ConnectorResult<IReadOnlyList<DiscoveredTenant>>> DiscoverAsync(string query, CancellationToken ct) =>
        Respond(() => ConnectorResult<IReadOnlyList<DiscoveredTenant>>.Ok(DiscoveredTenants));

    public Task<ConnectorResult<ApplicationForm>> InspectAsync(Uri applyUrl, CancellationToken ct) =>
        Respond(() => FindForm(applyUrl, out var form)
            ? ConnectorResult<ApplicationForm>.Ok(form!)
            : ConnectorResult<ApplicationForm>.Fail(UnknownForm(applyUrl)));

    public Task<ConnectorResult<SubmissionReceipt>> SubmitAsync(ApplicationSubmission submission, CancellationToken ct) =>
        Respond(() =>
        {
            if (!FindForm(submission.ApplyUrl, out var form))
                return ConnectorResult<SubmissionReceipt>.Fail(UnknownForm(submission.ApplyUrl));

            if (form!.FormHash != submission.FormHash)
                return ConnectorResult<SubmissionReceipt>.Fail(new(ConnectorErrorKind.FormChanged, "Fake.FormChanged", "Form changed since inspection"));

            var result = NextSubmitResult ?? ConnectorResult<SubmissionReceipt>.Ok(
                new SubmissionReceipt(clock.GetUtcNow(), "Application received", Guid.NewGuid().ToString("N"), null));
            NextSubmitResult = null;

            if (result.IsSuccess)
                Submissions.Enqueue(submission);

            return result;
        });

    public Task<ConnectorResult<bool>> OpenAssistedAsync(ApplicationSubmission submission, CancellationToken ct) =>
        Respond(() =>
        {
            AssistedSessions.Enqueue(submission);
            return ConnectorResult<bool>.Ok(true);
        });

    public Task<ConnectorResult<bool>> ProbeAsync(CancellationToken ct) =>
        Respond(() => ConnectorResult<bool>.Ok(true));

    private Task<ConnectorResult<T>> Respond<T>(Func<ConnectorResult<T>> respond)
    {
        if (_scripted.TryDequeue(out var scripted))
        {
            return scripted is Exception exception
                ? Task.FromException<ConnectorResult<T>>(exception)
                : Task.FromResult(ConnectorResult<T>.Fail((ConnectorError)scripted));
        }

        return Task.FromResult(respond());
    }

    private bool FindForm(Uri applyUrl, out ApplicationForm? form)
    {
        form = null;
        return CanHandle(applyUrl) && Forms.TryGetValue(applyUrl, out form);
    }

    private ConnectorError UnknownForm(Uri applyUrl) =>
        CanHandle(applyUrl)
            ? NotFound(applyUrl)
            : new ConnectorError(ConnectorErrorKind.InvalidInput, "Fake.UnsupportedUrl", $"'{applyUrl}' is not a fake URL");

    private static ConnectorError NotFound(Uri url) =>
        new(ConnectorErrorKind.NotFound, "Fake.NotFound", $"Nothing registered for '{url}'");

    private static ConnectorResult<TenantRef> InvalidTenant(string? input) =>
        ConnectorResult<TenantRef>.Fail(new(ConnectorErrorKind.InvalidInput, "Fake.InvalidTenant", $"'{input}' is not a fake tenant"));

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex TenantSlug();
}
