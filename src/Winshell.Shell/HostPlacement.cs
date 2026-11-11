using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;

namespace Winshell;

/// <summary>
/// Where a native window is and whether it shows: its scale, size and place, its z-order, and the parts of it that
/// take clicks. Where its kind puts it is its <see cref="HostWindow.Placer"/>.
/// </summary>
internal sealed class HostPlacement(HostWindow host, ShellPlatform platform)
{
    private bool _visible;
    private bool _clickThrough;
    private Point _origin;
    private string _screenId = "";

    public bool IsVisible => _visible;

    public Point Origin => _origin;

    public double Scale { get; internal set; } = 1;

    /// <summary>Whether <see cref="Move"/> is placing the window, so the messages it sends come from that placement.</summary>
    internal bool Placing { get; private set; }

    private nint Handle => host.Handle;

    public (int X, int Y) MapToScreen(double x, double y)
    {
        var pt = new System.Drawing.Point((int)Math.Round(x * Scale), (int)Math.Round(y * Scale));
        PInvoke.ClientToScreen((HWND)Handle, ref pt);
        return (pt.X, pt.Y);
    }

    /// <summary>Whether a point on the virtual desktop, in physical pixels, is inside this window.</summary>
    internal bool ContainsScreenPoint(POINT pt)
    {
        PInvoke.GetWindowRect((HWND)Handle, out var r);
        return pt.X >= r.left && pt.X < r.right && pt.Y >= r.top && pt.Y < r.bottom;
    }

    /// <summary>Moves the window to a rectangle in physical pixels, then tells its client the size and place it got.</summary>
    internal void Move(RECT rect, nint after, SET_WINDOW_POS_FLAGS flags)
    {
        // Moving onto a monitor of another DPI sends WM_DPICHANGED from inside SetWindowPos; this rect is already right for it.
        Placing = true;
        try
        {
            PInvoke.SetWindowPos((HWND)Handle, (HWND)after, rect.left, rect.top, rect.Width, rect.Height, flags);
        }
        finally
        {
            Placing = false;
        }

        UpdateSize();
    }

    internal void SetExStyle(uint flag, bool on)
    {
        var ex = (uint)PInvoke.GetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var next = on ? ex | flag : ex & ~flag;
        if (next != ex)
            PInvoke.SetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)next);
    }

    internal void UpdateSize()
    {
        PInvoke.GetClientRect((HWND)Handle, out var rc);
        var origin = new System.Drawing.Point();
        PInvoke.ClientToScreen((HWND)Handle, ref origin);
        var monitor = Screens.Of(Handle);
        var device = monitor?.Id ?? "";
        _origin = DesktopSpace.ToLogical(new POINT { X = origin.X, Y = origin.Y }, monitor?.Bounds ?? default, Scale);
        var width = Math.Max(1, rc.Width);
        var height = Math.Max(1, rc.Height);
        host.Presentation.FitIsland(width, height);
        host.Client.OnResized(width / Scale, height / Scale, Scale);
        host.Client.OnMoved();
        if (device != _screenId)
        {
            _screenId = device;
            host.Client.OnScreenChanged(device);
        }
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible)
            return;
        _visible = visible;
        if (visible)
        {
            host.Placer.Show();
            host.Focus.Show();
            // Showing a window can put it above the windows it owns. Put those back above it.
            if (!PInvoke.IsIconic((HWND)Handle))
                OwnedWindows.Raise(Handle, platform.Windows);
        }
        else
        {
            host.Placer.Hide();
            PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_HIDE);
            host.Focus.Release();
        }
    }

    public void SetInputRegion(IReadOnlyList<Rect>? rects)
    {
        if (rects is null)
        {
            Win32.SetWindowRgn(Handle, 0, true);
            SetClickThrough(false);
            return;
        }

        if (rects.Count == 0)
        {
            // Nothing takes input: let every click through, but keep drawing.
            Win32.SetWindowRgn(Handle, 0, true);
            SetClickThrough(true);
            return;
        }

        SetClickThrough(false);
        var region = Win32.CreateRectRgn(0, 0, 0, 0);
        foreach (var r in rects)
        {
            var part = Win32.CreateRectRgn((int)Math.Floor(r.X * Scale), (int)Math.Floor(r.Y * Scale),
                (int)Math.Ceiling(r.Right * Scale), (int)Math.Ceiling(r.Bottom * Scale));
            _ = Win32.CombineRgn(region, region, part, Win32.RGN_OR);
            PInvoke.DeleteObject(new HGDIOBJ(part));
        }

        // The window owns the region once it is set.
        Win32.SetWindowRgn(Handle, region, true);
    }

    private void SetClickThrough(bool on)
    {
        if (_clickThrough == on)
            return;
        _clickThrough = on;
        SetExStyle((uint)(WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_LAYERED), on);
        if (on)
            PInvoke.SetLayeredWindowAttributes((HWND)Handle, default, 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
    }

    public void Raise()
    {
        PInvoke.SetWindowPos((HWND)Handle, HWND.HWND_TOP, 0, 0, 0, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                   SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE));
    }

    public void StartSystemMove()
    {
        Win32.ReleaseCapture();
        PInvoke.SendMessage((HWND)Handle, PInvoke.WM_SYSCOMMAND, (WPARAM)(nuint)(PInvoke.SC_MOVE | 2), 0);
    }

    public void StartSystemResize(Edges edges)
    {
        // SC_SIZE + WMSZ_* edge code.
        var code = edges switch
        {
            Edges.Left => 1,
            Edges.Right => 2,
            Edges.Top => 3,
            Edges.Top | Edges.Left => 4,
            Edges.Top | Edges.Right => 5,
            Edges.Bottom => 6,
            Edges.Bottom | Edges.Left => 7,
            _ => 8
        };
        Win32.ReleaseCapture();
        PInvoke.SendMessage((HWND)Handle, PInvoke.WM_SYSCOMMAND, (WPARAM)(nuint)(PInvoke.SC_SIZE + code), 0);
    }

    /// <summary>Gives back what the window holds while it is placed: a popup's grab on clicks and a panel's edge.</summary>
    internal void Release()
    {
        host.Focus.Release();
        host.Placer.Release();
    }
}
