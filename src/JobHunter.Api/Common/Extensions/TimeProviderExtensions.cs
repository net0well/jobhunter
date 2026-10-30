namespace JobHunter.Api.Common.Extensions;

internal static class TimeProviderExtensions
{
    public static DateOnly GetLocalToday(this TimeProvider clock, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
}
