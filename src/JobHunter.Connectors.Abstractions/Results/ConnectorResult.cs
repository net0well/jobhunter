using System.Diagnostics.CodeAnalysis;

namespace JobHunter.Connectors.Abstractions.Results;

public readonly record struct ConnectorResult<T>
{
    private ConnectorResult(T? value, ConnectorError? error) => (Value, Error) = (value, error);

    public T? Value { get; }

    public ConnectorError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static ConnectorResult<T> Ok(T value) => new(value, null);

    public static ConnectorResult<T> Fail(ConnectorError error) => new(default, error);
}
