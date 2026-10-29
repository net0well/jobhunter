namespace JobHunter.Api.Common.Results;

internal static class ResultExtensions
{
    public static IResult ToProblemDetails(this Result result) =>
        result.IsFailure
            ? result.Error.ToProblemDetails()
            : throw new InvalidOperationException("A successful result cannot be converted to problem details.");

    public static IResult ToProblemDetails(this Error error) =>
        TypedResults.Problem(
            detail: error.Message,
            statusCode: error.Type.ToStatusCode(),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };
}
