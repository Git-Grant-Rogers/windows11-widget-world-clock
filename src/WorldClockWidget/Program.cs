using WorldClockWidget.Com;
using WorldClockWidget.Services;

namespace WorldClockWidget;

/// <summary>
/// Process entry point. The Widgets Board launches this executable with
/// <c>-RegisterProcessAsComServer</c> (see Package.appxmanifest); anything else is a person
/// opening the app from Start, who just needs to be told where the widget lives.
/// </summary>
internal static class Program
{
    private const string ComServerSwitch = "-RegisterProcessAsComServer";

    [MTAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], ComServerSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return RunAsWidgetProvider();
        }

        ShowHowToPin();
        return 0;
    }

    private static int RunAsWidgetProvider()
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            using ComServerRegistration registration = ComServerRegistration.Register<WidgetProvider>();
            using WidgetService service = WidgetService.Instance;

            DiagnosticLog.Info("COM class object registered; waiting for the Widgets Board.");
            service.RecoverRunningWidgets();

            // Stay alive until the last widget is unpinned. The host re-launches us on demand.
            service.ShutdownRequested.WaitOne();
            DiagnosticLog.Info("Provider exiting.");
            return 0;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Provider crashed.", ex);
            return 1;
        }
    }

    private static void ShowHowToPin()
    {
        const string text =
            "World Clock Widget lives on the Windows 11 Widgets Board.\n\n" +
            "Press Win+W (or select Widgets on the taskbar), choose Add widgets, " +
            "then pick World clock. Use \"Customize widget\" on the widget's menu to choose your cities.";

        _ = NativeMethods.MessageBox(nint.Zero, text, "World Clock Widget", NativeMethods.MbOk | NativeMethods.MbIconInformation);
    }
}
