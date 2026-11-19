using System.Collections.Concurrent;
using Winshell.Native;

namespace Winshell;

internal sealed record PanelPlace(
    ScreenInfo? Screen,
    Edge Edge,
    PanelAlign Align,
    double MarginLeft,
    double MarginTop,
    double MarginRight,
    double MarginBottom,
    double Width,
    double Height,
    bool CoversScreen,
    bool Reserve,
    bool AboveWindows);

internal sealed class PanelPlacer(IPanelDesktop desktop) : IWindowPlacer
{
    private static readonly uint Message = PInvoke.WM_APP + 1;

    private static readonly ConcurrentDictionary<IPanelDesktop, byte> Held = new();

    private readonly AppBarReservation _reservation = new();
    private PanelPlace? _place;
    private bool _visible;
    private bool _released;
    private bool _registered;
    private bool _placePosted;

    internal void Set(PanelPlace place)
    {
        _place = place;
        Place();
    }

    public void Show()
    {
        _visible = true;
        Place();
    }

    public void Hide()
    {
        _visible = false;
        Remove();
    }

    public void Release()
    {
        _released = true;
        Remove();
    }

    public void OnDpiChanged(RECT suggested, bool placing)
    {
        if (!placing)
            desktop.Post(Place);
    }

    public void OnWorkAreaChanged()
    {
        if (_visible)
            Place();
    }

    private void Place()
    {
        if (_released || _place is not { } p)
            return;
        var monitor = (p.Screen is { } screen ? desktop.Find(screen.Id) : null) ?? desktop.Primary();
        if (monitor is not { } found)
            return;
        var scale = found.Scale;
        var thickness = (int)Math.Round((Horizontal(p.Edge) ? p.Height : p.Width) * scale);
        RECT rect;
        if (p.CoversScreen)
        {
            Remove();
            rect = Anchor(found.Bounds, p, scale, true, true, true, true);
        }
        else if (p.Reserve && thickness > 0 && _visible)
        {
            rect = Anchor(Reserve(p.Edge, thickness, found.Bounds), p, scale);
        }
        else
        {
            Remove();
            rect = Anchor(found.Area, p, scale);
        }

        desktop.Move(rect, p.AboveWindows && !desktop.FullscreenOn(found.Id), scale);
    }

    private void Remove()
    {
        if (!_registered)
            return;
        desktop.Remove();

        _registered = false;
        Held.TryRemove(desktop, out _);
        _reservation.Clear();
        desktop.WorkAreaMayHaveChanged();
    }

    internal static void ReleaseAll()
    {
        foreach (var pair in Held)
            if (Held.TryRemove(pair.Key, out _))
                pair.Key.Remove();
    }

    public nint? OnMessage(WindowMessage message)
    {
        var (msg, wParam) = (message.Id, message.WParam);
        if (msg == TaskbarWindows.Created)
        {
            Remove();
            desktop.Post(Place);
            return 0;
        }

        if (msg != Message)
            return null;
        if (AppBars.PositionChanged(wParam))
        {
            _reservation.NoteExternalChange();
            if (!_placePosted)
            {
                _placePosted = true;
                desktop.Post(() =>
                {
                    _placePosted = false;
                    Place();
                });
            }
        }

        return 0;
    }

    private static bool Horizontal(Edge edge)
    {
        return edge is Edge.Top or Edge.Bottom;
    }

    private static RECT Strip(Edge edge, int thickness, RECT offered)
    {
        return edge switch
        {
            Edge.Bottom => offered with { top = offered.bottom - thickness },
            Edge.Left => offered with { right = offered.left + thickness },
            Edge.Right => offered with { left = offered.right - thickness },
            _ => offered with { bottom = offered.top + thickness }
        };
    }

    private static RECT Area(Edge edge, RECT granted, RECT monitor)
    {
        return edge switch
        {
            Edge.Bottom => monitor with { bottom = granted.bottom, left = granted.left, right = granted.right },
            Edge.Left => monitor with { left = granted.left, top = granted.top, bottom = granted.bottom },
            Edge.Right => monitor with { right = granted.right, top = granted.top, bottom = granted.bottom },
            _ => monitor with { top = granted.top, left = granted.left, right = granted.right }
        };
    }

    private static RECT Anchor(RECT area, PanelPlace p, double scale)
    {
        var start = p.Align is PanelAlign.Stretch or PanelAlign.Start;
        var end = p.Align is PanelAlign.Stretch or PanelAlign.End;
        return Horizontal(p.Edge)
            ? Anchor(area, p, scale, start, p.Edge == Edge.Top, end, p.Edge == Edge.Bottom)
            : Anchor(area, p, scale, p.Edge == Edge.Left, start, p.Edge == Edge.Right, end);
    }

    private static RECT Anchor(RECT area, PanelPlace p, double scale, bool left, bool top, bool right, bool bottom)
    {
        var ml = (int)Math.Round(p.MarginLeft * scale);
        var mr = (int)Math.Round(p.MarginRight * scale);
        var mt = (int)Math.Round(p.MarginTop * scale);
        var mb = (int)Math.Round(p.MarginBottom * scale);
        var w = (int)Math.Round(p.Width * scale);
        var h = (int)Math.Round(p.Height * scale);
        var (x, width) = Span(area.left, area.Width, left, right, ml, mr, w);
        var (y, height) = Span(area.top, area.Height, top, bottom, mt, mb, h);
        return new RECT(x, y, x + Math.Max(1, width), y + Math.Max(1, height));
    }

    private static (int Start, int Length) Span(int start, int length, bool near, bool far, int nearMargin,
        int farMargin, int size)
    {
        if (near && far)
            return (start + nearMargin, length - nearMargin - farMargin);
        if (near)
            return (start + nearMargin, size);
        if (far)
            return (start + length - size - farMargin, size);
        return (start + (length - size) / 2, size);
    }

    private RECT Reserve(Edge edge, int thickness, RECT monitor)
    {
        var side = Side(edge);
        if (!_registered)
        {
            _registered = desktop.Register(Message);
            if (!_registered)
                return monitor;
            Held[desktop] = 0;
        }

        var offered = desktop.Query(side, monitor);
        var moved = _reservation.Reserve(Strip(edge, thickness, offered), desired =>
            desktop.Reserve(side, desired));
        if (moved)
            desktop.WorkAreaMayHaveChanged();
        return Area(edge, _reservation.Granted, monitor);
    }

    private static AppBarEdge Side(Edge edge)
    {
        return edge switch
        {
            Edge.Left => AppBarEdge.Left,
            Edge.Right => AppBarEdge.Right,
            Edge.Bottom => AppBarEdge.Bottom,
            _ => AppBarEdge.Top
        };
    }
}
