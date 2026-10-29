using JobHunter.Api.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobHunter.Api.Tests.Common.Results;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unprocessable, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorType.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ErrorType.Unexpected, StatusCodes.Status500InternalServerError)]
    internal void ToProblemDetails_FailedResult_MapsErrorTypeToStatusCode(ErrorType type, int expectedStatus)
    {
        var result = Result.Failure(new Error("Some.Code", "Something happened", type));

        var problem = Assert.IsType<ProblemHttpResult>(result.ToProblemDetails());

        Assert.Equal(expectedStatus, problem.StatusCode);
    }

    [Fact]
    public void ToProblemDetails_FailedResult_ExposesErrorCodeAndMessage()
    {
        var result = Result.Failure(Error.NotFound("Application.NotFound", "Application not found"));

        var problem = Assert.IsType<ProblemHttpResult>(result.ToProblemDetails());

        Assert.Equal("Application not found", problem.ProblemDetails.Detail);
        Assert.Equal("Application.NotFound", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void ToProblemDetails_SuccessfulResult_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Success().ToProblemDetails());
    }

    [Fact]
    public void Value_FailedResult_Throws()
    {
        Result<int> result = Error.Conflict("Posting.AlreadyExists", "Posting already exists");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
