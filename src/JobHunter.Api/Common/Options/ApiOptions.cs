namespace JobHunter.Api.Common.Options;

internal sealed class ApiOptions
{
    public const string Section = "Api";

    public const int MinimumApiKeyLength = 32;

    public bool RequireApiKey { get; init; } = true;

    public string? ApiKey { get; init; }
}
