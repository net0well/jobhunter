using JobHunter.Api.Common.Connectors;
using JobHunter.Api.Common.Extensions;
using JobHunter.Api.Common.Options;
using JobHunter.Api.Common.Persistence;
using JobHunter.Connectors.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Features.Connectors;

internal static class ListConnectors
{
    internal sealed record Query : IRequest<IReadOnlyList<Response>>;

    internal sealed record Response(
        string Key,
        string DisplayName,
        IReadOnlyList<string> Capabilities,
        bool UsesBrowser,
        bool RequiresAuthentication,
        bool Enabled,
        bool Paused,
        DateTimeOffset? PausedUntil,
        string? PauseReason,
        DateTimeOffset? LastSuccessAt,
        DateTimeOffset? LastFailureAt,
        int SubmissionsToday,
        int DailySubmissionLimit,
        string CircuitState);

    internal sealed class Handler(
        AppDbContext db,
        IConnectorRegistry registry,
        ConnectorCircuitStates circuits,
        IOptionsMonitor<ConnectorOptions> connectorOptions,
        IOptions<AppOptions> appOptions,
        TimeProvider clock) : IRequestHandler<Query, IReadOnlyList<Response>>
    {
        public async ValueTask<IReadOnlyList<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var states = await db.ConnectorStates.AsNoTracking().ToDictionaryAsync(state => state.ConnectorKey, cancellationToken);
            var now = clock.GetUtcNow();
            var today = clock.GetLocalToday(appOptions.Value.Zone);

            return
            [
                .. registry.Connectors.Select(descriptor =>
                {
                    var state = states.GetValueOrDefault(descriptor.Key);
                    var paused = state?.IsPaused(now) == true;

                    return new Response(
                        descriptor.Key,
                        descriptor.DisplayName,
                        [.. ConnectorCapabilityServices.Flags(descriptor.Capabilities).Select(capability => capability.ToString())],
                        descriptor.Traits.UsesBrowser,
                        descriptor.Traits.RequiresAuthentication,
                        Enabled: true,
                        paused,
                        paused ? state!.PausedUntil : null,
                        paused ? state!.PauseReason : null,
                        state?.LastSuccessAt,
                        state?.LastFailureAt,
                        state?.SubmissionsOn(today) ?? 0,
                        connectorOptions.Get(descriptor.Key).DailySubmissionLimit,
                        circuits.StateOf(descriptor.Key).ToString());
                }),
            ];
        }
    }
}
