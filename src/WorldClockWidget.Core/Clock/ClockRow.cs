namespace WorldClockWidget.Core.Clock;

/// <summary>
/// A single rendered line on the widget: what the card template binds to.
/// </summary>
/// <param name="TimeZoneId">The configured zone id, used to route actions back to the right entry.</param>
/// <param name="Label">City name, e.g. "Tokyo".</param>
/// <param name="Time">Formatted wall-clock time, e.g. "9:41 AM" or "21:41".</param>
/// <param name="Caption">Secondary line, e.g. "Tomorrow, +9 hrs" or "Today, same time".</param>
/// <param name="ZoneName">Long zone name for the customisation card, e.g. "(UTC+09:00) Osaka, Sapporo, Tokyo".</param>
/// <param name="IsResolved">False when the zone id is unknown on this machine.</param>
public sealed record ClockRow(
    string TimeZoneId,
    string Label,
    string Time,
    string Caption,
    string ZoneName,
    bool IsResolved);
