using System.Globalization;

namespace Winshell;

internal sealed record ShellArguments(
    int? After,
    bool NoDuplicate,
    bool Daemonize,
    IReadOnlyList<string> Kept,
    IReadOnlyList<string> Detached,
    bool Verbose)
{
    public IReadOnlyList<string> ForNext(int pid)
    {
        return [CommandWords.After.Long, pid.ToString(CultureInfo.InvariantCulture), .. Kept];
    }
}
