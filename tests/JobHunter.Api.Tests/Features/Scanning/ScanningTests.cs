using System.Net;
using System.Net.Http.Json;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Scanning;
using JobHunter.Api.Features.Scanning.Shared;
using JobHunter.Api.Features.Sources;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Builders;
using JobHunter.Connectors.Testing.Fakes;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Features.Scanning;

[Collection(nameof(ApiCollection))]
public sealed class ScanningTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScanDueSources_DueSourceWithThreePostings_CreatesPostingsSourcesAndRun()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend"), Posting("frontend"), Posting("data")];

        var response = await SendAsync(new ScanDueSources.Command(BatchSize: 50));

        Assert.Equal(1, response.Scanned);
        await using var scope = factory.OpenDb();
        var db = scope.Db;
        Assert.Equal(3, await db.JobPostings.CountAsync(posting => posting.Status == JobPostingStatus.New, Ct));
        Assert.Equal(3, await db.PostingSources.CountAsync(Ct));
        var run = await db.ScanRuns.SingleAsync(Ct);
        Assert.Equal(ScanRunStatus.Succeeded, run.Status);
        Assert.Equal(3, run.Created);
        Assert.Equal(source.Id, run.SourceTargetId);
    }

    [Fact]
    public async Task ScanSource_SameListingTwice_UpdatesWithoutDuplicating()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend"), Posting("frontend")];
        await ScanAsync(source.Id);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var second = await ScanAsync(source.Id);

        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.Updated);
        await using var scope = factory.OpenDb();
        var db = scope.Db;
        Assert.Equal(2, await db.JobPostings.CountAsync(Ct));
        Assert.All(await db.PostingSources.ToListAsync(Ct), item => Assert.Equal(factory.Clock.GetUtcNow(), item.LastSeenAt));
    }

    [Fact]
    public async Task ScanSource_SameApplyUrlFromTwoConnectors_MergesIntoOnePostingAsExact()
    {
        var first = await AddSourceAsync(FakeConnector.Key, "acme");
        var second = await AddSourceAsync(JobHunterApiFactory.SecondFakeKey, "acme");
        var applyUrl = FakeConnector.ApplyUrl("acme", "backend");
        factory.Fake.Tenants["acme"] = [Posting("backend")];
        factory.SecondFake.Tenants["acme"] = [SourcedPostingBuilder.Default("acme", "other-id").WithTitle("Backend Developer (.NET)").WithApplyUrl(new Uri(applyUrl + "?utm_source=board")).Build()];

        await ScanAsync(first.Id);
        var run = await ScanAsync(second.Id);

        Assert.Equal(1, run.Deduplicated);
        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var posting = await db.JobPostings.Include(item => item.Sources).SingleAsync(Ct);
        Assert.Equal(2, posting.Sources.Count);
        Assert.Equal(DedupConfidence.Exact, posting.DedupConfidence);
    }

    [Fact]
    public async Task ScanSource_SameCompanyTitleAndCityWithoutCommonUrl_MergesAsProbable()
    {
        var first = await AddSourceAsync(FakeConnector.Key, "acme");
        var second = await AddSourceAsync(JobHunterApiFactory.SecondFakeKey, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend")];
        factory.SecondFake.Tenants["acme"] = [SourcedPostingBuilder.Default("acme", "elsewhere").WithTitle("Developer backend").WithApplyUrl(null).WithLocation("Sao Paulo - SP").Build()];

        await ScanAsync(first.Id);
        await ScanAsync(second.Id);

        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var posting = await db.JobPostings.SingleAsync(Ct);
        Assert.Equal(DedupConfidence.Probable, posting.DedupConfidence);
    }

    [Fact]
    public async Task ScanSource_PostingMissingFromEverySource_ClosesAfterConfiguredScans()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [Posting("stays"), Posting("leaves")];
        await ScanAsync(source.Id);
        factory.Fake.Tenants["acme"] = [Posting("stays")];

        var firstMiss = await ScanAsync(source.Id);
        var secondMiss = await ScanAsync(source.Id);

        Assert.Equal(0, firstMiss.Closed);
        Assert.Equal(1, secondMiss.Closed);
        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var statuses = await db.JobPostings.Include(posting => posting.Sources).ToDictionaryAsync(posting => posting.Sources.Single().ExternalId, posting => posting.Status, Ct);
        Assert.Equal(JobPostingStatus.Closed, statuses["leaves"]);
        Assert.Equal(JobPostingStatus.New, statuses["stays"]);
    }

    [Fact]
    public async Task ScanSource_PostingStillListedByAnotherSource_StaysOpen()
    {
        var first = await AddSourceAsync(FakeConnector.Key, "acme");
        var second = await AddSourceAsync(JobHunterApiFactory.SecondFakeKey, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend")];
        factory.SecondFake.Tenants["acme"] = [SourcedPostingBuilder.Default("acme", "mirror").WithApplyUrl(FakeConnector.ApplyUrl("acme", "backend")).Build()];
        await ScanAsync(first.Id);
        await ScanAsync(second.Id);
        factory.Fake.Tenants["acme"] = [];

        await ScanAsync(first.Id);
        await ScanAsync(first.Id);

        await using var scope = factory.OpenDb();
        var db = scope.Db;
        Assert.NotEqual(JobPostingStatus.Closed, (await db.JobPostings.SingleAsync(Ct)).Status);
    }

    [Fact]
    public async Task ScanSource_ClosedPostingReappears_IsReopenedAsNew()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend")];
        await ScanAsync(source.Id);
        factory.Fake.Tenants["acme"] = [];
        await ScanAsync(source.Id);
        await ScanAsync(source.Id);
        factory.Fake.Tenants["acme"] = [Posting("backend")];

        await ScanAsync(source.Id);

        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var posting = await db.JobPostings.SingleAsync(Ct);
        Assert.Equal(JobPostingStatus.New, posting.Status);
        Assert.Null(posting.ClosedAt);
    }

    [Fact]
    public async Task ScanSource_ApplyUrlHandledByFakeChannel_AssignsChannelOtherwiseManual()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] =
        [
            Posting("own"),
            SourcedPostingBuilder.Default("acme", "foreign").WithTitle("Data Engineer").WithApplyUrl(SampleUrls.Postings["gupy"]).Build(),
            SourcedPostingBuilder.Default("acme", "none").WithTitle("QA Analyst").WithApplyUrl(null).Build(),
        ];

        await ScanAsync(source.Id);

        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var channels = await db.JobPostings.Include(posting => posting.Sources).ToDictionaryAsync(posting => posting.Sources.Single().ExternalId, posting => posting.ApplyChannelKey, Ct);
        Assert.Equal(FakeConnector.Key, channels["own"]);
        Assert.Null(channels["foreign"]);
        Assert.Null(channels["none"]);
    }

    [Fact]
    public async Task ScanSource_PostingWithoutDescription_FetchesDetails()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        var item = SourcedPostingBuilder.Default("acme", "backend").WithDescription(null).Build();
        factory.Fake.Tenants["acme"] = [item];
        factory.Fake.Details[item.SourceUrl] = new PostingDetails("backend", item.Title, "<p>C# and PostgreSQL</p>", "C# and PostgreSQL", ["C#"], null, true, new Dictionary<string, string>());

        var run = await ScanAsync(source.Id);

        Assert.Equal(ScanRunStatus.Succeeded, run.Status);
        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var posting = await db.JobPostings.SingleAsync(Ct);
        Assert.Equal("C# and PostgreSQL", posting.DescriptionText);
        Assert.Equal(["C#"], posting.Requirements);
    }

    [Fact]
    public async Task ScanSource_DetailsUnavailable_KeepsPostingAndMarksRunPartial()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [SourcedPostingBuilder.Default("acme", "backend").WithDescription(null).Build()];

        var run = await ScanAsync(source.Id);

        Assert.Equal(ScanRunStatus.PartiallySucceeded, run.Status);
        Assert.Equal(1, run.Created);
    }

    [Fact]
    public async Task ScanDueSources_LayoutChanged_FailsRunPausesConnectorAndKeepsOthersGoing()
    {
        await AddSourceAsync(FakeConnector.Key, "acme");
        await AddSourceAsync(JobHunterApiFactory.SecondFakeKey, "globex");
        factory.Fake.FailNext(new ConnectorError(ConnectorErrorKind.LayoutChanged, "Fake.Layout", "Listing markup changed", EvidencePath: "Artifacts/fake/listing.html"));
        factory.SecondFake.Tenants["globex"] = [Posting("backend", "globex")];

        await SendAsync(new ScanDueSources.Command(BatchSize: 50));

        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var runs = await db.ScanRuns.ToDictionaryAsync(run => run.ConnectorKey, Ct);
        Assert.Equal(ScanRunStatus.Failed, runs[FakeConnector.Key].Status);
        Assert.Equal("Artifacts/fake/listing.html", runs[FakeConnector.Key].EvidencePath);
        Assert.Equal(ScanRunStatus.Succeeded, runs[JobHunterApiFactory.SecondFakeKey].Status);
        var state = await db.ConnectorStates.SingleAsync(item => item.ConnectorKey == FakeConnector.Key, Ct);
        Assert.True(state.IsPaused(factory.Clock.GetUtcNow()));
    }

    [Fact]
    public async Task ScanDueSources_PausedConnector_IsSkipped()
    {
        await AddSourceAsync(FakeConnector.Key, "acme");
        await using (var scope = factory.OpenDb())
        {
            var db = scope.Db;
            var state = new ConnectorState(FakeConnector.Key, DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime));
            state.Pause(factory.Clock.GetUtcNow().AddHours(1), "Captcha detected", factory.Clock.GetUtcNow());
            db.ConnectorStates.Add(state);
            await db.SaveChangesAsync(Ct);
        }

        var response = await SendAsync(new ScanDueSources.Command(BatchSize: 50));

        Assert.Equal(0, response.Scanned);
    }

    [Fact]
    public async Task ScanSource_RepeatedFailures_DeactivatesSource()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            factory.Fake.FailNext(new ConnectorError(ConnectorErrorKind.NotFound, "Fake.TenantNotFound", "Tenant does not exist"));
            await ScanAsync(source.Id);
        }

        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var stored = await db.SourceTargets.SingleAsync(Ct);
        Assert.False(stored.IsActive);
        Assert.Equal(5, stored.ConsecutiveFailures);
        Assert.Equal("Fake.TenantNotFound", stored.LastErrorCode);
    }

    [Fact]
    public async Task TriggerScan_ActiveSource_QueuesRunThatCompletesWhenProcessed()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend")];
        using var client = factory.CreateApiClient();

        var response = await client.PostAsync($"/api/sources/{source.Id}/scan", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<TriggerSourceScan.Response>(Ct))!;
        Assert.True(factory.Services.GetRequiredService<ScanRequestQueue>().TryDequeue(out var request));
        Assert.Equal(accepted.ScanRunId, request!.ScanRunId);

        var run = await ScanAsync(source.Id, request.ScanRunId);

        Assert.Equal(accepted.ScanRunId, run.Id);
        Assert.Equal(ScanRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public async Task TriggerScan_PausedConnector_ReturnsServiceUnavailable()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        await using (var scope = factory.OpenDb())
        {
            var db = scope.Db;
            var state = new ConnectorState(FakeConnector.Key, DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime));
            state.Pause(factory.Clock.GetUtcNow().AddHours(1), "Captcha detected", factory.Clock.GetUtcNow());
            db.ConnectorStates.Add(state);
            await db.SaveChangesAsync(Ct);
        }

        using var client = factory.CreateApiClient();
        var response = await client.PostAsync($"/api/sources/{source.Id}/scan", null, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task TriggerScan_WhileTheSourceIsBeingScanned_ReturnsConflict()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        using var client = factory.CreateApiClient();
        await client.PostAsync($"/api/sources/{source.Id}/scan", null, Ct);

        var second = await client.PostAsync($"/api/sources/{source.Id}/scan", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ScanDueSources_SourceAlreadyBeingScanned_IsSkippedUntilTheRunIsStale()
    {
        var source = await AddSourceAsync(FakeConnector.Key, "acme");
        factory.Fake.Tenants["acme"] = [Posting("backend")];
        await using (var scope = factory.OpenDb())
        {
            scope.Db.ScanRuns.Add(ScanRun.Start(source, factory.Clock.GetUtcNow()));
            await scope.Db.SaveChangesAsync(Ct);
        }

        var whileRunning = await SendAsync(new ScanDueSources.Command(BatchSize: 50));
        factory.Clock.Advance(RunningScans.StaleAfter + TimeSpan.FromMinutes(1));
        var afterStale = await SendAsync(new ScanDueSources.Command(BatchSize: 50));

        Assert.Equal(0, whileRunning.Scanned);
        Assert.Equal(1, afterStale.Scanned);
    }

    private static SourcedPosting Posting(string externalId, string tenant = "acme") =>
        SourcedPostingBuilder.Default(tenant, externalId).WithTitle($"Developer {externalId}").Build();

    private async Task<SourceTarget> AddSourceAsync(string connectorKey, string tenant)
    {
        await using var scope = factory.OpenDb();
        var db = scope.Db;
        var source = SourceTarget.Create(connectorKey, SourceKind.Tenant, tenant, null, tenant, null, factory.Clock.GetUtcNow());
        db.SourceTargets.Add(source);
        await db.SaveChangesAsync(Ct);
        return source;
    }

    private async Task<ScanRunResponse> ScanAsync(Guid sourceId, Guid? runId = null)
    {
        var result = await SendAsync(new ScanSource.Command(sourceId, runId));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, Ct);
    }
}
