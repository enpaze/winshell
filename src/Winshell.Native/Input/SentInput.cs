using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

/// <summary>One send of this process's own input: from just before it to just after it, and the last input before it.</summary>
/// <param name="Start">The <c>GetTickCount</c> time just before the send.</param>
/// <param name="End">The <c>GetTickCount</c> time just after it.</param>
/// <param name="Before">What <c>GetLastInputInfo</c> said just before it, which the send then hid.</param>
internal readonly record struct Send(uint Start, uint End, uint Before);

/// <summary>
/// When this process last sent input of its own, so a check for the user's input can tell theirs apart:
/// <c>GetLastInputInfo</c> counts both, and keeps only the newest. Each send keeps what it hid, so the user's input just
/// before it, such as the hotkey a focus takeover answers, is still seen.
/// </summary>
internal static unsafe class SentInput
{
    /// <summary>How long a send is kept: longer than any check for the user's input looks back.</summary>
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
        var start = Win32.GetTickCount();
        var before = LastInput();
        try
        {
            return send();
        }
        finally
        {
            var end = Win32.GetTickCount();
            lock (Gate)
            {
                // Without the input before it, the send could hide anything: count that as just before the send.
                Sends.Enqueue(new Send(start, end, before ?? start));
                while (Sends.Count > Most || unchecked(end - Sends.Peek().End) > KeptFor)
                    Sends.Dequeue();
            }
        }
    }
}
