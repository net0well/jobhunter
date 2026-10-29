namespace JobHunter.Api.Common.Results;

internal enum ErrorType
{
    Validation,
    Unauthorized,
    NotFound,
    Conflict,
    Unprocessable,
    Unavailable,
    Unexpected,
}
