using System.Text.Json;
using WorldClockWidget.Core.Cards;
using Xunit;

namespace WorldClockWidget.Core.Tests;

public class WidgetTemplatesTests
{
    [Fact]
    public void ClockTemplate_IsValidAdaptiveCardJson()
    {
        using JsonDocument card = JsonDocument.Parse(WidgetTemplates.Clock);

        Assert.Equal("AdaptiveCard", card.RootElement.GetProperty("type").GetString());
        Assert.Equal("1.5", card.RootElement.GetProperty("version").GetString());
        Assert.Contains(WidgetVerbs.OpenClockApp, WidgetTemplates.Clock, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomizationTemplate_ReferencesEveryVerbAndInput()
    {
        using JsonDocument card = JsonDocument.Parse(WidgetTemplates.Customization);
        Assert.Equal("AdaptiveCard", card.RootElement.GetProperty("type").GetString());

        string json = WidgetTemplates.Customization;
        Assert.Contains($"\"verb\": \"{WidgetVerbs.AddClock}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"verb\": \"{WidgetVerbs.RemoveClock}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"verb\": \"{WidgetVerbs.MoveClockUp}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"verb\": \"{WidgetVerbs.ExitCustomization}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"id\": \"{WidgetInputs.TimeZoneId}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"id\": \"{WidgetInputs.Label}\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"id\": \"{WidgetInputs.Use24Hour}\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Templates_OnlyUseActionExecute()
    {
        // The Widgets Board supports Action.Execute only; anything else renders as an error.
        foreach (string template in new[] { WidgetTemplates.Clock, WidgetTemplates.Customization })
        {
            Assert.DoesNotContain("Action.Submit", template, StringComparison.Ordinal);
            Assert.DoesNotContain("Action.OpenUrl", template, StringComparison.Ordinal);
            Assert.DoesNotContain("Action.ShowCard", template, StringComparison.Ordinal);
        }
    }
}
