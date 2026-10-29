using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Tests.Common.Endpoints;

public sealed class ApiKeyEndpointFilterTests
{
    private const string ValidKey = "test-api-key-with-at-least-32-characters";

    private static readonly object NextResult = new();

    [Fact]
    public async Task InvokeAsync_ValidKey_CallsNext()
    {
        var result = await InvokeAsync(new ApiOptions { ApiKey = ValidKey }, providedKey: ValidKey);

        Assert.Same(NextResult, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData(ValidKey + "x")]
    public async Task InvokeAsync_MissingOrWrongKey_ReturnsUnauthorized(string? providedKey)
    {
        var result = await InvokeAsync(new ApiOptions { ApiKey = ValidKey }, providedKey);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal("Api.InvalidApiKey", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotRequired_CallsNextWithoutHeader()
    {
        var result = await InvokeAsync(new ApiOptions { RequireApiKey = false }, providedKey: null);

        Assert.Same(NextResult, result);
    }

    private static async Task<object?> InvokeAsync(ApiOptions options, string? providedKey)
    {
        var httpContext = new DefaultHttpContext();
        if (providedKey is not null)
            httpContext.Request.Headers[ApiKeyEndpointFilter.HeaderName] = providedKey;

        var filter = new ApiKeyEndpointFilter(Options.Create(options));

        return await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>(NextResult));
    }
}
