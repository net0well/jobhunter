using System.Security.Cryptography;
using System.Text;
using JobHunter.Api.Common.Options;
using JobHunter.Api.Common.Results;
using Microsoft.Extensions.Options;

namespace JobHunter.Api.Common.Endpoints;

internal sealed class ApiKeyEndpointFilter(IOptions<ApiOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";

    private readonly byte[]? _expectedHash = options.Value.ApiKey is { Length: > 0 } key ? Hash(key) : null;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!options.Value.RequireApiKey)
            return await next(context);

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        // Hashing first keeps the comparison constant-time regardless of the provided key length.
        if (_expectedHash is null || provided.Length == 0 || !CryptographicOperations.FixedTimeEquals(Hash(provided), _expectedHash))
            return Error.Unauthorized("Api.InvalidApiKey", $"A valid {HeaderName} header is required.").ToProblemDetails();

        return await next(context);
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
