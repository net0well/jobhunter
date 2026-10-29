using FluentValidation;
using JobHunter.Api.Common.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobHunter.Api.Tests.Common.Endpoints;

public sealed class ValidationFilterTests
{
    private static readonly object NextResult = new();

    [Fact]
    public async Task InvokeAsync_ValidArgument_CallsNext()
    {
        var result = await InvokeAsync(new SampleRequest("Acme"));

        Assert.Same(NextResult, result);
    }

    [Fact]
    public async Task InvokeAsync_InvalidArgument_ReturnsValidationProblem()
    {
        var result = await InvokeAsync(new SampleRequest(""));

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains(nameof(SampleRequest.Name), problem.ProblemDetails.Errors.Keys);
    }

    private static async Task<object?> InvokeAsync(SampleRequest request)
    {
        var validator = new InlineValidator<SampleRequest>();
        validator.RuleFor(r => r.Name).NotEmpty();

        var filter = new ValidationFilter<SampleRequest>(validator);

        return await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), request),
            _ => ValueTask.FromResult<object?>(NextResult));
    }

    internal sealed record SampleRequest(string Name);
}
