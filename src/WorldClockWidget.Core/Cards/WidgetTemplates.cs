using System.Reflection;

namespace WorldClockWidget.Core.Cards;

/// <summary>
/// Loads the Adaptive Card templates embedded in this assembly. Templates are static JSON;
/// everything that changes at runtime is supplied through the data payload instead.
/// </summary>
public static class WidgetTemplates
{
    private static readonly Lazy<string> s_clockTemplate = new(() => Read("Templates.WorldClockTemplate.json"));
    private static readonly Lazy<string> s_customizationTemplate = new(() => Read("Templates.CustomizationTemplate.json"));

    /// <summary>The everyday clock list, with layouts for small, medium and large sizes.</summary>
    public static string Clock => s_clockTemplate.Value;

    /// <summary>The "Customize widget" card: manage clocks and the 12/24-hour toggle.</summary>
    public static string Customization => s_customizationTemplate.Value;

    private static string Read(string logicalName)
    {
        Assembly assembly = typeof(WidgetTemplates).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded template '{logicalName}' is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
