namespace WorldClockWidget.Core.Cards;

/// <summary>
/// The three sizes the Widgets Board offers. Mirrors <c>Microsoft.Windows.Widgets.WidgetSize</c>
/// without taking a dependency on the Windows App SDK.
/// </summary>
public enum WidgetSize
{
    Small,
    Medium,
    Large,
}

public static class WidgetSizeExtensions
{
    /// <summary>
    /// How many clock rows fit comfortably at each size, respecting the 16px margins, the
    /// 48px attribution area and 44px per two-line row from the Windows widget design guidance.
    /// </summary>
    public static int MaxRows(this WidgetSize size) => size switch
    {
        WidgetSize.Small => 1,
        WidgetSize.Medium => 4,
        WidgetSize.Large => 8,
        _ => 4,
    };

    /// <summary>
    /// Parses the host's size string ("small", "medium", "large"), defaulting to medium.
    /// </summary>
    public static WidgetSize Parse(string? value) => value?.ToUpperInvariant() switch
    {
        "SMALL" => WidgetSize.Small,
        "LARGE" => WidgetSize.Large,
        _ => WidgetSize.Medium,
    };
}
