using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace UrbanPlanToolbox.Widgets;

/// <summary>Dedicated local-COM-server mode of the existing packaged WinUI executable.</summary>
internal static class InspirationWidgetHost
{
    internal const string ClassId = "6C7543F7-AC8A-49D4-9AA2-5DDCF31673D6";
    private static readonly ManualResetEvent StopEvent = new(false);

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid classId,
        [MarshalAs(UnmanagedType.IUnknown)] object factory,
        uint classContext, uint flags, out uint cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint cookie);

    internal static void RequestStop() => StopEvent.Set();

    internal static void Run()
    {
        var factory = new WidgetProviderFactory();
        // CLSCTX_LOCAL_SERVER = 4; REGCLS_MULTIPLEUSE = 1.
        Marshal.ThrowExceptionForHR(CoRegisterClassObject(Guid.Parse(ClassId), factory, 4, 1, out var cookie));
        try
        {
            // The widget host controls activation; there is no tray or WinUI window in this mode.
            // On last unpin DeleteWidget triggers clean shutdown.
            StopEvent.WaitOne();
        }
        finally
        {
            CoRevokeClassObject(cookie);
            GC.KeepAlive(factory);
        }
    }

    [ComImport]
    [ComVisible(false)]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr outer, ref Guid iid, out IntPtr instance);

        [PreserveSig]
        int LockServer([MarshalAs(UnmanagedType.Bool)] bool lockServer);
    }

    [ComVisible(true)]
    private sealed class WidgetProviderFactory : IClassFactory
    {
        public int CreateInstance(IntPtr outer, ref Guid iid, out IntPtr instance)
        {
            instance = IntPtr.Zero;
            if (outer != IntPtr.Zero) return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
            IntPtr unknown = IntPtr.Zero;
            try
            {
                unknown = MarshalInspectable<IWidgetProvider>.FromManaged(new InspirationWidgetProvider());
                return Marshal.QueryInterface(unknown, in iid, out instance);
            }
            catch (Exception exception)
            {
                return Marshal.GetHRForException(exception);
            }
            finally
            {
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
            }
        }

        public int LockServer(bool lockServer) => 0;
    }
}
