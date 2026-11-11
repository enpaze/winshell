using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32.Foundation;

namespace Winshell.Native;

// CsWin32 will not generate POINT: it tells callers to use System.Drawing.Point, which the pointer and hook
// structs below do not. The generated RECT is the window rectangle.
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct MONITORINFOEXW
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    public fixed char szDevice[32];
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DISPLAY_DEVICEW
{
    public uint cb;
    public fixed char DeviceName[32];
    public fixed char DeviceString[128];
    public uint StateFlags;
    public fixed char DeviceID[128];
    public fixed char DeviceKey[128];
}

[StructLayout(LayoutKind.Sequential)]
internal struct APPBARDATA
{
    public uint cbSize;
    public nint hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public nint lParam;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HIGHCONTRASTW
{
    public uint cbSize;
    public uint dwFlags;
    public nint lpszDefaultScheme;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MARGINS
{
    public int Left;
    public int Right;
    public int Top;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData;
    public uint flags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}

/// <summary>REASON_CONTEXT. Only the simple string is used; the two fields after it pad the union to the size of its Detailed member.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct REASON_CONTEXT
{
    public uint Version;
    public uint Flags;
    public nint SimpleReasonString;
    public nint Padding1;
    public nint Padding2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_POWER_STATUS
{
    public byte ACLineStatus;
    public byte BatteryFlag;
    public byte BatteryLifePercent;
    public byte SystemStatusFlag;
    public uint BatteryLifeTime;
    public uint BatteryFullLifeTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAP
{
    public int bmType;
    public int bmWidth;
    public int bmHeight;
    public int bmWidthBytes;
    public ushort bmPlanes;
    public ushort bmBitsPixel;
    public nint bmBits;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DWM_THUMBNAIL_PROPERTIES
{
    public uint Flags;
    public RECT Destination;
    public RECT Source;
    public byte Opacity;
    public int Visible;
    public int SourceClientAreaOnly;
}

internal static partial class Win32
{
    // The work-area value of WM_SETTINGCHANGE. Generating it pulls the whole system-parameters catalog.
    public const nint SPI_SETWORKAREA = 0x002F;
    public const uint SPI_GETHIGHCONTRAST = 0x0042;
    public const uint SPI_GETMESSAGEDURATION = 0x2016;
    public const uint HCF_HIGHCONTRASTON = 0x0001;

    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITORINFOF_PRIMARY = 1;

    public const uint ABM_NEW = 0;
    public const uint ABM_REMOVE = 1;
    public const uint ABM_QUERYPOS = 2;
    public const uint ABM_SETPOS = 3;
    public const uint ABM_GETSTATE = 4;
    public const uint ABM_GETTASKBARPOS = 5;
    public const uint ABE_LEFT = 0;
    public const uint ABE_TOP = 1;
    public const uint ABE_RIGHT = 2;
    public const uint ABE_BOTTOM = 3;
    public const uint ABN_POSCHANGED = 1;
    public const uint ABS_AUTOHIDE = 1;

    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_SYSTEM_DESKTOPSWITCH = 0x0020;
    public const uint EVENT_OBJECT_CREATE = 0x8000;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_SHOW = 0x8002;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_REORDER = 0x8004;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
    public const uint EVENT_OBJECT_CLOAKED = 0x8017;
    public const uint EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const int OBJID_WINDOW = 0;
    public const int OBJID_CLIENT = -4;
    public const int CHILDID_SELF = 0;

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    public const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

    public const uint GMEM_MOVEABLE = 0x0002;

    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    public const uint IMAGE_ICON = 1;
    public const int IDI_APPLICATION = 32512;
    public const int SM_CXICON = 11;
    public const int SM_CYICON = 12;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;

    public const int IDC_ARROW = 32512;

    public const int RGN_OR = 2;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool SystemParametersInfoW(uint action, uint param, void* value, uint winIni);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachThreadInput(uint idAttach, uint idAttachTo,
        [MarshalAs(UnmanagedType.Bool)] bool attach);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int key);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hwnd, int id);

    [LibraryImport("user32.dll")]
    public static partial nint LoadCursorW(nint instance, nint name);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
    public static partial nint LoadImageW(nint instance, nint name, uint type, int width, int height, uint flags);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadImageW(nint instance, string name, uint type, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    // LibraryImport will not marshal a struct that holds CsWin32's RECT: those fields are internal.
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(nint monitor, ref MONITORINFOEXW info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool EnumDisplayMonitors(nint hdc, nint clip,
        delegate* unmanaged<nint, nint, global::Windows.Win32.Foundation.RECT*, nint, int> callback, nint data);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayDevicesW(string? device, uint index, ref DISPLAY_DEVICEW info, uint flags);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    // LibraryImport will not marshal a struct that holds CsWin32's RECT: those fields are internal.
    [DllImport("shell32.dll")]
    public static extern nint SHAppBarMessage(uint message, ref APPBARDATA data);

    [LibraryImport("user32.dll")]
    public static partial int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    public static partial int CombineRgn(nint dest, nint src1, nint src2, int mode);

    [LibraryImport("gdi32.dll")]
    public static partial int GetObjectW(nint handle, int size, out BITMAP bitmap);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int attribute, in int value, int size);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int attribute, in uint value, int size);

    // LibraryImport will not marshal a struct that holds CsWin32's RECT: those fields are internal.
    [DllImport("dwmapi.dll")]
    public static extern int DwmUpdateThumbnailProperties(nint thumbnail, in DWM_THUMBNAIL_PROPERTIES properties);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmExtendFrameIntoClientArea(nint hwnd, in MARGINS margins);

    [LibraryImport("shell32.dll")]
    public static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? name);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CommandLineToArgvW(string commandLine, out int count);

    [LibraryImport("kernel32.dll")]
    public static partial nint LocalFree(nint memory);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial SafeFileHandle CreateFileW(string fileName, uint access, uint share, nint security,
        uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetNamedPipeServerProcessId(nint pipe, out uint processId);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetTickCount();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint PowerCreateRequest(ref REASON_CONTEXT context);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PowerSetRequest(nint request, int type);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PowerClearRequest(nint request, int type);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetLastInputInfo(ref LASTINPUTINFO info);

    [LibraryImport("user32.dll")]
    public static unsafe partial nint SetWindowsHookExW(int hook, delegate* unmanaged<int, nint, nint, nint> proc,
        nint module, uint thread);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    public static unsafe partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module,
        delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void> callback, uint processId, uint threadId,
        uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    public static partial nint WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    public static partial nint SetClipboardData(uint format, nint data);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalLock(nint mem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint mem);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    public static int LowWord(nint value)
    {
        return (short)((long)value & 0xFFFF);
    }

    public static int HighWord(nint value)
    {
        return (short)(((long)value >> 16) & 0xFFFF);
    }
}
