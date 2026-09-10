using WorldClockWidget.Core.Models;
using WorldClockWidget.Core.Settings;
using Xunit;

namespace WorldClockWidget.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WorldClockWidgetTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutFile_ReturnsDefaults()
    {
        var store = new SettingsStore(_directory, () => TestZones.PlusFiveThirty);

        ClockSettings settings = store.Load();

        Assert.NotEmpty(settings.Clocks);
        Assert.Null(settings.Use24HourClock);
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(_directory, () => TestZones.PlusFiveThirty);
        var original = new ClockSettings
        {
            Use24HourClock = true,
            Clocks = [new ClockEntry("UTC", "Zulu"), new ClockEntry("Tokyo Standard Time", "Tokyo")],
        };

        store.Save(original);
        ClockSettings loaded = store.Load();

        Assert.True(loaded.Use24HourClock);
        Assert.Equal(original.Clocks, loaded.Clocks);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, SettingsStore.FileName), "{ this is not json");
        var store = new SettingsStore(_directory, () => TestZones.PlusFiveThirty);

        ClockSettings settings = store.Load();

        Assert.NotEmpty(settings.Clocks);
    }

    [Fact]
    public void Load_DropsBlankEntriesAndTrimsToMaximum()
    {
        Directory.CreateDirectory(_directory);
        string clocks = string.Join(",", Enumerable.Range(0, ClockSettings.MaxClocks + 3)
            .Select(i => $"{{\"timeZoneId\":\"Zone{i}\",\"label\":\"Z{i}\"}}"));
        string json = $"{{\"schemaVersion\":1,\"clocks\":[{{\"timeZoneId\":\" \",\"label\":\"blank\"}},{clocks}]}}";
        File.WriteAllText(Path.Combine(_directory, SettingsStore.FileName), json);
        var store = new SettingsStore(_directory);

        ClockSettings settings = store.Load();

        Assert.Equal(ClockSettings.MaxClocks, settings.Clocks.Count);
        Assert.DoesNotContain(settings.Clocks, c => c.Label == "blank");
    }
}
