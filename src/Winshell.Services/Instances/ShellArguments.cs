using System.Globalization;

namespace Winshell;

/// <summary>
/// What a configuration's program takes from its own command line. Winshell's words are these, and a <c>--</c> ends
/// them, so anything else on the line is the program's. <c>--after</c> is for this start only: a reload names the
/// process it replaces instead. <c>-d</c> is for this start only too: the process that takes over does not detach again.
/// </summary>
internal sealed record ShellArguments(
    int? After,
    bool NoDuplicate,
    bool Daemonize,
    IReadOnlyList<string> Kept,
    IReadOnlyList<string> Detached,
    bool Verbose)
{
    /// <summary>The arguments of the process that takes over from <paramref name="pid"/>. <c>--after</c> comes first, before any <c>--</c>.</summary>
    public IReadOnlyList<string> ForNext(int pid)
    {
        return [CommandWords.After.Long, pid.ToString(CultureInfo.InvariantCulture), .. Kept];
    }
}
