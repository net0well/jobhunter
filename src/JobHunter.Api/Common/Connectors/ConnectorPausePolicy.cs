using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Common.Connectors;

internal static class ConnectorPausePolicy
{
    // Errors that mean the platform is pushing back: insisting would only make it worse, so the whole connector waits.
    public static TimeSpan? PauseFor(ConnectorError error, TimeSpan blockedDuration) => error switch
    {
        { Code: ConnectorInvoker.PausedCode or ConnectorInvoker.LocalRateLimitCode } => null,
        { Kind: ConnectorErrorKind.LayoutChanged or ConnectorErrorKind.CaptchaDetected or ConnectorErrorKind.AuthenticationRequired } => blockedDuration,
        { Kind: ConnectorErrorKind.RateLimited, RetryAfter: { } retryAfter } => retryAfter,
        _ => null,
    };

    // Failures produced by the core itself before reaching the connector say nothing about the source.
    public static bool IsLocal(ConnectorError error) =>
        error.Code is ConnectorInvoker.PausedCode or ConnectorInvoker.LocalRateLimitCode;
}
