using WorldClockWidget.Core.Cards;

namespace WorldClockWidget.Widgets;

/// <summary>
/// What the provider remembers about one pinned widget. There may be several instances of the
/// same definition (the manifest sets <c>AllowMultiple="true"</c>), each with its own size and
/// visibility, all sharing one set of user settings.
/// </summary>
internal sealed class WidgetInstance
{
    public WidgetInstance(string id, WidgetSize size)
    {
        Id = id;
        Size = size;
    }

    /// <summary>The host-assigned id passed back in every callback.</summary>
    public string Id { get; }

    /// <summary>Current size; updated from <c>OnWidgetContextChanged</c>.</summary>
    public WidgetSize Size { get; set; }

    /// <summary>True between <c>Activate</c> and <c>Deactivate</c>, i.e. while the board can show updates.</summary>
    public bool IsActive { get; set; }

    /// <summary>True while the customisation card is showing instead of the clock list.</summary>
    public bool InCustomization { get; set; }
}
