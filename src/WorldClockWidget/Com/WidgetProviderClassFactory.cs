using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace WorldClockWidget.Com;

/// <summary>
/// COM <c>IClassFactory</c> that hands the Widgets Board a new <typeparamref name="TProvider"/>
/// whenever it calls <c>CoCreateInstance</c> on our CLSID. Boilerplate lifted from the official
/// Windows App SDK widgets sample.
/// </summary>
[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000001-0000-0000-C000-000000000046")]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(nint pUnkOuter, ref Guid riid, out nint ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

internal sealed class WidgetProviderClassFactory<TProvider> : IClassFactory
    where TProvider : IWidgetProvider, new()
{
    private const int ClassENoAggregation = unchecked((int)0x80040110);
    private const int ENoInterface = unchecked((int)0x80004002);
    private static readonly Guid s_iUnknownIid = new("00000000-0000-0000-C000-000000000046");

    public int CreateInstance(nint pUnkOuter, ref Guid riid, out nint ppvObject)
    {
        ppvObject = nint.Zero;

        if (pUnkOuter != nint.Zero)
        {
            return ClassENoAggregation;
        }

        if (riid != typeof(TProvider).GUID && riid != s_iUnknownIid)
        {
            return ENoInterface;
        }

        ppvObject = MarshalInspectable<IWidgetProvider>.FromManaged(new TProvider());
        return 0;
    }

    public int LockServer(bool fLock) => 0;
}
