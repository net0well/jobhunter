using System.Net;
using System.Net.Http.Json;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;
using JobHunter.Api.Features.Connectors;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Features.Connectors;

[Collection(nameof(ApiCollection))]
public sealed class ListConnectorsTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private const string Route = "/api/connectors";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Get_WithoutApiKey_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_FakeConnectorRegistered_ListsItsCapabilitiesAndIdleState()
    {
        var connector = await GetFakeAsync();

        Assert.Equal("Fake", connector.DisplayName);
        Assert.True(connector.Enabled);
        Assert.False(connector.Paused);
        Assert.Equal("Closed", connector.CircuitState);
        Assert.Equal(0, connector.SubmissionsToday);
        Assert.Equal(
            ["TenantListing", "Search", "Feed", "PostingDetails", "ApplicationInspect", "ApplicationSubmit", "AssistedApply", "TenantDiscovery", "HealthProbe"],
            connector.Capabilities);
    }

    [Fact]
    public async Task Get_ConnectorPausedWithSubmissionsToday_ReportsPersistedState()
    {
        var now = factory.Clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
        await SaveStateAsync(state =>
        {
            state.Pause(now.AddHours(2), "Captcha detected", now);
            state.RegisterSubmission(today, dailyLimit: 5);
        }, today);

        var connector = await GetFakeAsync();

        Assert.True(connector.Paused);
        Assert.Equal(now.AddHours(2), connector.PausedUntil);
        Assert.Equal("Captcha detected", connector.PauseReason);
        Assert.Equal(1, connector.SubmissionsToday);
    }

    private async Task<ListConnectors.Response> GetFakeAsync()
    {
        using var client = factory.CreateApiClient();

        var connectors = await client.GetFromJsonAsync<List<ListConnectors.Response>>(Route, TestContext.Current.CancellationToken);

        return Assert.Single(connectors!, connector => connector.Key == FakeConnector.Key);
    }

    private async Task SaveStateAsync(Action<ConnectorState> change, DateOnly today)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = new ConnectorState(FakeConnector.Key, today);
        change(state);
        db.ConnectorStates.Add(state);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
