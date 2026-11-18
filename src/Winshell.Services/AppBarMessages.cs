using System.Collections.Concurrent;
using Winshell.Native;

namespace Winshell;

internal static class AppBarMessages
{
    private static readonly ConcurrentDictionary<object, (Func<bool> Lower, Action Raise)> Trays = new();

    internal static void Opened(object tray, Func<bool> lower, Action raise)
    {
        Trays[tray] = (lower, raise);
    }

    internal static void Closed(object tray)
    {
        Trays.TryRemove(tray, out _);
    }

    internal static bool Register(nint window, uint callback)
    {
        return Send(() => AppBars.Register(window, callback));
    }

    internal static RECT Query(nint window, AppBarEdge edge, RECT proposed)
    {
        return Send(() => AppBars.Query(window, edge, proposed));
    }

    internal static RECT Reserve(nint window, AppBarEdge edge, RECT desired)
    {
        return Send(() => AppBars.Reserve(window, edge, desired));
    }

    internal static bool Remove(nint window)
    {
        return Send(() => AppBars.Remove(window));
    }

    internal static bool Autohide(nint window)
    {
        return Send(() => AppBars.Autohide(window));
    }

    internal static uint State()
    {
        return Send(AppBars.State);
    }

    internal static void SetState(uint state)
    {
        Send(() =>
        {
            AppBars.SetState(state);
            return true;
        });
    }

    internal static bool TryPosition(nint window, out AppBarEdge edge, out RECT bounds)
    {
        var (found, foundEdge, foundBounds) = Send(() =>
            (AppBars.TryPosition(window, out var e, out var b), e, b));
        edge = foundEdge;
        bounds = foundBounds;
        return found;
    }

    private static T Send<T>(Func<T> message)
    {
        List<object> lowered = [];
        foreach (var (tray, step) in Trays)
            if (step.Lower())
                lowered.Add(tray);
        try
        {
            return message();
        }
        finally
        {
            foreach (var tray in lowered)
                if (Trays.TryGetValue(tray, out var step))
                    step.Raise();
        }
    }
}
