using JobHunter.Api.Common.Endpoints;
using JobHunter.Api.Common.Options;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace JobHunter.Api.Common.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        var apiKey = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value.ApiKey;

        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle("JobHunter")
            .AddPreferredSecuritySchemes(ApiKeySecuritySchemeTransformer.SchemeName)
            .AddApiKeyAuthentication(ApiKeySecuritySchemeTransformer.SchemeName, scheme => scheme.Value = apiKey));

        return app;
    }
}
