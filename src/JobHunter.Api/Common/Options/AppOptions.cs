namespace JobHunter.Api.Common.Options;

internal sealed class AppOptions
{
    public const string Section = "App";

    public string TimeZone { get; init; } = "America/Sao_Paulo";

    public TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
}
