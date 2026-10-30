using JobHunter.Api.Common.Options;
using JobHunter.Api.Features.Matching;
using JobHunter.Api.Features.Scanning;
using JobHunter.Api.Features.Scanning.Shared;
using JobHunter.Api.Workers;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace JobHunter.Api.Tests.Workers;

public sealed class WorkerTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SourceScanWorker_FirstCycle_SendsScanDueSourcesWithBatchSize()
    {
        var sender = new RecordingSender();
        await using var services = Services(sender, ("Workers:SourceScan:BatchSize", "7"));
        using var worker = Create<SourceScanWorker>(services, new FakeTimeProvider());

        await worker.StartAsync(Ct);
        var message = await sender.NextAsync(Ct);
        await worker.StopAsync(Ct);

        Assert.Equal(new ScanDueSources.Command(7), message);
    }

    [Fact]
    public async Task MatchingWorker_CycleThrows_KeepsRunningOnNextTick()
    {
        var sender = new RecordingSender(failFirst: true);
        var clock = new FakeTimeProvider();
        await using var services = Services(sender, ("Workers:Matching:TickInterval", Tick.ToString()));
        using var worker = Create<MatchingWorker>(services, clock);

        await worker.StartAsync(Ct);
        await sender.NextAsync(Ct);
        clock.Advance(Tick);
        var second = await sender.NextAsync(Ct);
        await worker.StopAsync(Ct);

        Assert.IsType<ScorePendingPostings.Command>(second);
    }

    [Fact]
    public async Task SourceScanWorker_Disabled_SendsNothing()
    {
        var sender = new RecordingSender();
        await using var services = Services(sender, ("Workers:SourceScan:Enabled", "false"));
        using var worker = Create<SourceScanWorker>(services, new FakeTimeProvider());

        await worker.StartAsync(Ct);
        await (worker.ExecuteTask ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await worker.StopAsync(Ct);

        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task ManualScanWorker_QueuedRequest_SendsScanSourceForTheRun()
    {
        var sender = new RecordingSender();
        await using var services = Services(sender);
        var queue = services.GetRequiredService<ScanRequestQueue>();
        using var worker = new ManualScanWorker(queue, services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<IOptionsMonitor<WorkersOptions>>(), NullLogger<ManualScanWorker>.Instance);
        var request = new ScanRequest(Guid.NewGuid(), Guid.NewGuid());

        await worker.StartAsync(Ct);
        queue.TryEnqueue(request);
        var message = await sender.NextAsync(Ct);
        await worker.StopAsync(Ct);

        Assert.Equal(new ScanSource.Command(request.SourceTargetId, request.ScanRunId), message);
    }

    private static ServiceProvider Services(RecordingSender sender, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<WorkersOptions>().Bind(configuration.GetSection(WorkersOptions.Section));
        services.AddScoped<ISender>(_ => sender);
        services.AddSingleton<ScanRequestQueue>();
        return services.BuildServiceProvider();
    }

    private static TWorker Create<TWorker>(IServiceProvider services, TimeProvider clock)
        where TWorker : BackgroundService =>
        ActivatorUtilities.CreateInstance<TWorker>(services, clock, NullLogger<TWorker>.Instance);
}
