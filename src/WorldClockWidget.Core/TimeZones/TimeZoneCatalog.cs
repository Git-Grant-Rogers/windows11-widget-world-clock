using System.Collections.Frozen;
using System.Globalization;
using WorldClockWidget.Core.Models;

namespace WorldClockWidget.Core.TimeZones;

/// <summary>
/// Knows about the time zones installed on the machine and how to present them to people:
/// a picker list for the customisation card and a short city label for the widget itself.
/// </summary>
public static class TimeZoneCatalog
{
    /// <summary>
    /// A friendly city name for the most common Windows time-zone ids. Windows display names
    /// list several cities ("Osaka, Sapporo, Tokyo"), which is too long for a widget row, so
    /// we pick the one people recognise. IANA aliases are included so the defaults resolve
    /// on non-Windows test machines too.
    /// </summary>
    private static readonly FrozenDictionary<string, string> s_knownCities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["UTC"] = "UTC",
        ["GMT Standard Time"] = "London",
        ["Europe/London"] = "London",
        ["Romance Standard Time"] = "Paris",
        ["Europe/Paris"] = "Paris",
        ["W. Europe Standard Time"] = "Berlin",
        ["Europe/Berlin"] = "Berlin",
        ["Central Europe Standard Time"] = "Prague",
        ["Central European Standard Time"] = "Warsaw",
        ["GTB Standard Time"] = "Athens",
        ["FLE Standard Time"] = "Helsinki",
        ["E. Europe Standard Time"] = "Chisinau",
        ["Turkey Standard Time"] = "Istanbul",
        ["Russian Standard Time"] = "Moscow",
        ["Europe/Moscow"] = "Moscow",
        ["Israel Standard Time"] = "Jerusalem",
        ["Egypt Standard Time"] = "Cairo",
        ["South Africa Standard Time"] = "Johannesburg",
        ["W. Central Africa Standard Time"] = "Lagos",
        ["Arabian Standard Time"] = "Dubai",
        ["Asia/Dubai"] = "Dubai",
        ["Arab Standard Time"] = "Riyadh",
        ["Iran Standard Time"] = "Tehran",
        ["Pakistan Standard Time"] = "Karachi",
        ["India Standard Time"] = "New Delhi",
        ["Asia/Kolkata"] = "New Delhi",
        ["Nepal Standard Time"] = "Kathmandu",
        ["Bangladesh Standard Time"] = "Dhaka",
        ["SE Asia Standard Time"] = "Bangkok",
        ["Asia/Bangkok"] = "Bangkok",
        ["Singapore Standard Time"] = "Singapore",
        ["Asia/Singapore"] = "Singapore",
        ["China Standard Time"] = "Beijing",
        ["Asia/Shanghai"] = "Beijing",
        ["Asia/Hong_Kong"] = "Hong Kong",
        ["Taipei Standard Time"] = "Taipei",
        ["Korea Standard Time"] = "Seoul",
        ["Asia/Seoul"] = "Seoul",
        ["Tokyo Standard Time"] = "Tokyo",
        ["Asia/Tokyo"] = "Tokyo",
        ["W. Australia Standard Time"] = "Perth",
        ["Cen. Australia Standard Time"] = "Adelaide",
        ["E. Australia Standard Time"] = "Brisbane",
        ["AUS Eastern Standard Time"] = "Sydney",
        ["Australia/Sydney"] = "Sydney",
        ["Australia/Melbourne"] = "Melbourne",
        ["New Zealand Standard Time"] = "Auckland",
        ["Pacific/Auckland"] = "Auckland",
        ["Hawaiian Standard Time"] = "Honolulu",
        ["Alaskan Standard Time"] = "Anchorage",
        ["Pacific Standard Time"] = "Los Angeles",
        ["America/Los_Angeles"] = "Los Angeles",
        ["Mountain Standard Time"] = "Denver",
        ["US Mountain Standard Time"] = "Phoenix",
        ["Central Standard Time"] = "Chicago",
        ["America/Chicago"] = "Chicago",
        ["Central Standard Time (Mexico)"] = "Mexico City",
        ["Eastern Standard Time"] = "New York",
        ["America/New_York"] = "New York",
        ["America/Toronto"] = "Toronto",
        ["Atlantic Standard Time"] = "Halifax",
        ["SA Pacific Standard Time"] = "Bogotá",
        ["Pacific SA Standard Time"] = "Santiago",
        ["Argentina Standard Time"] = "Buenos Aires",
        ["E. South America Standard Time"] = "São Paulo",
        ["America/Sao_Paulo"] = "São Paulo",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The clocks a fresh install shows. Chosen to span the globe so the widget is useful
    /// before anyone customises it.
    /// </summary>
    public static IReadOnlyList<ClockEntry> DefaultClocks { get; } =
    [
        new("GMT Standard Time", "London"),
        new("Eastern Standard Time", "New York"),
        new("Tokyo Standard Time", "Tokyo"),
        new("AUS Eastern Standard Time", "Sydney"),
    ];

    /// <summary>
    /// Resolves a time zone id, returning <see langword="null"/> instead of throwing when the
    /// id is unknown on this machine (for example after a Windows time-zone data update).
    /// </summary>
    public static TimeZoneInfo? TryFind(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out TimeZoneInfo? zone) ? zone : null;
    }

    /// <summary>
    /// True when <paramref name="timeZoneId"/> refers to the same zone as <paramref name="zone"/>,
    /// tolerating Windows/IANA id differences by comparing the resolved zones.
    /// </summary>
    public static bool IsSameZone(string timeZoneId, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (string.Equals(timeZoneId, zone.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        TimeZoneInfo? resolved = TryFind(timeZoneId);
        return resolved is not null && resolved.HasSameRules(zone) && resolved.BaseUtcOffset == zone.BaseUtcOffset
            && string.Equals(resolved.StandardName, zone.StandardName, StringComparison.Ordinal);
    }

    /// <summary>
    /// A short label for a zone: the curated city if we have one, otherwise the first city
    /// in the Windows display name ("(UTC+01:00) Amsterdam, Berlin, ..." becomes "Amsterdam").
    /// </summary>
    public static string GetFriendlyLabel(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (s_knownCities.TryGetValue(zone.Id, out string? known))
        {
            return known;
        }

        return DeriveLabel(zone.DisplayName, zone.Id);
    }

    /// <summary>
    /// The picker entries for the customisation card, ordered west to east and then by name,
    /// which matches the order Windows Settings uses.
    /// </summary>
    public static IReadOnlyList<TimeZoneChoice> GetChoices()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(zone => zone.BaseUtcOffset)
            .ThenBy(zone => zone.DisplayName, StringComparer.CurrentCulture)
            .Select(zone => new TimeZoneChoice(zone.Id, zone.DisplayName))
            .ToList();
    }

    internal static string DeriveLabel(string displayName, string fallback)
    {
        string name = displayName;

        // Strip a leading "(UTC+09:00) " style prefix.
        if (name.StartsWith('(') && name.IndexOf(')', StringComparison.Ordinal) is int close && close > 0)
        {
            name = name[(close + 1)..];
        }

        int comma = name.IndexOf(',', StringComparison.Ordinal);
        if (comma > 0)
        {
            name = name[..comma];
        }

        name = name.Trim();
        return name.Length > 0 ? name : fallback;
    }

    /// <summary>
    /// Formats a UTC offset the way Windows does in its time-zone picker, e.g. "UTC+09:00".
    /// </summary>
    public static string FormatUtcOffset(TimeSpan offset)
    {
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{offset.Hours:00}:{offset.Minutes:00}");
    }
}

/// <summary>
/// One entry in the time-zone picker.
/// </summary>
/// <param name="Id">The system time-zone id used as the choice value.</param>
/// <param name="Title">The display name shown to the user.</param>
public sealed record TimeZoneChoice(string Id, string Title);
