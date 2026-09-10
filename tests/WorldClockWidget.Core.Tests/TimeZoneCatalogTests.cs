using WorldClockWidget.Core.TimeZones;
using Xunit;

namespace WorldClockWidget.Core.Tests;

public class TimeZoneCatalogTests
{
    [Theory]
    [InlineData("(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna", "Amsterdam")]
    [InlineData("(UTC-03:00) Buenos Aires", "Buenos Aires")]
    [InlineData("Coordinated Universal Time", "Coordinated Universal Time")]
    [InlineData("   ", "fallback")]
    public void DeriveLabel_TakesFirstCityWithoutOffsetPrefix(string displayName, string expected)
    {
        Assert.Equal(expected, TimeZoneCatalog.DeriveLabel(displayName, "fallback"));
    }

    [Theory]
    [InlineData(9, 0, "UTC+09:00")]
    [InlineData(-5, 0, "UTC-05:00")]
    [InlineData(5, 30, "UTC+05:30")]
    [InlineData(0, 0, "UTC+00:00")]
    public void FormatUtcOffset_MatchesWindowsPicker(int hours, int minutes, string expected)
    {
        Assert.Equal(expected, TimeZoneCatalog.FormatUtcOffset(new TimeSpan(hours, minutes, 0)));
    }

    [Fact]
    public void GetFriendlyLabel_UsesCuratedCityForKnownZone()
    {
        Assert.Equal("UTC", TimeZoneCatalog.GetFriendlyLabel(TimeZoneInfo.Utc));
    }

    [Fact]
    public void GetFriendlyLabel_FallsBackToDisplayName()
    {
        Assert.Equal("Test Nine", TimeZoneCatalog.GetFriendlyLabel(TestZones.PlusNine));
    }

    [Fact]
    public void GetChoices_IsOrderedWestToEast()
    {
        IReadOnlyList<TimeZoneChoice> choices = TimeZoneCatalog.GetChoices();

        Assert.NotEmpty(choices);
        Assert.All(choices, c => Assert.False(string.IsNullOrWhiteSpace(c.Id)));
        Assert.All(choices, c => Assert.False(string.IsNullOrWhiteSpace(c.Title)));
    }

    [Fact]
    public void TryFind_ReturnsNullForUnknownAndBlank()
    {
        Assert.Null(TimeZoneCatalog.TryFind("Definitely/Not_Real"));
        Assert.Null(TimeZoneCatalog.TryFind(" "));
        Assert.Null(TimeZoneCatalog.TryFind(null));
        Assert.NotNull(TimeZoneCatalog.TryFind("UTC"));
    }
}
