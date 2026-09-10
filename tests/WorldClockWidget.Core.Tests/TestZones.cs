namespace WorldClockWidget.Core.Tests;

/// <summary>
/// Fixed zones so tests never depend on the machine's own time zone or DST rules.
/// </summary>
internal static class TestZones
{
    public static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    public static readonly TimeZoneInfo PlusNine =
        TimeZoneInfo.CreateCustomTimeZone("Test+9", TimeSpan.FromHours(9), "(UTC+09:00) Test Nine", "Test Nine");

    public static readonly TimeZoneInfo MinusFive =
        TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "(UTC-05:00) Test Minus Five", "Test Minus Five");

    public static readonly TimeZoneInfo PlusFiveThirty =
        TimeZoneInfo.CreateCustomTimeZone("Test+5:30", new TimeSpan(5, 30, 0), "(UTC+05:30) Test India", "Test India");
}
