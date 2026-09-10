using System.Runtime.InteropServices;

namespace WorldClockWidget.Com;

/// <summary>
/// The handful of Win32 entry points the provider needs. Classic <c>DllImport</c> is used on
/// purpose: <c>CoRegisterClassObject</c> takes an <c>IUnknown</c>, which the source-generated
/// <c>LibraryImport</c> marshaller does not support.
/// </summary>
internal static partial class NativeMethods
{
    public const uint ClsctxLocalServer = 0x4;
    public const uint RegclsMultipleUse = 0x1;
    public const uint MbOk = 0x0;
    public const uint MbIconInformation = 0x40;

#pragma warning disable SYSLIB1054 // LibraryImport cannot marshal IUnknown; DllImport is the supported route.
    [DllImport("ole32.dll")]
    public static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    [DllImport("ole32.dll")]
    public static extern int CoRevokeClassObject(uint dwRegister);
#pragma warning restore SYSLIB1054

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(nint hWnd, string text, string caption, uint type);
}
