using System.Runtime.InteropServices;
using Windows.Win32.System.Com;

namespace Winshell.Native;

internal sealed class WindowDesktopManager
{
    private static readonly Guid ManagerClass = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    private readonly IVirtualDesktopManager _manager;

    private WindowDesktopManager(IVirtualDesktopManager manager)
    {
        _manager = manager;
    }

    internal static WindowDesktopManager? Create(out int hr)
    {
        var manager = Com.Create<IVirtualDesktopManager>(ManagerClass, CLSCTX.CLSCTX_INPROC_SERVER, out hr);
        return manager is null ? null : new WindowDesktopManager(manager);
    }

    internal int DesktopOf(nint hwnd, out Guid desktop)
    {
        return _manager.GetWindowDesktopId(hwnd, out desktop);
    }

    internal int Move(nint hwnd, Guid desktop)
    {
        return _manager.MoveWindowToDesktop(hwnd, in desktop);
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(nint hwnd, out int onCurrent);

        [PreserveSig]
        int GetWindowDesktopId(nint hwnd, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(nint hwnd, in Guid desktopId);
    }
}
