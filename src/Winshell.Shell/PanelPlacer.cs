using System.Collections.Concurrent;
using Winshell.Native;

namespace Winshell;

internal sealed record PanelPlace(
    ScreenInfo? Screen,
    Edges Anchors,
    double MarginLeft,
    double MarginTop,
    double MarginRight,
    double MarginBottom,
    double Width,
    double Height,
    bool CoversScreen,
    double ReservedSize,
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
        var edge = EdgeFor(p.Anchors);
        var size = (int)Math.Round(p.ReservedSize * scale);
        RECT area;
        if (p.CoversScreen)
        {
            Remove();
            area = found.Bounds;
        }
        else if (edge is { } side && size > 0 && _visible)
        {
            area = Reserve(side, size, found.Bounds);
        }
        else
        {
            Remove();
            area = found.Area;
        }

        var rect = Anchor(area, p, scale);
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

    private static Edges? EdgeFor(Edges anchors)
    {
        var horizontal = anchors.HasFlag(Edges.Left) ^ anchors.HasFlag(Edges.Right);
        var vertical = anchors.HasFlag(Edges.Top) ^ anchors.HasFlag(Edges.Bottom);
        if (vertical && !horizontal)
            return anchors.HasFlag(Edges.Top) ? Edges.Top : Edges.Bottom;
        if (horizontal && !vertical)
            return anchors.HasFlag(Edges.Left) ? Edges.Left : Edges.Right;
        return null;
    }

    private static RECT Strip(Edges edge, int thickness, RECT offered)
    {
        return edge switch
        {
            Edges.Bottom => offered with { top = offered.bottom - thickness },
            Edges.Left => offered with { right = offered.left + thickness },
            Edges.Right => offered with { left = offered.right - thickness },
            _ => offered with { bottom = offered.top + thickness }
        };
    }

    private static RECT Area(Edges edge, RECT granted, RECT monitor)
    {
        return edge switch
        {
            Edges.Bottom => monitor with { bottom = granted.bottom, left = granted.left, right = granted.right },
            Edges.Left => monitor with { left = granted.left, top = granted.top, bottom = granted.bottom },
            Edges.Right => monitor with { right = granted.right, top = granted.top, bottom = granted.bottom },
            _ => monitor with { top = granted.top, left = granted.left, right = granted.right }
        };
    }

    private static RECT Anchor(RECT area, PanelPlace p, double scale)
    {
        var ml = (int)Math.Round(p.MarginLeft * scale);
        var mr = (int)Math.Round(p.MarginRight * scale);
        var mt = (int)Math.Round(p.MarginTop * scale);
        var mb = (int)Math.Round(p.MarginBottom * scale);
        var w = (int)Math.Round(p.Width * scale);
        var h = (int)Math.Round(p.Height * scale);
        var (x, width) = Span(area.left, area.Width, p.Anchors.HasFlag(Edges.Left), p.Anchors.HasFlag(Edges.Right), ml,
            mr, w);
        var (y, height) = Span(area.top, area.Height, p.Anchors.HasFlag(Edges.Top), p.Anchors.HasFlag(Edges.Bottom), mt,
            mb, h);
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

    private RECT Reserve(Edges edge, int thickness, RECT monitor)
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

    private static AppBarEdge Side(Edges edge)
    {
        return edge switch
        {
            Edges.Left => AppBarEdge.Left,
            Edges.Right => AppBarEdge.Right,
            Edges.Bottom => AppBarEdge.Bottom,
            _ => AppBarEdge.Top
        };
    }
}
