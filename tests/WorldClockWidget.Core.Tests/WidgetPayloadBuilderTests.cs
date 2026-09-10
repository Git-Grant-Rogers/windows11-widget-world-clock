using System.Globalization;
using System.Text.Json;
using WorldClockWidget.Core.Cards;
using WorldClockWidget.Core.Clock;
using WorldClockWidget.Core.Models;
using WorldClockWidget.Core.TimeZones;
using Xunit;

namespace WorldClockWidget.Core.Tests;

public class WidgetPayloadBuilderTests
{
    private static readonly DateTimeOffset s_noon = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private static WidgetPayloadBuilder CreateBuilder() =>
        new(new WorldClockCalculator(TestZones.Utc, CultureInfo.GetCultureInfo("en-US")));

    private static ClockSettings SettingsWith(int count)
    {
        var settings = new ClockSettings();
        for (int i = 0; i < count; i++)
        {
            settings.Clocks.Add(new ClockEntry("UTC", $"City {i}"));
        }

        return settings;
    }

    [Theory]
    [InlineData(WidgetSize.Small, 1)]
    [InlineData(WidgetSize.Medium, 4)]
    [InlineData(WidgetSize.Large, 8)]
    public void BuildClockData_LimitsRowsToWhatTheSizeCanShow(WidgetSize size, int expectedRows)
    {
        // Each entry is deliberately the same zone; the builder does not de-duplicate, the settings model does.
        ClockSettings settings = SettingsWith(10);

        using JsonDocument data = JsonDocument.Parse(CreateBuilder().BuildClockData(settings, size, s_noon));
        JsonElement root = data.RootElement;

        Assert.True(root.GetProperty("hasClocks").GetBoolean());
        Assert.Equal(expectedRows, root.GetProperty("clocks").GetArrayLength());
        Assert.Equal(10 - expectedRows, root.GetProperty("hiddenCount").GetInt32());
        Assert.Equal("City 0", root.GetProperty("primary").GetProperty("label").GetString());
    }

    [Fact]
    public void BuildClockData_NoClocks_ProvidesEmptyStatePrimary()
    {
        using JsonDocument data = JsonDocument.Parse(CreateBuilder().BuildClockData(new ClockSettings(), WidgetSize.Medium, s_noon));
        JsonElement root = data.RootElement;

        Assert.False(root.GetProperty("hasClocks").GetBoolean());
        Assert.Equal(0, root.GetProperty("clocks").GetArrayLength());
        Assert.Equal("No clocks yet", root.GetProperty("primary").GetProperty("label").GetString());
    }

    [Fact]
    public void BuildCustomizationData_IncludesPickerAndToggleAsString()
    {
        var settings = new ClockSettings { Clocks = [new ClockEntry("UTC", "Zulu")], Use24HourClock = true };
        IReadOnlyList<TimeZoneChoice> choices = [new("UTC", "(UTC) Coordinated Universal Time"), new("Tokyo Standard Time", "(UTC+09:00) Tokyo")];

        using JsonDocument data = JsonDocument.Parse(CreateBuilder().BuildCustomizationData(settings, s_noon, choices));
        JsonElement root = data.RootElement;

        Assert.Equal("true", root.GetProperty("use24Hour").GetString());
        Assert.True(root.GetProperty("canAddMore").GetBoolean());
        Assert.Equal(2, root.GetProperty("availableZones").GetArrayLength());
        Assert.Equal("Tokyo Standard Time", root.GetProperty("availableZones")[1].GetProperty("value").GetString());
        Assert.Equal("Zulu", root.GetProperty("clocks")[0].GetProperty("label").GetString());
    }

    [Fact]
    public void BuildCustomizationData_AtMaximum_DisablesAdding()
    {
        ClockSettings settings = SettingsWith(ClockSettings.MaxClocks);

        using JsonDocument data = JsonDocument.Parse(CreateBuilder().BuildCustomizationData(settings, s_noon, []));

        Assert.False(data.RootElement.GetProperty("canAddMore").GetBoolean());
    }

    [Theory]
    [InlineData("small", WidgetSize.Small)]
    [InlineData("Medium", WidgetSize.Medium)]
    [InlineData("LARGE", WidgetSize.Large)]
    [InlineData(null, WidgetSize.Medium)]
    [InlineData("weird", WidgetSize.Medium)]
    public void WidgetSize_Parse_IsCaseInsensitiveWithMediumDefault(string? input, WidgetSize expected)
    {
        Assert.Equal(expected, WidgetSizeExtensions.Parse(input));
    }
}
