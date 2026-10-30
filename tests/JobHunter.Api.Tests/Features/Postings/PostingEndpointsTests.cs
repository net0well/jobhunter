using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Companies;
using JobHunter.Api.Features.Postings.Shared;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Api.Tests.Features.Postings;

[Collection(nameof(ApiCollection))]
public sealed class PostingEndpointsTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private const string Route = "/api/postings";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_UrlRecognizedByConnector_CreatesPostingWithDetailsAndConnectorSource()
    {
        var url = FakeConnector.PostingUrl("acme", "backend");
        factory.Fake.Details[url] = new PostingDetails("backend", "Backend Developer", "<p>.NET</p>", ".NET and PostgreSQL", ["C#"], FakeConnector.ApplyUrl("acme", "backend"), true, new Dictionary<string, string>());
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync($"{Route}/import", new { url, companyName = "Acme" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var posting = (await response.Content.ReadFromJsonAsync<PostingDetail>(Json, Ct))!;
        Assert.Equal("Backend Developer", posting.Title);
        Assert.Equal(".NET and PostgreSQL", posting.DescriptionText);
        Assert.Equal(FakeConnector.Key, Assert.Single(posting.Sources).ConnectorKey);
        Assert.Equal(FakeConnector.Key, posting.ApplyChannelKey);
    }

    [Fact]
    public async Task Import_UnknownUrlWithTitle_CreatesManualPosting()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync($"{Route}/import", new { url = "https://careers.example.com/jobs/42?utm_source=mail", title = "Platform Engineer" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var posting = (await response.Content.ReadFromJsonAsync<PostingDetail>(Json, Ct))!;
        Assert.Equal("Platform Engineer", posting.Title);
        Assert.Equal("careers.example.com", posting.Company);
        Assert.Equal(PostingSource.ManualConnectorKey, Assert.Single(posting.Sources).ConnectorKey);
        Assert.True(posting.ManualApplyOnly);
    }

    [Fact]
    public async Task Import_UnknownUrlWithoutTitle_ReturnsTitleRequired()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync($"{Route}/import", new { url = "https://careers.example.com/jobs/42" }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_SameUrlTwice_ReturnsAlreadyExists()
    {
        using var client = factory.CreateApiClient();
        await client.PostAsJsonAsync($"{Route}/import", new { url = "https://careers.example.com/jobs/42", title = "Platform Engineer" }, Json, Ct);

        var response = await client.PostAsJsonAsync($"{Route}/import", new { url = "https://careers.example.com/jobs/42/", title = "Platform Engineer" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task List_MatchedPostings_ComeByScoreDescendingAndPaged()
    {
        await SeedScoredAsync(("Low", 61), ("High", 95), ("Middle", 80), ("Rejected", 20));
        using var client = factory.CreateApiClient();

        var firstPage = await client.GetFromJsonAsync<PagedResponse<PostingSummary>>($"{Route}?status=Matched&pageSize=2", Json, Ct);
        var secondPage = await client.GetFromJsonAsync<PagedResponse<PostingSummary>>($"{Route}?status=Matched&pageSize=2&page=2", Json, Ct);

        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(["High", "Middle"], firstPage.Items.Select(posting => posting.Title));
        Assert.Equal(["Low"], secondPage!.Items.Select(posting => posting.Title));
    }

    [Fact]
    public async Task List_SearchTerm_MatchesTitleIgnoringAccents()
    {
        await SeedScoredAsync(("Engenheiro de Dados Sênior", 70), ("Designer", 70));
        using var client = factory.CreateApiClient();

        var result = await client.GetFromJsonAsync<PagedResponse<PostingSummary>>($"{Route}?q=senior", Json, Ct);

        Assert.Equal("Engenheiro de Dados Sênior", Assert.Single(result!.Items).Title);
    }

    [Fact]
    public async Task DiscardThenRestore_ChangesStatusAndKeepsManualFlagOnlyWhileDiscarded()
    {
        var ids = await SeedScoredAsync(("Backend", 90));
        using var client = factory.CreateApiClient();

        var discarded = await client.PostAsJsonAsync($"{Route}/{ids[0]}/discard", new { reason = "Not interested" }, Json, Ct);
        var restored = await client.PostAsync($"{Route}/{ids[0]}/restore", null, Ct);

        Assert.Equal(JobPostingStatus.Discarded, (await discarded.Content.ReadFromJsonAsync<PostingDetail>(Json, Ct))!.Status);
        Assert.Equal(JobPostingStatus.Matched, (await restored.Content.ReadFromJsonAsync<PostingDetail>(Json, Ct))!.Status);
    }

    [Fact]
    public async Task Restore_PostingNotDiscarded_ReturnsConflict()
    {
        var ids = await SeedScoredAsync(("Backend", 90));
        using var client = factory.CreateApiClient();

        var response = await client.PostAsync($"{Route}/{ids[0]}/restore", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownPosting_ReturnsNotFound()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_PostingFromFakeConnector_UpdatesDescription()
    {
        var url = FakeConnector.PostingUrl("acme", "backend");
        factory.Fake.Details[url] = new PostingDetails("backend", "Backend Developer", null, "First version", [], null, true, new Dictionary<string, string>());
        using var client = factory.CreateApiClient();
        var created = await (await client.PostAsJsonAsync($"{Route}/import", new { url }, Json, Ct)).Content.ReadFromJsonAsync<PostingDetail>(Json, Ct);
        factory.Fake.Details[url] = new PostingDetails("backend", "Backend Developer", null, "Second version", [], null, true, new Dictionary<string, string>());

        var refreshed = await client.PostAsync($"{Route}/{created!.Id}/refresh", null, Ct);

        Assert.Equal("Second version", (await refreshed.Content.ReadFromJsonAsync<PostingDetail>(Json, Ct))!.DescriptionText);
    }

    [Fact]
    public async Task BlockCompany_MatchedPostings_GoBackToScoring()
    {
        var ids = await SeedScoredAsync(("Backend", 90));
        Guid companyId;
        await using (var scope = factory.OpenDb())
        {
            companyId = (await scope.Db.JobPostings.SingleAsync(Ct)).CompanyId;
        }

        using var client = factory.CreateApiClient();
        var response = await client.PostAsync($"/api/companies/{companyId}/block", null, Ct);

        var result = (await response.Content.ReadFromJsonAsync<SetCompanyBlocked.Response>(Json, Ct))!;
        Assert.True(result.IsBlocked);
        Assert.Equal(1, result.PostingsToRescore);
        var posting = await client.GetFromJsonAsync<PostingDetail>($"{Route}/{ids[0]}", Json, Ct);
        Assert.Equal(JobPostingStatus.New, posting!.Status);
    }

    private async Task<List<Guid>> SeedScoredAsync(params (string Title, int Score)[] postings)
    {
        await using var scope = factory.OpenDb();
        var now = factory.Clock.GetUtcNow();
        var company = Company.Create("Acme", now);
        scope.Db.Companies.Add(company);
        List<Guid> ids = [];

        foreach (var (title, score) in postings)
        {
            var posting = JobPosting.Create(company, new PostingSnapshot(title, "São Paulo, SP", WorkModel.Remote, null, null, "Description", null, null), now);
            posting.ApplyMatch(new MatchResult(score, [], null), Guid.NewGuid(), minScore: 60);
            scope.Db.JobPostings.Add(posting);
            ids.Add(posting.Id);
        }

        await scope.Db.SaveChangesAsync(Ct);
        return ids;
    }
}
