using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;
using JobHunter.Api.Domain.Enums;
using JobHunter.Api.Features.Sources;
using JobHunter.Api.Features.Sources.Shared;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Features.Sources;

[Collection(nameof(ApiCollection))]
public sealed class SourceEndpointsTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private const string Route = "/api/sources";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Post_TenantAcceptedByConnector_CreatesSourceDueNow()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(Route, new { connectorKey = FakeConnector.Key, kind = "Tenant", input = "acme" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var source = (await response.Content.ReadFromJsonAsync<SourceResponse>(Json, Ct))!;
        Assert.Equal("acme", source.TenantRef);
        Assert.Equal("acme", source.DisplayName);
        Assert.True(source.IsActive);
        Assert.Equal(factory.Clock.GetUtcNow(), source.NextScanAt);
        Assert.Equal($"/api/sources/{source.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_SameTenantAsUrlAfterId_ReturnsDuplicate()
    {
        using var client = factory.CreateApiClient();
        await client.PostAsJsonAsync(Route, new { connectorKey = FakeConnector.Key, kind = "Tenant", input = "acme" }, Json, Ct);

        var response = await client.PostAsJsonAsync(Route, new { connectorKey = FakeConnector.Key, kind = "Tenant", input = "https://jobs.fake.test/acme" }, Json, Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Source.Duplicate");
    }

    [Fact]
    public async Task Post_InputRejectedByConnector_ReturnsInvalidInput()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(Route, new { connectorKey = FakeConnector.Key, kind = "Tenant", input = "Not A Tenant!" }, Json, Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Source.InvalidInput");
    }

    [Fact]
    public async Task Post_ConnectorWithoutTenantCapability_ReturnsCapabilityMissing()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(Route, new { connectorKey = JobHunterApiFactory.SearchOnlyKey, kind = "Tenant", input = "acme" }, Json, Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Connector.CapabilityMissing");
    }

    [Fact]
    public async Task Post_UnknownConnector_ReturnsNotFound()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(Route, new { connectorKey = "gupy", kind = "Tenant", input = "acme" }, Json, Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Connector.NotFound");
    }

    [Fact]
    public async Task Post_SearchWithoutCriteria_ReturnsValidationProblem()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(Route, new { connectorKey = JobHunterApiFactory.SearchOnlyKey, kind = "Search" }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_SearchWithCriteria_CreatesSearchSource()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            Route,
            new { connectorKey = JobHunterApiFactory.SearchOnlyKey, kind = "Search", searchCriteria = new { keywords = new[] { ".NET", "C#" }, workModels = new[] { "Remote" } } },
            Json,
            Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var source = (await response.Content.ReadFromJsonAsync<SourceResponse>(Json, Ct))!;
        Assert.Equal(SourceKind.Search, source.Kind);
        Assert.Equal("Search: .NET, C#", source.DisplayName);
    }

    [Fact]
    public async Task Import_MixedInputs_ReportsCreatedDuplicatedAndInvalid()
    {
        using var client = factory.CreateApiClient();
        await client.PostAsJsonAsync(Route, new { connectorKey = FakeConnector.Key, kind = "Tenant", input = "acme" }, Json, Ct);

        var response = await client.PostAsJsonAsync(
            $"{Route}/import",
            new { connectorKey = FakeConnector.Key, inputs = new[] { "globex", "https://jobs.fake.test/acme", "???" } },
            Json,
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<ImportSources.Response>(Json, Ct))!;
        Assert.Equal("globex", Assert.Single(result.Created).TenantRef);
        Assert.Equal("https://jobs.fake.test/acme", Assert.Single(result.Duplicates));
        Assert.Equal("???", Assert.Single(result.Invalid).Input);
    }

    [Fact]
    public async Task Import_SameTenantTwiceInBatch_CreatesItOnce()
    {
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync($"{Route}/import", new { connectorKey = FakeConnector.Key, inputs = new[] { "acme", "acme" } }, Json, Ct);

        var result = (await response.Content.ReadFromJsonAsync<ImportSources.Response>(Json, Ct))!;
        Assert.Single(result.Created);
        Assert.Single(result.Duplicates);
    }

    [Fact]
    public async Task List_SourceOfDisabledConnector_IsFlaggedAsConnectorDisabled()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SourceTargets.Add(SourceTarget.Create("gupy", SourceKind.Tenant, "acme", null, "Acme on Gupy", null, factory.Clock.GetUtcNow()));
            await db.SaveChangesAsync(Ct);
        }

        using var client = factory.CreateApiClient();
        var sources = await client.GetFromJsonAsync<List<SourceResponse>>(Route, Json, Ct);

        Assert.False(Assert.Single(sources!).ConnectorEnabled);
    }

    [Fact]
    public async Task Put_Deactivate_StopsSourceAndGetShowsIt()
    {
        using var client = factory.CreateApiClient();
        var created = await CreateAsync(client, "acme");

        var update = await client.PutAsJsonAsync($"{Route}/{created.Id}", new { isActive = false, displayName = "Acme Corp" }, Json, Ct);
        var detail = await client.GetFromJsonAsync<GetSource.Response>($"{Route}/{created.Id}", Json, Ct);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.False(detail!.Source.IsActive);
        Assert.Equal("Acme Corp", detail.Source.DisplayName);
        Assert.Empty(detail.RecentRuns);
    }

    [Fact]
    public async Task Delete_ExistingSource_RemovesIt()
    {
        using var client = factory.CreateApiClient();
        var created = await CreateAsync(client, "acme");

        var delete = await client.DeleteAsync($"{Route}/{created.Id}", Ct);
        var get = await client.GetAsync($"{Route}/{created.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SourceResponse> CreateAsync(HttpClient client, string tenant)
    {
        var response = await client.PostAsJsonAsync(Route, new { connectorKey = FakeConnector.Key, kind = "Tenant", input = tenant }, Json, Ct);
        return (await response.Content.ReadFromJsonAsync<SourceResponse>(Json, Ct))!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, Ct);
        Assert.Equal(code, problem!.Extensions["code"]?.ToString());
    }
}
