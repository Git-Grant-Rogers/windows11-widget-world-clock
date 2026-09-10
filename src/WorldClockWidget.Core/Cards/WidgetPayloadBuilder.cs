using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldClockWidget.Core.Clock;
using WorldClockWidget.Core.Models;
using WorldClockWidget.Core.TimeZones;

namespace WorldClockWidget.Core.Cards;

/// <summary>
/// Produces the JSON <em>data</em> payloads that are bound into the card templates.
/// Keeping data separate from the template lets the provider refresh the time every minute
/// by sending a small data update only.
/// </summary>
public sealed class WidgetPayloadBuilder
{
    private static readonly JsonSerializerOptions s_compactJson = new() { WriteIndented = false };

    private readonly WorldClockCalculator _calculator;

    public WidgetPayloadBuilder(WorldClockCalculator calculator)
    {
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    /// <summary>
    /// Data for the clock view. Only as many rows as the size can show are included so the
    /// card never overflows and the host never has to clip content.
    /// </summary>
    public string BuildClockData(ClockSettings settings, WidgetSize size, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(settings);

        IReadOnlyList<ClockRow> rows = _calculator.BuildRows(settings, utcNow);
        int limit = size.MaxRows();

        var clocks = new JsonArray();
        foreach (ClockRow row in rows.Take(limit))
        {
            clocks.Add((JsonNode)ToClockNode(row));
        }

        ClockRow? primary = rows.Count > 0 ? rows[0] : null;

        var data = new JsonObject
        {
            ["hasClocks"] = rows.Count > 0,
            ["primary"] = primary is null ? EmptyPrimary() : ToClockNode(primary),
            ["clocks"] = clocks,
            ["hiddenCount"] = Math.Max(0, rows.Count - limit),
            ["updatedAt"] = utcNow.ToString("o", CultureInfo.InvariantCulture),
        };

        return data.ToJsonString(s_compactJson);
    }

    /// <summary>
    /// Data for the customisation card: the current list plus the full time-zone picker.
    /// </summary>
    public string BuildCustomizationData(ClockSettings settings, DateTimeOffset utcNow, IReadOnlyList<TimeZoneChoice>? choices = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        choices ??= TimeZoneCatalog.GetChoices();

        var clocks = new JsonArray();
        foreach (ClockRow row in _calculator.BuildRows(settings, utcNow))
        {
            JsonNode node = new JsonObject
            {
                ["timeZoneId"] = row.TimeZoneId,
                ["label"] = row.Label,
                ["zoneName"] = row.ZoneName,
            };
            clocks.Add(node);
        }

        var zones = new JsonArray();
        foreach (TimeZoneChoice choice in choices)
        {
            JsonNode node = new JsonObject
            {
                ["value"] = choice.Id,
                ["title"] = choice.Title,
            };
            zones.Add(node);
        }

        bool use24Hour = _calculator.ResolveUse24Hour(settings);

        var data = new JsonObject
        {
            ["hasClocks"] = settings.Clocks.Count > 0,
            ["canAddMore"] = settings.Clocks.Count < ClockSettings.MaxClocks,
            ["clockCount"] = settings.Clocks.Count,
            ["maxClocks"] = ClockSettings.MaxClocks,
            ["clocks"] = clocks,
            ["availableZones"] = zones,
            // Input.Toggle wants its value as a string matching valueOn/valueOff.
            ["use24Hour"] = use24Hour ? "true" : "false",
        };

        return data.ToJsonString(s_compactJson);
    }

    private static JsonObject ToClockNode(ClockRow row) => new()
    {
        ["timeZoneId"] = row.TimeZoneId,
        ["label"] = row.Label,
        ["time"] = row.Time,
        ["caption"] = row.Caption,
    };

    private static JsonObject EmptyPrimary() => new()
    {
        ["timeZoneId"] = string.Empty,
        ["label"] = "No clocks yet",
        ["time"] = "--:--",
        ["caption"] = "Choose Customize widget to add cities",
    };
}
