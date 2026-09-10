namespace WorldClockWidget.Core.Cards;

/// <summary>
/// The <c>verb</c> values used by <c>Action.Execute</c> elements in the card templates.
/// The provider switches on these when the host reports a user action.
/// </summary>
public static class WidgetVerbs
{
    /// <summary>Tap anywhere on the clock list: open the Windows Clock app.</summary>
    public const string OpenClockApp = "openClockApp";

    /// <summary>Customisation card: add the time zone chosen in the picker.</summary>
    public const string AddClock = "addClock";

    /// <summary>Customisation card: remove the clock named in the action data.</summary>
    public const string RemoveClock = "removeClock";

    /// <summary>Customisation card: move the clock named in the action data one place up.</summary>
    public const string MoveClockUp = "moveClockUp";

    /// <summary>Customisation card: persist the toggle state and return to the clock view.</summary>
    public const string ExitCustomization = "exitCustomization";
}

/// <summary>
/// Input ids and data keys shared between the customisation template and the provider.
/// </summary>
public static class WidgetInputs
{
    public const string TimeZoneId = "timeZoneId";
    public const string Label = "label";
    public const string Use24Hour = "use24Hour";
}
