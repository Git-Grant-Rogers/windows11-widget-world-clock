namespace WorldClockWidget.Core.Models;

/// <summary>
/// One city (time zone) the user has chosen to show on the widget.
/// </summary>
/// <param name="TimeZoneId">A Windows or IANA time-zone identifier accepted by <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>.</param>
/// <param name="Label">The short, human-friendly name shown on the widget, for example "Tokyo".</param>
public sealed record ClockEntry(string TimeZoneId, string Label)
{
    /// <summary>
    /// Returns a copy with the label replaced, keeping the time zone.
    /// </summary>
    public ClockEntry WithLabel(string label) => this with { Label = label };
}
