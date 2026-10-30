using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace JobHunter.Api.Tests.Connectors;

public sealed class ConnectorRateLimiterTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AcquireAsync_MaxConcurrencyOne_QueuesSecondCallUntilFirstReleases()
    {
        await using var services = StubConnectors("alpha");
        var limiter = services.GetRequiredService<ConnectorRateLimiter>();
        var ct = TestContext.Current.CancellationToken;

        var first = await limiter.AcquireAsync("alpha", ct);
        var second = limiter.AcquireAsync("alpha", ct).AsTask();

        Assert.True(first.IsAcquired);
        Assert.False(second.IsCompleted);

        first.Dispose();
        using var secondLease = await second.WaitAsync(Generous, ct);
        Assert.True(secondLease.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_DifferentConnectors_DoNotBlockEachOther()
    {
        await using var services = StubConnectors("alpha", "beta");
        var limiter = services.GetRequiredService<ConnectorRateLimiter>();
        var ct = TestContext.Current.CancellationToken;

        using var alpha = await limiter.AcquireAsync("alpha", ct);
        using var beta = await limiter.AcquireAsync("beta", ct).AsTask().WaitAsync(Generous, ct);

        Assert.True(alpha.IsAcquired);
        Assert.True(beta.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_RequestsPerMinuteExhausted_QueuesInsteadOfRunning()
    {
        await using var services = StubConnectors(new Dictionary<string, string?>
        {
            ["Connectors:alpha:MaxConcurrency"] = "5",
            ["Connectors:alpha:RequestsPerMinute"] = "2",
        }, "alpha");
        var limiter = services.GetRequiredService<ConnectorRateLimiter>();
        var ct = TestContext.Current.CancellationToken;

        using var first = await limiter.AcquireAsync("alpha", ct);
        using var second = await limiter.AcquireAsync("alpha", ct);
        var third = limiter.AcquireAsync("alpha", ct).AsTask();

        Assert.True(first.IsAcquired && second.IsAcquired);
        Assert.False(third.IsCompleted);
    }

    [Fact]
    public async Task PolitenessDelayAsync_FirstCall_DoesNotWait()
    {
        await using var services = SpacedConnector(new FakeTimeProvider());

        var delay = services.GetRequiredService<ConnectorRateLimiter>().PolitenessDelayAsync("alpha", ConnectorOperationKind.Submit, TestContext.Current.CancellationToken);

        Assert.True(delay.IsCompleted);
    }

    [Fact]
    public async Task PolitenessDelayAsync_SecondCallOfSameKind_WaitsForTheSpacing()
    {
        var clock = new FakeTimeProvider();
        await using var services = SpacedConnector(clock);
        var limiter = services.GetRequiredService<ConnectorRateLimiter>();
        var ct = TestContext.Current.CancellationToken;

        await limiter.PolitenessDelayAsync("alpha", ConnectorOperationKind.Read, ct);
        var second = limiter.PolitenessDelayAsync("alpha", ConnectorOperationKind.Read, ct);
        clock.Advance(TimeSpan.FromSeconds(9));
        var completedEarly = second.IsCompleted;
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.False(completedEarly);
        await second.WaitAsync(Generous, ct);
    }

    [Fact]
    public async Task PolitenessDelayAsync_ReadThenSubmit_SpacesEachKindIndependently()
    {
        await using var services = SpacedConnector(new FakeTimeProvider());
        var limiter = services.GetRequiredService<ConnectorRateLimiter>();
        var ct = TestContext.Current.CancellationToken;

        await limiter.PolitenessDelayAsync("alpha", ConnectorOperationKind.Read, ct);
        var submit = limiter.PolitenessDelayAsync("alpha", ConnectorOperationKind.Submit, ct);

        Assert.True(submit.IsCompleted);
    }

    private static ServiceProvider SpacedConnector(FakeTimeProvider clock) =>
        StubConnectors(
            new Dictionary<string, string?>
            {
                ["Connectors:alpha:PolitenessDelayMin"] = "00:00:10",
                ["Connectors:alpha:PolitenessDelayMax"] = "00:00:10",
                ["Connectors:alpha:SubmissionDelayMin"] = "00:00:10",
                ["Connectors:alpha:SubmissionDelayMax"] = "00:00:10",
            },
            registry => registry.AddSingleton<TimeProvider>(clock),
            "alpha");

    private static ServiceProvider StubConnectors(params string[] keys) => StubConnectors([], keys);

    private static ServiceProvider StubConnectors(Dictionary<string, string?> settings, params string[] keys) =>
        StubConnectors(settings, _ => { }, keys);

    private static ServiceProvider StubConnectors(Dictionary<string, string?> settings, Action<IServiceCollection> configure, params string[] keys) =>
        ConnectorTestServices.Build(
            registry =>
            {
                foreach (var key in keys)
                {
                    registry.AddSingleton<IJobConnector>(new StubConnector(key, ConnectorCapabilities.None));
                }

                configure(registry);
            },
            settings);
}
