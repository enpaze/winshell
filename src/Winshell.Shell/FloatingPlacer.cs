using Windows.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Winshell.Native;
using Winshell.AppWindows;

namespace Winshell;

internal sealed record FloatingPlace(
    ScreenInfo? Screen,
    string Title,
    double Width,
    double Height,
    Size MinimumSize,
    Size MaximumSize,
    bool Maximized,
    bool Minimized,
    bool Fullscreen,
    HostWindow? Owner,
    bool StateRequested);

internal sealed class FloatingPlacer(HostWindow host, ShellPlatform platform, FloatingWindow window) : IWindowPlacer
{
    private FloatingPlace? _place;
    private bool _placed;
    private bool _fullscreen;
    private bool _applying;
    private SavedFloatingPlace? _saved;
    private double _lastWidth = -1, _lastHeight = -1;

    private nint Handle => host.Handle;

    public (uint Style, uint ExStyle) Styles => (
        (uint)(WINDOW_STYLE.WS_OVERLAPPEDWINDOW | WINDOW_STYLE.WS_CLIPCHILDREN),
        (uint)(WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_APPWINDOW));

    internal void Set(FloatingPlace place)
    {
        if (Handle == 0 || host.Released)
            return;
        _place = place;
        _applying = true;
        try
        {
            PInvoke.SetWindowText((HWND)Handle, place.Title);
            OwnedWindows.Link(Handle, place.Owner);
            if (_fullscreen && !place.Fullscreen)
            {
                ExitFullscreen(place);
            }
            else if (!_fullscreen && place.Fullscreen)
            {
                ApplyFrame(place);
                EnterFullscreen();
            }
            else if (_fullscreen)
            {
                _lastWidth = place.Width;
                _lastHeight = place.Height;
                if (place.Minimized && !PInvoke.IsIconic((HWND)Handle))
                {
                    PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_MINIMIZE);
                }
                else if (!place.Minimized && PInvoke.IsIconic((HWND)Handle))
                {
                    PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_RESTORE);
                    FillMonitor();
                }
                else if (!PInvoke.IsIconic((HWND)Handle))
                {
                    FillMonitor();
                }
            }
            else
            {
                ApplyFrame(place);
            }

            if (!PInvoke.IsIconic((HWND)Handle))
                OwnedWindows.Raise(Handle, platform.Windows);
            ReportState();
        }
        finally
        {
            _applying = false;
        }
    }

    public nint? OnMessage(WindowMessage message)
    {
        switch (message.Id)
        {
            case PInvoke.WM_SIZE or PInvoke.WM_MOVE:
                if (!host.Place.Placing && !host.Released && !_applying)
                    ReportState();
                return null;
            case PInvoke.WM_GETMINMAXINFO:
                return OnGetMinMaxInfo(ref message.MinMaxInfo);
            case PInvoke.WM_DISPLAYCHANGE:
                if (_fullscreen)
                    FillMonitor();
                return null;
            default:
                return null;
        }
    }

    public void OnDpiChanged(RECT suggested, bool placing)
    {
        if (_fullscreen)
        {
            if (!placing)
                FillMonitor();
        }
        else
        {
            PInvoke.SetWindowPos((HWND)Handle, HWND.HWND_TOP, suggested.left, suggested.top, suggested.Width, suggested.Height,
                (SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE));
        }
    }

    private nint? OnGetMinMaxInfo(ref MINMAXINFO info)
    {
        if (_fullscreen && Handle != 0)
        {
            if (Screens.Of(Handle)?.Bounds is not { Width: > 0, Height: > 0 } bounds)
                return null;
            info.ptMaxPosition.X = bounds.left;
            info.ptMaxPosition.Y = bounds.top;
            info.ptMaxSize.X = bounds.Width;
            info.ptMaxSize.Y = bounds.Height;
            info.ptMaxTrackSize.X = bounds.Width;
            info.ptMaxTrackSize.Y = bounds.Height;
            return 0;
        }

        if (_place is not { } f)
            return null;
        if (f.MinimumSize.Width > 0)
            info.ptMinTrackSize.X = (int)(f.MinimumSize.Width * host.Place.Scale);
        if (f.MinimumSize.Height > 0)
            info.ptMinTrackSize.Y = (int)(f.MinimumSize.Height * host.Place.Scale);
        if (f.MaximumSize.Width > 0)
            info.ptMaxTrackSize.X = (int)(f.MaximumSize.Width * host.Place.Scale);
        if (f.MaximumSize.Height > 0)
            info.ptMaxTrackSize.Y = (int)(f.MaximumSize.Height * host.Place.Scale);
        return 0;
    }

    private void ApplyFrame(FloatingPlace place)
    {
        var first = !_placed;
        var widthChanged = place.Width != _lastWidth;
        var heightChanged = place.Height != _lastHeight;
        if (first || widthChanged || heightChanged)
        {
            _lastWidth = place.Width;
            _lastHeight = place.Height;
            var dpi = PInvoke.GetDpiForWindow((HWND)Handle);
            host.Place.Scale = dpi / 96.0;
            var client = new RECT(0, 0, (int)Math.Round(place.Width * host.Place.Scale),
                (int)Math.Round(place.Height * host.Place.Scale));
            var (style, ex) = Styles;
            PInvoke.AdjustWindowRectExForDpi(ref client, (WINDOW_STYLE)style, false, (WINDOW_EX_STYLE)ex, dpi);
            int x, y;
            if (first)
            {
                var monitor = place.Screen is { } screen ? Screens.Find(screen.Id) : Screens.Primary();
                var area = monitor?.Area ?? default;
                x = area.left + (area.Width - client.Width) / 2;
                y = area.top + (area.Height - client.Height) / 2;
            }
            else
            {
                PInvoke.GetWindowRect((HWND)Handle, out var current);
                x = current.left;
                y = current.top;
            }

            PInvoke.SetWindowPos((HWND)Handle, HWND.HWND_TOP, x, y, client.Width, client.Height,
                (SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE));
            _placed = true;
        }

        if (host.Place.IsVisible)
        {
            if (place.Minimized && !PInvoke.IsIconic((HWND)Handle))
                PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_MINIMIZE);
            else if (place.Maximized && !PInvoke.IsZoomed((HWND)Handle))
                PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED);
            else if (place is { Maximized: false, Minimized: false } &&
                     (PInvoke.IsZoomed((HWND)Handle) || PInvoke.IsIconic((HWND)Handle)))
                PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_RESTORE);
        }
    }

    private void EnterFullscreen()
    {
        _saved = CapturePlace();
        _fullscreen = true;
        host.Presentation.SetFullscreen(true);
        SetStyle(BorderlessStyle());
        if (PInvoke.IsIconic((HWND)Handle) || PInvoke.IsZoomed((HWND)Handle))
            PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_RESTORE);
        FillMonitor();
    }

    private void ExitFullscreen(FloatingPlace place)
    {
        var saved = _saved;
        _fullscreen = false;
        host.Presentation.SetFullscreen(false);
        _saved = null;
        _lastWidth = place.Width;
        _lastHeight = place.Height;
        if (saved is null)
        {
            ApplyFrame(place);
            return;
        }

        SetStyle(saved.Style);
        Reframe();
        var monitor = Screens.Find(saved.MonitorId) ?? Screens.Of(Handle);
        var scale = host.Place.Scale > 0 ? host.Place.Scale : saved.Scale;
        if (monitor is not null && saved.Matches(monitor.Id, monitor.Bounds, scale) && saved.HasPlacement)
        {
            var next = saved.Placement;
            next.showCmd = place.StateRequested
                ? RequestedShow(place)
                : Shown(saved);
            if (place is { StateRequested: true, Maximized: false })
                next.flags &= ~WINDOWPLACEMENT_FLAGS.WPF_RESTORETOMAXIMIZED;
            if (PInvoke.SetWindowPlacement((HWND)Handle, next))
            {
                host.Place.UpdateSize();
                return;
            }
        }

        if (monitor is null)
        {
            ApplyFrame(place);
            return;
        }

        var rect = saved.RectOn(monitor.Bounds, scale);
        PlaceOuter(rect);
        if (!host.Place.IsVisible)
            return;
        var maximized = place.StateRequested ? place.Maximized : saved.Maximized;
        var minimized = place.StateRequested ? place.Minimized : saved.Minimized;
        if (minimized)
            PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_MINIMIZE);
        else if (maximized)
            PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED);
        else if (PInvoke.IsZoomed((HWND)Handle) || PInvoke.IsIconic((HWND)Handle))
            PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_RESTORE);
    }

    private SHOW_WINDOW_CMD Shown(SavedFloatingPlace saved)
    {
        if (!host.Place.IsVisible)
            return SHOW_WINDOW_CMD.SW_HIDE;
        if (saved.Minimized)
            return SHOW_WINDOW_CMD.SW_SHOWMINIMIZED;
        if (saved.Maximized)
            return SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED;
        var show = saved.HasPlacement ? saved.Placement.showCmd : 0;
        return show == 0 ? SHOW_WINDOW_CMD.SW_SHOWNORMAL : show;
    }

    private static SHOW_WINDOW_CMD RequestedShow(FloatingPlace place)
    {
        return place.Minimized ? SHOW_WINDOW_CMD.SW_SHOWMINIMIZED :
            place.Maximized ? SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED : SHOW_WINDOW_CMD.SW_SHOWNORMAL;
    }

    private SavedFloatingPlace CapturePlace()
    {
        var monitor = Screens.Of(Handle);
        var dpi = PInvoke.GetDpiForWindow((HWND)Handle);
        var scale = dpi == 0 ? monitor?.Scale ?? host.Place.Scale : dpi / 96.0;
        if (scale <= 0)
            scale = 1;
        var bounds = monitor?.Bounds ?? default;
        PInvoke.GetWindowRect((HWND)Handle, out var shown);
        var maximized = PInvoke.IsZoomed((HWND)Handle);
        var minimized = PInvoke.IsIconic((HWND)Handle);
        var hasPlacement = TopLevelWindows.TryPlacement(Handle, out var placement);
        var normal = shown;
        if ((maximized || minimized) && hasPlacement)
        {
            var primary = Screens.Primary();
            var tool =
                (PInvoke.GetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0;
            normal = WindowOnScreen.Restored(
                new WindowToMove(shown, placement.rcNormalPosition, true, minimized, maximized, tool),
                primary?.Bounds ?? default, primary?.Work ?? default);
        }

        var logical = bounds.Width > 0
            ? DesktopSpace.ToLogical(normal, bounds, scale)
            : new Rect(0, 0, normal.Width, normal.Height);
        return new SavedFloatingPlace(
            (uint)PInvoke.GetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE),
            scale,
            bounds,
            monitor?.Id ?? "",
            logical.X - bounds.left,
            logical.Y - bounds.top,
            logical.Width,
            logical.Height,
            maximized,
            minimized,
            placement,
            hasPlacement);
    }

    private uint BorderlessStyle()
    {
        var style = (uint)PInvoke.GetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var next = (style & ~(uint)WINDOW_STYLE.WS_OVERLAPPEDWINDOW) |
                   (uint)(WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_CLIPCHILDREN);
        if ((style & (uint)WINDOW_STYLE.WS_VISIBLE) != 0)
            next |= (uint)WINDOW_STYLE.WS_VISIBLE;
        return next;
    }

    private void SetStyle(uint style)
    {
        if (Handle == 0)
            return;
        var current = (uint)PInvoke.GetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        if ((current & (uint)WINDOW_STYLE.WS_VISIBLE) != 0)
            style |= (uint)WINDOW_STYLE.WS_VISIBLE;
        else
            style &= ~(uint)WINDOW_STYLE.WS_VISIBLE;
        if (current == style)
            return;
        PInvoke.SetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (nint)style);
    }

    private void Reframe()
    {
        PInvoke.SetWindowPos((HWND)Handle, HWND.HWND_TOP, 0, 0, 0, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                   SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                   SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED));
    }

    private void FillMonitor()
    {
        if (Handle == 0)
            return;
        if (Screens.Of(Handle)?.Bounds is { Width: > 0, Height: > 0 } bounds)
            PlaceOuter(bounds);
    }

    private void PlaceOuter(RECT rect)
    {
        host.Place.Move(rect, 0,
            (SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
                   SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED));
    }

    private void ReportState()
    {
        if (Handle == 0)
            return;
        var fullscreen = ReadsFullscreen();
        _fullscreen = fullscreen;
        window.OnFullscreenChanged(fullscreen);
        window.OnStateChanged(PInvoke.IsZoomed((HWND)Handle) && !fullscreen, PInvoke.IsIconic((HWND)Handle));
    }

    private bool ReadsFullscreen()
    {
        if (PInvoke.IsIconic((HWND)Handle))
            return _fullscreen;
        if (!PInvoke.GetWindowRect((HWND)Handle, out var rect))
            return _fullscreen;
        if (Screens.Of(Handle)?.Bounds is not { Width: > 0, Height: > 0 } bounds)
            return _fullscreen;
        return WindowRules.CoversMonitor(PInvoke.GetWindowLongPtr((HWND)Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE), rect, bounds);
    }
}
