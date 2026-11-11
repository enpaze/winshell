using System.Collections.Immutable;

namespace Winshell;

/// <summary>
/// One read of a command line: which command, the words left over, and the flags that command set. A flag another
/// command takes is not a field here.
/// </summary>
internal sealed class Invocation
{
    private readonly Dictionary<CommandOption, object?> _values;

    public Invocation(
        string? command,
        bool showHelp,
        bool showVersion,
        bool shellStartAllowed,
        bool verbose,
        ImmutableArray<string> arguments,
        ImmutableArray<string> positional,
        ImmutableArray<string> rest,
        ImmutableArray<string> childArguments,
        Dictionary<CommandOption, object?>? values = null)
    {
        Command = command;
        ShowHelp = showHelp;
        ShowVersion = showVersion;
        ShellStartAllowed = shellStartAllowed;
        Verbose = verbose;
        Arguments = arguments;
        Positional = positional;
        Rest = rest;
        ChildArguments = childArguments;
        _values = values ?? [];
    }

    public string? Command { get; }

    public bool ShowHelp { get; }

    public bool ShowVersion { get; }

    /// <summary>A line with no command word whose flags are all ones <c>run</c> takes.</summary>
    public bool ShellStartAllowed { get; }

    public bool Verbose { get; }

    public ImmutableArray<string> Arguments { get; }

    public ImmutableArray<string> Positional { get; }

    /// <summary>The <c>--</c> and everything after it. Those words belong to the configuration's program.</summary>
    public ImmutableArray<string> Rest { get; }

    /// <summary>
    /// Arguments for the background <c>winshell</c> that <c>run -d</c> leaves behind. Empty unless this line daemonizes.
    /// The flag itself is gone, and a <c>-d</c> that is the value of <c>-c</c> or <c>-p</c> is still there.
    /// </summary>
    public ImmutableArray<string> ChildArguments { get; }

    public bool IsSet(CommandOption option)
    {
        return _values.ContainsKey(option);
    }

    public string? Text(CommandOption option)
    {
        return _values.TryGetValue(option, out var value) ? value as string : null;
    }

    public int? Number(CommandOption option)
    {
        return _values.TryGetValue(option, out var value) && value is int number ? number : null;
    }

    public TimeSpan? Time(CommandOption option)
    {
        return _values.TryGetValue(option, out var value) && value is TimeSpan time ? time : null;
    }

    public InstanceChoice Instance =>
        new(Number(CliFlags.Pid), Text(CliFlags.Id), Text(CliFlags.Path), Text(CliFlags.Config));
}
