using System.Globalization;
using WorldClockWidget.Core.Models;
using WorldClockWidget.Core.TimeZones;

namespace WorldClockWidget.Core.Clock;

/// <summary>
/// Turns the user's clock list into display rows for a given instant. Pure and deterministic:
/// the current time, local zone and culture are all passed in so the logic is testable.
/// </summary>
public sealed class WorldClockCalculator
{
    /// <summary>Unicode minus sign, which reads better than a hyphen in the Segoe UI type ramp.</summary>
    internal const char MinusSign = '−';

    private readonly TimeZoneInfo _localZone;
    private readonly CultureInfo _culture;

    public WorldClockCalculator(TimeZoneInfo? localZone = null, CultureInfo? culture = null)
    {
        _localZone = localZone ?? TimeZoneInfo.Local;
        _culture = culture ?? CultureInfo.CurrentCulture;
    }

    /// <summary>
    /// Whether the regional format prefers a 24-hour clock ("HH" or "H" in the short time pattern).
    /// </summary>
    public bool CulturePrefers24Hour =>
        _culture.DateTimeFormat.ShortTimePattern.Contains('H', StringComparison.Ordinal);

    /// <summary>
    /// Resolves the effective clock style from the setting, falling back to the regional format.
    /// </summary>
    public bool ResolveUse24Hour(ClockSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Use24HourClock ?? CulturePrefers24Hour;
    }

    /// <summary>
    /// Builds one row per configured clock, in the user's order.
    /// </summary>
    public IReadOnlyList<ClockRow> BuildRows(ClockSettings settings, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(settings);

        bool use24Hour = ResolveUse24Hour(settings);
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(utcNow, _localZone);

        var rows = new List<ClockRow>(settings.Clocks.Count);
        foreach (ClockEntry entry in settings.Clocks)
        {
            rows.Add(BuildRow(entry, utcNow, localNow, use24Hour));
        }

        return rows;
    }

    private ClockRow BuildRow(ClockEntry entry, DateTimeOffset utcNow, DateTimeOffset localNow, bool use24Hour)
    {
        TimeZoneInfo? zone = TimeZoneCatalog.TryFind(entry.TimeZoneId);
        if (zone is null)
        {
            return new ClockRow(entry.TimeZoneId, entry.Label, "--:--", "Time zone not available", entry.TimeZoneId, IsResolved: false);
        }

        DateTimeOffset cityNow = TimeZoneInfo.ConvertTime(utcNow, zone);
        string time = FormatTime(cityNow, use24Hour);
        string caption = BuildCaption(cityNow, localNow);
        string label = string.IsNullOrWhiteSpace(entry.Label) ? TimeZoneCatalog.GetFriendlyLabel(zone) : entry.Label;

        return new ClockRow(entry.TimeZoneId, label, time, caption, zone.DisplayName, IsResolved: true);
    }

    internal string FormatTime(DateTimeOffset moment, bool use24Hour)
    {
        // Custom patterns keep the layout predictable across regions while the AM/PM designator
        // still follows the user's language.
        return use24Hour
            ? moment.ToString("HH:mm", CultureInfo.InvariantCulture)
            : moment.ToString("h:mm tt", _culture);
    }

    internal static string BuildCaption(DateTimeOffset cityNow, DateTimeOffset localNow)
    {
        string day = DescribeDay(cityNow, localNow);
        TimeSpan difference = cityNow.Offset - localNow.Offset;

        if (difference == TimeSpan.Zero)
        {
            return $"{day}, same time";
        }

        return $"{day}, {FormatOffsetDifference(difference)}";
    }

    internal static string DescribeDay(DateTimeOffset cityNow, DateTimeOffset localNow)
    {
        int dayDelta = (cityNow.Date - localNow.Date).Days;
        return dayDelta switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            // Only reachable with a broken clock or zone data; keep it honest rather than crashing.
            _ => cityNow.ToString("ddd", CultureInfo.CurrentCulture),
        };
    }

    /// <summary>
    /// Formats a difference the way the Windows Clock app does: "+9 hrs", "−1 hr", "+5.5 hrs".
    /// </summary>
    internal static string FormatOffsetDifference(TimeSpan difference)
    {
        char sign = difference < TimeSpan.Zero ? MinusSign : '+';
        double hours = Math.Abs(difference.TotalHours);

        string magnitude = hours % 1 == 0
            ? hours.ToString("0", CultureInfo.InvariantCulture)
            : hours.ToString("0.##", CultureInfo.InvariantCulture);

        string unit = hours == 1 ? "hr" : "hrs";
        return $"{sign}{magnitude} {unit}";
    }
}
