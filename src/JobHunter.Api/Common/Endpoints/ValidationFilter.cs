using FluentValidation;

namespace JobHunter.Api.Common.Endpoints;

internal sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return await next(context);

        var validation = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (validation.IsValid)
            return await next(context);

        return TypedResults.ValidationProblem(validation.ToDictionary());
    }
}
