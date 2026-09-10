using WorldClockWidget.Core.Models;
using WorldClockWidget.Core.TimeZones;
using Xunit;

namespace WorldClockWidget.Core.Tests;

public class ClockSettingsTests
{
    private static ClockEntry Entry(string id) => new(id, id);

    [Fact]
    public void TryAddClock_RejectsDuplicatesCaseInsensitively()
    {
        var settings = new ClockSettings();

        Assert.True(settings.TryAddClock(Entry("Tokyo Standard Time")));
        Assert.False(settings.TryAddClock(Entry("tokyo standard time")));
        Assert.Single(settings.Clocks);
    }

    [Fact]
    public void TryAddClock_StopsAtMaximum()
    {
        var settings = new ClockSettings();
        for (int i = 0; i < ClockSettings.MaxClocks; i++)
        {
            Assert.True(settings.TryAddClock(Entry($"Zone{i}")));
        }

        Assert.False(settings.TryAddClock(Entry("OneTooMany")));
        Assert.Equal(ClockSettings.MaxClocks, settings.Clocks.Count);
    }

    [Fact]
    public void RemoveClock_ReportsWhetherAnythingChanged()
    {
        var settings = new ClockSettings { Clocks = [Entry("A"), Entry("B")] };

        Assert.True(settings.RemoveClock("a"));
        Assert.False(settings.RemoveClock("missing"));
        Assert.Equal(["B"], settings.Clocks.Select(c => c.TimeZoneId));
    }

    [Fact]
    public void MoveClockUp_SwapsWithPrevious_AndIgnoresTop()
    {
        var settings = new ClockSettings { Clocks = [Entry("A"), Entry("B"), Entry("C")] };

        Assert.True(settings.MoveClockUp("C"));
        Assert.Equal(["A", "C", "B"], settings.Clocks.Select(c => c.TimeZoneId));

        Assert.False(settings.MoveClockUp("A"));
        Assert.False(settings.MoveClockUp("missing"));
    }

    [Fact]
    public void CreateDefault_SkipsTheLocalZone()
    {
        TimeZoneInfo? tokyo = TimeZoneCatalog.TryFind("Tokyo Standard Time");
        if (tokyo is null)
        {
            // Time-zone data for Windows ids is unavailable on this machine; nothing to verify.
            return;
        }

        ClockSettings settings = ClockSettings.CreateDefault(tokyo);

        Assert.DoesNotContain(settings.Clocks, c => c.Label == "Tokyo");
        Assert.Equal(TimeZoneCatalog.DefaultClocks.Count - 1, settings.Clocks.Count);
    }

    [Fact]
    public void CreateDefault_WithUnrelatedLocalZone_KeepsAllDefaults()
    {
        ClockSettings settings = ClockSettings.CreateDefault(TestZones.PlusFiveThirty);
        Assert.Equal(TimeZoneCatalog.DefaultClocks.Count, settings.Clocks.Count);
    }
}
