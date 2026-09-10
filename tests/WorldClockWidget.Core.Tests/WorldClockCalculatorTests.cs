using System.Globalization;
using WorldClockWidget.Core.Clock;
using WorldClockWidget.Core.Models;
using Xunit;

namespace WorldClockWidget.Core.Tests;

public class WorldClockCalculatorTests
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo s_enGb = CultureInfo.GetCultureInfo("en-GB");

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 3, 15, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(9, "+9 hrs")]
    [InlineData(-5, "−5 hrs")]
    [InlineData(1, "+1 hr")]
    [InlineData(-1, "−1 hr")]
    [InlineData(5.5, "+5.5 hrs")]
    [InlineData(5.75, "+5.75 hrs")]
    [InlineData(-3.5, "−3.5 hrs")]
    public void FormatOffsetDifference_MatchesClockAppStyle(double hours, string expected)
    {
        Assert.Equal(expected, WorldClockCalculator.FormatOffsetDifference(TimeSpan.FromHours(hours)));
    }

    [Fact]
    public void BuildCaption_SameOffset_SaysSameTime()
    {
        DateTimeOffset local = At(10);
        Assert.Equal("Today, same time", WorldClockCalculator.BuildCaption(local, local));
    }

    [Fact]
    public void BuildCaption_AheadAcrossMidnight_SaysTomorrow()
    {
        DateTimeOffset localNow = new(2026, 3, 15, 20, 0, 0, TimeSpan.Zero);
        DateTimeOffset cityNow = TimeZoneInfo.ConvertTime(localNow, TestZones.PlusNine); // 05:00 next day

        Assert.Equal("Tomorrow, +9 hrs", WorldClockCalculator.BuildCaption(cityNow, localNow));
    }

    [Fact]
    public void BuildCaption_BehindAcrossMidnight_SaysYesterday()
    {
        DateTimeOffset localNow = new(2026, 3, 15, 2, 0, 0, TimeSpan.Zero);
        DateTimeOffset cityNow = TimeZoneInfo.ConvertTime(localNow, TestZones.MinusFive); // 21:00 previous day

        Assert.Equal("Yesterday, −5 hrs", WorldClockCalculator.BuildCaption(cityNow, localNow));
    }

    [Fact]
    public void FormatTime_TwelveHour_UsesCultureDesignator()
    {
        var calculator = new WorldClockCalculator(TestZones.Utc, s_enUs);
        Assert.Equal("9:41 PM", calculator.FormatTime(At(21, 41), use24Hour: false));
        Assert.Equal("12:05 AM", calculator.FormatTime(At(0, 5), use24Hour: false));
    }

    [Fact]
    public void FormatTime_TwentyFourHour_IsZeroPadded()
    {
        var calculator = new WorldClockCalculator(TestZones.Utc, s_enUs);
        Assert.Equal("21:41", calculator.FormatTime(At(21, 41), use24Hour: true));
        Assert.Equal("00:05", calculator.FormatTime(At(0, 5), use24Hour: true));
    }

    [Fact]
    public void CulturePrefers24Hour_FollowsRegionalFormat()
    {
        Assert.False(new WorldClockCalculator(TestZones.Utc, s_enUs).CulturePrefers24Hour);
        Assert.True(new WorldClockCalculator(TestZones.Utc, s_enGb).CulturePrefers24Hour);
    }

    [Fact]
    public void ResolveUse24Hour_ExplicitSettingWins()
    {
        var calculator = new WorldClockCalculator(TestZones.Utc, s_enUs);
        Assert.True(calculator.ResolveUse24Hour(new ClockSettings { Use24HourClock = true }));
        Assert.False(calculator.ResolveUse24Hour(new ClockSettings { Use24HourClock = null }));
    }

    [Fact]
    public void BuildRows_UnknownZone_ProducesUnresolvedRowInsteadOfThrowing()
    {
        var calculator = new WorldClockCalculator(TestZones.Utc, s_enUs);
        var settings = new ClockSettings { Clocks = [new ClockEntry("Not/A_Zone", "Nowhere")] };

        ClockRow row = Assert.Single(calculator.BuildRows(settings, At(12)));

        Assert.False(row.IsResolved);
        Assert.Equal("Nowhere", row.Label);
        Assert.Equal("--:--", row.Time);
    }

    [Fact]
    public void BuildRows_UtcEntry_ResolvesAndFormats()
    {
        var calculator = new WorldClockCalculator(TestZones.PlusNine, s_enUs);
        var settings = new ClockSettings { Clocks = [new ClockEntry("UTC", "")], Use24HourClock = true };

        ClockRow row = Assert.Single(calculator.BuildRows(settings, At(12)));

        Assert.True(row.IsResolved);
        Assert.Equal("UTC", row.Label); // blank label falls back to the catalogue name
        Assert.Equal("12:00", row.Time);
        Assert.Equal("Today, −9 hrs", row.Caption);
    }
}
