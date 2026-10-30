using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;
using JobHunter.Api.Tests.Infrastructure;
using JobHunter.Connectors.Abstractions.Results;
using JobHunter.Connectors.Abstractions.Sources;
using JobHunter.Connectors.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace JobHunter.Api.Tests.Connectors;

[Collection(nameof(ApiCollection))]
public sealed class ConnectorInvokerTests(JobHunterApiFactory factory) : IAsyncLifetime
{
    private static readonly ConnectorError Transient = new(ConnectorErrorKind.Transient, "Fake.Timeout", "Upstream timed out");

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task InvokeAsync_SuccessfulOperation_ReturnsConnectorResult()
    {
        factory.Fake.Tenants["acme"] = [];

        var result = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, token => factory.Fake.ListAsync(new TenantRef("acme"), token), ct));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task InvokeAsync_OperationThrows_ReturnsUnhandledTransientWithoutThrowing()
    {
        factory.Fake.ThrowNext(new InvalidOperationException("selector exploded"));

        var result = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, token => factory.Fake.ProbeAsync(token), ct));

        Assert.Equal(ConnectorErrorKind.Transient, result.Error?.Kind);
        Assert.Equal("Connector.Unhandled", result.Error?.Code);
    }

    [Fact]
    public async Task InvokeAsync_ConnectorPaused_ReturnsPausedWithoutRunningOperation()
    {
        await PauseFakeAsync(factory.Clock.GetUtcNow().AddHours(1));
        var calls = 0;

        var result = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, _ =>
        {
            calls++;
            return Task.FromResult(ConnectorResult<bool>.Ok(true));
        }, ct));

        Assert.Equal("Connector.Paused", result.Error?.Code);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task InvokeAsync_PauseExpired_RunsOperation()
    {
        await PauseFakeAsync(factory.Clock.GetUtcNow().AddMinutes(1));
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        var result = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, token => factory.Fake.ProbeAsync(token), ct));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task InvokeAsync_SubmitReturnsTransient_DoesNotRetry()
    {
        var calls = 0;

        var result = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, _ =>
        {
            calls++;
            return Task.FromResult(ConnectorResult<bool>.Fail(Transient));
        }, ct, ConnectorOperationKind.Submit));

        Assert.Equal(ConnectorErrorKind.Transient, result.Error?.Kind);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvokeAsync_ReadReturnsTransient_RetriesTwiceBeforeGivingUp()
    {
        var calls = 0;
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<ConnectorInvoker>();

        var invocation = invoker.InvokeAsync(FakeConnector.Key, _ =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(ConnectorResult<bool>.Fail(Transient));
        }, ct);

        // Retry back-off runs on the fake clock, so time has to be pushed forward for the retries to fire.
        while (!invocation.IsCompleted)
        {
            factory.Clock.Advance(TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }

        var result = await invocation;
        Assert.Equal(ConnectorErrorKind.Transient, result.Error?.Kind);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task InvokeAsync_RepeatedLayoutChanges_OpensCircuitUntilClosed()
    {
        var layoutChanged = ConnectorResult<bool>.Fail(new ConnectorError(ConnectorErrorKind.LayoutChanged, "Fake.Layout", "Markup changed"));

        for (var call = 0; call < 10; call++)
        {
            await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, _ => Task.FromResult(layoutChanged), ct));
        }

        var whileOpen = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, token => factory.Fake.ProbeAsync(token), ct));
        await factory.Services.GetRequiredService<ConnectorCircuitStates>().CloseAllAsync(TestContext.Current.CancellationToken);
        var afterClose = await InvokeAsync((invoker, ct) => invoker.InvokeAsync(FakeConnector.Key, token => factory.Fake.ProbeAsync(token), ct));

        Assert.Equal("Connector.CircuitOpen", whileOpen.Error?.Code);
        Assert.True(afterClose.IsSuccess);
    }

    private async Task<ConnectorResult<T>> InvokeAsync<T>(Func<ConnectorInvoker, CancellationToken, Task<ConnectorResult<T>>> invoke)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await invoke(scope.ServiceProvider.GetRequiredService<ConnectorInvoker>(), TestContext.Current.CancellationToken);
    }

    private async Task PauseFakeAsync(DateTimeOffset until)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = new ConnectorState(FakeConnector.Key, DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime));
        state.Pause(until, "Captcha detected", factory.Clock.GetUtcNow());
        db.ConnectorStates.Add(state);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
