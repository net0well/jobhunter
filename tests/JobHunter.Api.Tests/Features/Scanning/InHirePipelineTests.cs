using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Scanning;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.InHire;
using JobHunter.Connectors.Testing.Http;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Features.Scanning;

[Collection(nameof(ApiCollection))]
public sealed class InHirePipelineTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private const string Tenant = "eyxo";
    private const string DetailedJobId = "5f8b856e-f4a1-486c-94c2-385c18e483e7";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ScanDueSources_InHireSource_StoresPostingsWithDetailsCompanyAndChannel()
    {
        MapEyxo();
        await using (var scope = factory.OpenDb())
        {
            scope.Db.SourceTargets.Add(SourceTarget.Create(InHireConnectorRegistration.Key, SourceKind.Tenant, Tenant, null, "Eyxo", null, factory.Clock.GetUtcNow()));
            await scope.Db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ScanDueSources.Command(BatchSize: 10), Ct);
        }

        await using var db = factory.OpenDb();
        var run = await db.Db.ScanRuns.SingleAsync(Ct);
        var postings = await db.Db.JobPostings.Include(posting => posting.Company).Include(posting => posting.Sources).ToListAsync(Ct);
        var detailed = postings.Single(posting => posting.Sources.Single().ExternalId == DetailedJobId);

        Assert.Equal(6, postings.Count);
        Assert.Equal(6, run.Created);
        Assert.All(postings, posting =>
        {
            Assert.Equal("Eyxo Content Co.", posting.Company!.Name);
            Assert.Equal(InHireConnectorRegistration.Key, posting.ApplyChannelKey);
        });
        Assert.Contains("Planejar campanhas", detailed.DescriptionText, StringComparison.Ordinal);
        Assert.All(postings, posting => Assert.True(posting.HasDescription));
        Assert.Equal(ScanRunStatus.Succeeded, run.Status);
        Assert.DoesNotContain(factory.InHireHttp.Requests, request => request.Uri.Host != "api.inhire.app");
    }

    // There is one real detail fixture; every listed job gets a copy of it under its own id.
    private void MapEyxo()
    {
        var http = factory.InHireHttp;
        var tenantHeader = FixtureHttpHandler.WithHeader("X-Tenant", Tenant);
        var detail = File.ReadAllText(Fixture("job-eyxo-5f8b856e.json"));

        http.RespondWithFile(Api($"tenants/public/config/resolve/{Tenant}"), Fixture("tenant-eyxo.json"));
        http.RespondWithFile(Api("job-posts/public/pages"), Fixture("listing-eyxo-20261030.json"), tenantHeader);

        foreach (var jobId in ListedJobIds())
        {
            http.Respond(Api($"job-posts/public/pages/{jobId}"), System.Net.HttpStatusCode.OK, detail.Replace(DetailedJobId, jobId, StringComparison.Ordinal), when: tenantHeader);
        }
    }

    private static IEnumerable<string> ListedJobIds()
    {
        using var listing = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Fixture("listing-eyxo-20261030.json")));
        return [.. listing.RootElement.GetProperty("jobsPage").EnumerateArray().Select(job => job.GetProperty("jobId").GetString()!)];
    }

    private static string Api(string path) => $"https://api.inhire.app/{path}";

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "InHire", name);
}
