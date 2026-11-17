using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

internal sealed class HotkeyTarget : IDisposable
{
    private const string ClassName = "WinshellHotkey";

    private readonly NativeWindow _window;

    private HotkeyTarget(NativeWindow window)
    {
        _window = window;
        Handle = window.Handle;
    }

    private nint Handle { get; }

    internal static HotkeyTarget? Create(Action<int> onHotkey, Action<Exception> failed)
    {
        var window = NativeWindow.TryCreate(ClassName, 0, 0, message =>
        {
            if (message.Id != PInvoke.WM_HOTKEY)
                return null;
            onHotkey((int)message.WParam);
            return null;
        }, failed, true);
        return window is null ? null : new HotkeyTarget(window);
    }

    internal bool Register(int id, uint modifiers, uint virtualKey, out int error)
    {
        var registered = PInvoke.RegisterHotKey((HWND)Handle, id, (HOT_KEY_MODIFIERS)modifiers, virtualKey);
        error = registered ? 0 : Marshal.GetLastPInvokeError();
        return registered;
    }

    internal void Unregister(int id)
    {
        PInvoke.UnregisterHotKey((HWND)Handle, id);
    }

    public void Dispose()
    {
        _window.Release(true);
    }
}
