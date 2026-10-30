using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Common.Connectors;

// Wraps any ConnectorResult<T> so a single resilience pipeline per connector can inspect errors regardless of T.
internal readonly record struct ConnectorCallOutcome(ConnectorError? Error, object BoxedResult);
