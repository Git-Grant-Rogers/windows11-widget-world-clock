using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WorldClockWidget.Services;

namespace WorldClockWidget;

/// <summary>
/// The COM-visible entry point the Widgets Board talks to. The host may create several of
/// these, so this class holds no state of its own and forwards everything to
/// <see cref="WidgetService"/>.
/// </summary>
/// <remarks>
/// The <see cref="GuidAttribute"/> value is the CLSID registered in Package.appxmanifest under
/// both <c>com:Class Id</c> and <c>CreateInstance ClassId</c>. Change all three together.
/// </remarks>
[ComVisible(true)]
[ComDefaultInterface(typeof(IWidgetProvider))]
[Guid("362D215B-9EDE-4946-AC8D-35CF85DDFAE9")]
public sealed class WidgetProvider : IWidgetProvider, IWidgetProvider2
{
    public WidgetProvider()
    {
        WidgetService.Instance.RecoverRunningWidgets();
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        ArgumentNullException.ThrowIfNull(widgetContext);
        WidgetService.Instance.CreateWidget(widgetContext);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        WidgetService.Instance.DeleteWidget(widgetId);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        ArgumentNullException.ThrowIfNull(actionInvokedArgs);
        WidgetService.Instance.OnActionInvoked(actionInvokedArgs);
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        ArgumentNullException.ThrowIfNull(contextChangedArgs);
        WidgetService.Instance.OnWidgetContextChanged(contextChangedArgs);
    }

    public void Activate(WidgetContext widgetContext)
    {
        ArgumentNullException.ThrowIfNull(widgetContext);
        WidgetService.Instance.Activate(widgetContext);
    }

    public void Deactivate(string widgetId)
    {
        WidgetService.Instance.Deactivate(widgetId);
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        ArgumentNullException.ThrowIfNull(customizationRequestedArgs);
        WidgetService.Instance.OnCustomizationRequested(customizationRequestedArgs);
    }
}
