using WorldClockWidget.Core.TimeZones;

namespace WorldClockWidget.Core.Models;

/// <summary>
/// Everything the user can customise about the widget. Persisted as JSON by
/// <see cref="Settings.SettingsStore"/>.
/// </summary>
public sealed class ClockSettings
{
    /// <summary>
    /// Bumped when the on-disk shape changes so older files can be migrated or discarded.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// The maximum number of clocks a user may configure. Large widgets show eight rows,
    /// so anything beyond that would never be visible.
    /// </summary>
    public const int MaxClocks = 8;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Ordered list of clocks. The first entry is the "primary" clock shown on the small widget.
    /// </summary>
    public List<ClockEntry> Clocks { get; set; } = [];

    /// <summary>
    /// <see langword="true"/> for 24-hour time, <see langword="false"/> for 12-hour time,
    /// <see langword="null"/> to follow the Windows regional format.
    /// </summary>
    public bool? Use24HourClock { get; set; }

    /// <summary>
    /// Builds the out-of-box configuration: a handful of major cities, skipping the
    /// user's own time zone because the taskbar already shows local time.
    /// </summary>
    public static ClockSettings CreateDefault(TimeZoneInfo? localTimeZone = null)
    {
        localTimeZone ??= TimeZoneInfo.Local;
        var settings = new ClockSettings();

        foreach (ClockEntry candidate in TimeZoneCatalog.DefaultClocks)
        {
            if (TimeZoneCatalog.IsSameZone(candidate.TimeZoneId, localTimeZone))
            {
                continue;
            }

            settings.Clocks.Add(candidate);
        }

        return settings;
    }

    /// <summary>
    /// Adds a clock unless it is already present or the list is full. Returns whether anything changed.
    /// </summary>
    public bool TryAddClock(ClockEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (Clocks.Count >= MaxClocks)
        {
            return false;
        }

        if (Clocks.Any(existing => string.Equals(existing.TimeZoneId, entry.TimeZoneId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Clocks.Add(entry);
        return true;
    }

    /// <summary>
    /// Removes the first clock with the given time-zone id. Returns whether anything changed.
    /// </summary>
    public bool RemoveClock(string timeZoneId)
    {
        int index = IndexOf(timeZoneId);
        if (index < 0)
        {
            return false;
        }

        Clocks.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Moves a clock one position towards the top of the list. Returns whether anything changed.
    /// </summary>
    public bool MoveClockUp(string timeZoneId)
    {
        int index = IndexOf(timeZoneId);
        if (index <= 0)
        {
            return false;
        }

        (Clocks[index - 1], Clocks[index]) = (Clocks[index], Clocks[index - 1]);
        return true;
    }

    private int IndexOf(string timeZoneId) =>
        Clocks.FindIndex(entry => string.Equals(entry.TimeZoneId, timeZoneId, StringComparison.OrdinalIgnoreCase));
}
