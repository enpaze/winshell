using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

internal readonly record struct Send(uint Start, uint End, uint Before);

internal static unsafe class SentInput
{
    private const uint OwnInputSlack = 50;

    internal static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    private const uint KeptFor = 10_000;

    private const int Most = 256;
    private static readonly Lock Gate = new();
    private static readonly Queue<Send> Sends = new();

    internal static IReadOnlyList<Send> Recent
    {
        get
        {
            lock (Gate)
            {
                return [.. Sends];
            }
        }
    }

    internal static uint? LastInput()
    {
        var info = new LASTINPUTINFO { cbSize = 8 };
        return PInvoke.GetLastInputInfo(ref info) ? info.dwTime : null;
    }

    internal static double? SecondsSinceLastUse()
    {
        if (LastInput() is not { } last)
            return null;
        return unchecked(InputClock() - LastPersonUse(last, Recent)) / 1000.0;
    }

    internal static bool? UsedWithinQuiet()
    {
        if (LastInput() is not { } last)
            return null;
        return WithinQuiet(InputClock(), last, Recent);
    }

    internal static bool WithinQuiet(uint now, uint lastInput, IReadOnlyList<Send> sent)
    {
        return unchecked(now - LastPersonUse(lastInput, sent)) < Quiet.TotalMilliseconds;
    }

    internal static uint LastPersonUse(uint lastInput, IReadOnlyList<Send> sent)
    {
        var stamp = lastInput;
        for (var step = 0; step <= sent.Count; step++)
        {
            var own = sent.Where(send => unchecked((int)(stamp - send.Start)) >= 0 &&
                                         unchecked((int)(stamp - send.End)) <= OwnInputSlack)
                .Select(send => (Send?)send).FirstOrDefault();
            if (own is not { } hid)
                break;
            stamp = hid.Before;
        }

        return stamp;
    }

    internal static bool Send(INPUT[] inputs, int releasesFrom)
    {
        var taken = Around(() => Inject(inputs));
        if (taken == inputs.Length)
            return true;
        var rest = inputs[Math.Max(taken, releasesFrom)..];
        if (rest.Length > 0)
            Around(() => Inject(rest));
        return false;
    }

    private static int Inject(INPUT[] inputs)
    {
        fixed (INPUT* first = inputs)
        {
            return (int)PInvoke.SendInput((uint)inputs.Length, first, sizeof(INPUT));
        }
    }

    internal static T Around<T>(Func<T> send)
    {
        var start = InputClock();
        var before = LastInput();
        try
        {
            return send();
        }
        finally
        {
            var end = InputClock();
            lock (Gate)
            {
                Sends.Enqueue(new Send(start, end, before ?? start));
                while (Sends.Count > Most || unchecked(end - Sends.Peek().End) > KeptFor)
                    Sends.Dequeue();
            }
        }
    }

    private static uint InputClock()
    {
        return PInvoke.GetTickCount();
    }
}
