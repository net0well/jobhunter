using JobHunter.Api.Common.Persistence;
using JobHunter.Api.Domain;

namespace JobHunter.Api.Common.Connectors;

internal static class ConnectorStateExtensions
{
    public static async Task<ConnectorState> GetOrCreateConnectorStateAsync(this AppDbContext db, string connectorKey, DateOnly today, CancellationToken ct)
    {
        var state = await db.ConnectorStates.FindAsync([connectorKey], ct);
        if (state is not null)
            return state;

        state = new ConnectorState(connectorKey, today);
        db.ConnectorStates.Add(state);
        return state;
    }
}
