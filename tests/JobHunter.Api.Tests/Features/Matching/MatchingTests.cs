using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Matching;
using JobHunter.Api.Features.Matching.Shared;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions.Sources;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Features.Matching;

[Collection(nameof(ApiCollection))]
public sealed class MatchingTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private const string Route = "/api/search-profiles";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScorePending_ExcludedKeywordInTitle_DiscardsWithReason()
    {
        await CreateProfileAsync(new { name = "Dev", excludedKeywords = new[] { "estágio" } });
        var id = await SeedPostingAsync("Estágio em Desenvolvimento .NET");

        await SendAsync(new ScorePendingPostings.Command(BatchSize: 100));

        var posting = await LoadAsync(id);
        Assert.Equal(JobPostingStatus.Discarded, posting.Status);
        Assert.Equal("ExcludedKeyword:estágio", posting.DiscardReason);
    }

    [Fact]
    public async Task ScorePending_CompatiblePosting_IsMatchedWithReasons()
    {
        await CreateProfileAsync(new
        {
            name = ".NET remoto",
            requiredKeywords = new[] { ".net" },
            preferredKeywords = new[] { "c#", "postgresql" },
            workModels = new[] { "Remote" },
            minScore = 60,
        });
        var id = await SeedPostingAsync("Desenvolvedor .NET Pleno");

        var result = await SendAsync(new ScorePendingPostings.Command(BatchSize: 100));

        var posting = await LoadAsync(id);
        Assert.Equal(1, result.Matched);
        Assert.Equal(JobPostingStatus.Matched, posting.Status);
        Assert.True(posting.MatchScore >= 60);
        Assert.Contains(posting.MatchReasons, reason => reason is { Rule: "Title", Matched: true });
        Assert.Contains(posting.MatchReasons, reason => reason is { Rule: "WorkModel", Matched: true });
    }

    [Fact]
    public async Task ScorePending_NoActiveProfile_LeavesPostingsNew()
    {
        await CreateProfileAsync(new { name = "Paused", isActive = false });
        var id = await SeedPostingAsync("Desenvolvedor .NET");

        var result = await SendAsync(new ScorePendingPostings.Command(BatchSize: 100));

        Assert.Equal(0, result.Scored);
        Assert.Equal(JobPostingStatus.New, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task ScorePending_TwoProfiles_KeepsTheBestOutcome()
    {
        await CreateProfileAsync(new { name = "Java", requiredKeywords = new[] { "java" } });
        var dotnet = await CreateProfileAsync(new { name = ".NET", requiredKeywords = new[] { ".net" } });
        var id = await SeedPostingAsync("Desenvolvedor .NET");

        await SendAsync(new ScorePendingPostings.Command(BatchSize: 100));

        var posting = await LoadAsync(id);
        Assert.Equal(JobPostingStatus.Matched, posting.Status);
        Assert.Equal(dotnet.Id, posting.ScoredWithProfileId);
    }

    [Fact]
    public async Task Rescore_ProfileTightened_DiscardsMatchedButKeepsManualDiscards()
    {
        var profile = await CreateProfileAsync(new { name = "Dev", requiredKeywords = new[] { ".net" } });
        var matched = await SeedPostingAsync("Desenvolvedor .NET");
        var manual = await SeedPostingAsync("Analista .NET");
        await SendAsync(new ScorePendingPostings.Command(BatchSize: 100));
        await using (var scope = factory.OpenDb())
        {
            (await scope.Db.JobPostings.SingleAsync(posting => posting.Id == manual, Ct)).Discard("Not this one");
            await scope.Db.SaveChangesAsync(Ct);
        }

        using var client = factory.CreateApiClient();
        await client.PutAsJsonAsync($"{Route}/{profile.Id}", new { name = "Dev", requiredKeywords = new[] { ".net" }, excludedKeywords = new[] { "desenvolvedor", "analista" } }, Json, Ct);
        var rescore = await client.PostAsync($"{Route}/rescore", null, Ct);

        Assert.Equal(HttpStatusCode.OK, rescore.StatusCode);
        Assert.Equal("ExcludedKeyword:desenvolvedor", (await LoadAsync(matched)).DiscardReason);
        Assert.Equal("Not this one", (await LoadAsync(manual)).DiscardReason);
    }

    [Fact]
    public async Task Create_InvalidMinScore_ReturnsValidationProblem()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(Route, new { name = "Dev", minScore = 150 }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListAndDelete_ProfileLifecycle_Works()
    {
        var profile = await CreateProfileAsync(new { name = "Dev" });
        using var client = factory.CreateApiClient();

        var listed = await client.GetFromJsonAsync<List<SearchProfileResponse>>(Route, Json, Ct);
        var deleted = await client.DeleteAsync($"{Route}/{profile.Id}", Ct);
        var after = await client.GetFromJsonAsync<List<SearchProfileResponse>>(Route, Json, Ct);

        Assert.Equal(SearchProfile.DefaultMinScore, Assert.Single(listed!).MinScore);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(after!);
    }

    private async Task<SearchProfileResponse> CreateProfileAsync(object profile)
    {
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync(Route, profile, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SearchProfileResponse>(Json, Ct))!;
    }

    private async Task<Guid> SeedPostingAsync(string title)
    {
        await using var scope = factory.OpenDb();
        var now = factory.Clock.GetUtcNow();
        var company = await scope.Db.Companies.FirstOrDefaultAsync(Ct) ?? Company.Create("Acme", now);
        if (scope.Db.Entry(company).State == EntityState.Detached)
            scope.Db.Companies.Add(company);

        var posting = JobPosting.Create(company, new PostingSnapshot(title, "Remoto", WorkModel.Remote, null, null, "Stack com C# e PostgreSQL.", null, null), now);
        scope.Db.JobPostings.Add(posting);
        await scope.Db.SaveChangesAsync(Ct);
        return posting.Id;
    }

    private async Task<JobPosting> LoadAsync(Guid id)
    {
        await using var scope = factory.OpenDb();
        return await scope.Db.JobPostings.AsNoTracking().SingleAsync(posting => posting.Id == id, Ct);
    }

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, Ct);
    }
}
