using System.Diagnostics.Metrics;
using JobHunter.Api.Common.Connectors;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Common.Observability;

internal sealed class JobHunterMetrics
{
    private readonly Counter<long> _connectorCalls;
    private readonly Histogram<double> _connectorDuration;

    public JobHunterMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(JobHunterTelemetry.ServiceName);

        _connectorCalls = meter.CreateCounter<long>("jobhunter.connector.calls", description: "Calls made to connectors");
        _connectorDuration = meter.CreateHistogram<double>("jobhunter.connector.duration", unit: "ms", description: "Duration of connector calls");
    }

    public void RecordConnectorCall(string connectorKey, ConnectorOperationKind kind, ConnectorErrorKind? errorKind, TimeSpan elapsed)
    {
        var connector = new KeyValuePair<string, object?>("connector", connectorKey);
        var operation = new KeyValuePair<string, object?>("operation", kind.ToString());
        var outcome = new KeyValuePair<string, object?>("outcome", errorKind?.ToString() ?? "Success");

        _connectorCalls.Add(1, connector, operation, outcome);
        _connectorDuration.Record(elapsed.TotalMilliseconds, connector, operation);
    }
}
