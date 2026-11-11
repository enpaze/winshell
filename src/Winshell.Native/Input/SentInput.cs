using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

/// <summary>One send of this process's own input: from just before it to just after it, and the last input before it.</summary>
/// <param name="Start">The input-clock time just before the send.</param>
/// <param name="End">The input-clock time just after it.</param>
/// <param name="Before">What <c>GetLastInputInfo</c> said just before it, which the send then hid.</param>
internal readonly record struct Send(uint Start, uint End, uint Before);

/// <summary>
/// When the person last used the PC, not counting input this process sent, on Windows' input clock.
/// <c>GetLastInputInfo</c> counts both and keeps only the newest. Each send keeps what it hid, so the person's input
/// just before it, such as the hotkey a focus takeover answers, is still seen.
/// </summary>
internal static unsafe class SentInput
{
    /// <summary>How late Windows may stamp input this sent, after <c>SendInput</c> returned: a few clock ticks.</summary>
    private const uint OwnInputSlack = 50;

    /// <summary>How long the PC must have gone without the person's input before real input will send.</summary>
    internal static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    /// <summary>How long a send is kept. Longer than <see cref="Quiet"/>, the look-back this type applies.</summary>
    private const uint KeptFor = 10_000;

    private const int Most = 256;
    private static readonly Lock Gate = new();
    private static readonly Queue<Send> Sends = new();

    /// <summary>The sends of the last few seconds, oldest first.</summary>
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

    /// <summary>What <c>GetLastInputInfo</c> says, or null when Windows won't say.</summary>
    internal static uint? LastInput()
    {
        var info = new LASTINPUTINFO { cbSize = 8 };
        return PInvoke.GetLastInputInfo(ref info) ? info.dwTime : null;
    }

    /// <summary>
    /// Seconds since the person last used the PC, not counting input this process sent. Null when Windows won't say.
    /// This is the one read of <c>GetTickCount</c>: last-input time is measured on that clock, and the record of this
    /// process's own sends uses the same read.
    /// </summary>
    internal static double? SecondsSinceLastUse()
    {
        if (LastInput() is not { } last)
            return null;
        return unchecked(InputClock() - LastPersonUse(last, Recent)) / 1000.0;
    }

    /// <summary>
    /// Whether the person used the PC within <see cref="Quiet"/>, not counting input this process sent.
    /// Null when Windows won't say.
    /// </summary>
    internal static bool? UsedWithinQuiet()
    {
        if (LastInput() is not { } last)
            return null;
        return WithinQuiet(InputClock(), last, Recent);
    }

    /// <summary>
    /// Whether <paramref name="lastInput"/>, walked back through <paramref name="sent"/>, falls inside <see cref="Quiet"/>.
    /// The times are input-clock milliseconds, which wrap and step about every 16.
    /// </summary>
    internal static bool WithinQuiet(uint now, uint lastInput, IReadOnlyList<Send> sent)
    {
        return unchecked(now - LastPersonUse(lastInput, sent)) < Quiet.TotalMilliseconds;
    }

    /// <summary>
    /// The input-clock time of the person's last use. <paramref name="lastInput"/> is what <c>GetLastInputInfo</c> says.
    /// A stamp that falls in one of <paramref name="sent"/>, oldest first, is that send's, so the input that counts is
    /// the one the send hid, and so on back. The send that made a stamp is the first one it falls in: a later send in
    /// the same clock tick hid that stamp itself.
    /// </summary>
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

    /// <summary>
    /// Sends <paramref name="inputs"/> as one injection that nothing else can split. When Windows takes only some, it
    /// sends the ones from <paramref name="releasesFrom"/> on that it left, the releases, so no key or button stays held.
    /// True when it took them all.
    /// </summary>
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

    /// <summary>Runs <paramref name="send"/> and remembers when, so what Windows stamps meanwhile counts as this process's.</summary>
    internal static T Around<T>(Func<T> send)
    {
        // The start first: input between the two reads then counts as what the send hid, not as the send.
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
                // Without the input before it, the send could hide anything: count that as just before the send.
                Sends.Enqueue(new Send(start, end, before ?? start));
                while (Sends.Count > Most || unchecked(end - Sends.Peek().End) > KeptFor)
                    Sends.Dequeue();
            }
        }
    }

    /// <summary>The Windows input clock. This is the one <c>GetTickCount</c> read in <c>src/</c>.</summary>
    private static uint InputClock()
    {
        return Win32.GetTickCount();
    }
}
