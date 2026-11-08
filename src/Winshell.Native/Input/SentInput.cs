using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

/// <summary>
/// When <c>winshell inspect</c>'s real input was last sent, as <c>GetTickCount</c> times, so its check for the user's
/// input can tell theirs apart: <c>GetLastInputInfo</c> counts both, and keeps only the newest. Only those sends are
/// kept. Each comes right after that check, so it can't hide the user's input; another send, such as a focus
/// takeover's, could.
/// </summary>
internal static unsafe class SentInput
{
    private const int Kept = 16;
    private static readonly Lock Gate = new();
    private static readonly Queue<(uint Start, uint End)> Sends = new();

    /// <summary>The latest sends, each from just before it to just after it.</summary>
    internal static IReadOnlyList<(uint Start, uint End)> Recent
    {
        get
        {
            lock (Gate)
            {
                return [.. Sends];
            }
        }
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
        var start = Win32.GetTickCount();
        try
        {
            return send();
        }
        finally
        {
            var end = Win32.GetTickCount();
            lock (Gate)
            {
                Sends.Enqueue((start, end));
                if (Sends.Count > Kept)
                    Sends.Dequeue();
            }
        }
    }
}
