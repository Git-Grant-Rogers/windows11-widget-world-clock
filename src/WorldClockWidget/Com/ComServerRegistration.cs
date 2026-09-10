using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;

namespace WorldClockWidget.Com;

/// <summary>
/// Registers the provider's class factory with COM for the lifetime of the process and
/// revokes it on dispose. The CLSID comes from the <see cref="GuidAttribute"/> on the provider
/// class and must match <c>com:Class Id</c> and <c>CreateInstance ClassId</c> in Package.appxmanifest.
/// </summary>
internal sealed class ComServerRegistration : IDisposable
{
    private uint _cookie;
    private bool _disposed;

    private ComServerRegistration(uint cookie)
    {
        _cookie = cookie;
    }

    public static ComServerRegistration Register<TProvider>()
        where TProvider : IWidgetProvider, new()
    {
        Guid clsid = typeof(TProvider).GUID;
        int hr = NativeMethods.CoRegisterClassObject(
            clsid,
            new WidgetProviderClassFactory<TProvider>(),
            NativeMethods.ClsctxLocalServer,
            NativeMethods.RegclsMultipleUse,
            out uint cookie);

        Marshal.ThrowExceptionForHR(hr);
        return new ComServerRegistration(cookie);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = NativeMethods.CoRevokeClassObject(_cookie);
        _cookie = 0;
    }
}
