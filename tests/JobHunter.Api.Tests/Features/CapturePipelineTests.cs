using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Features.Matching;
using JobHunter.Api.Features.Postings.Shared;
using JobHunter.Api.Features.Scanning;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Builders;
using JobHunter.Connectors.Testing.Fakes;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Features;

[Collection(nameof(ApiCollection))]
public sealed class CapturePipelineTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SourceToMatchedPosting_RunsThroughScanAndScoring()
    {
        factory.Fake.Tenants["acme"] =
        [
            SourcedPostingBuilder.Default("acme", "dotnet").WithTitle("Desenvolvedor .NET Pleno").WithDescription("C#, ASP.NET Core e PostgreSQL").Build(),
            SourcedPostingBuilder.Default("acme", "intern").WithTitle("Estágio em Desenvolvimento").Build(),
            SourcedPostingBuilder.Default("acme", "java").WithTitle("Desenvolvedor Java").WithDescription("Spring e Kafka").Build(),
        ];
        using var client = factory.CreateApiClient();

        await client.PostAsJsonAsync("/api/sources", new { connectorKey = FakeConnector.Key, kind = "Tenant", input = "https://jobs.fake.test/acme" }, Json, Ct);
        await client.PostAsJsonAsync(
            "/api/search-profiles",
            new { name = ".NET", requiredKeywords = new[] { ".net", "c#" }, preferredKeywords = new[] { "postgresql" }, excludedKeywords = new[] { "estágio" }, workModels = new[] { "Remote" } },
            Json,
            Ct);

        await SendAsync(new ScanDueSources.Command(BatchSize: 20));
        await SendAsync(new ScorePendingPostings.Command(BatchSize: 200));

        var matched = await client.GetFromJsonAsync<PagedResponse<PostingSummary>>("/api/postings?status=Matched", Json, Ct);
        var posting = Assert.Single(matched!.Items);
        Assert.Equal("Desenvolvedor .NET Pleno", posting.Title);
        Assert.Equal(FakeConnector.Key, posting.ApplyChannelKey);
        var discarded = await client.GetFromJsonAsync<PagedResponse<PostingSummary>>("/api/postings?status=Discarded", Json, Ct);
        Assert.Equal(2, discarded!.TotalCount);
    }

    private async Task SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, Ct);
    }
}
