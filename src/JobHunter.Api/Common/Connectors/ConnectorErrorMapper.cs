using JobHunter.Api.Common.Results;
using JobHunter.Connectors.Abstractions.Results;

namespace JobHunter.Api.Common.Connectors;

internal static class ConnectorErrorMapper
{
    public static Error ToError(this ConnectorError error) => error.Kind switch
    {
        ConnectorErrorKind.Transient or ConnectorErrorKind.RateLimited => Error.Unavailable(error.Code, error.Message),
        ConnectorErrorKind.NotFound => Error.NotFound(error.Code, error.Message),
        ConnectorErrorKind.InvalidInput => Error.Validation(error.Code, error.Message),
        ConnectorErrorKind.PostingClosed or ConnectorErrorKind.FormChanged => Error.Conflict(error.Code, error.Message),
        _ => Error.Unprocessable(error.Code, error.Message),
    };
}
