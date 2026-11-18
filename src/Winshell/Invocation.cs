using System.Collections.Immutable;

namespace Winshell;

internal sealed class Invocation
{
    private readonly Dictionary<CommandOption, object?> _values;

    public Invocation(
        string? command,
        bool showHelp,
        bool showVersion,
        bool shellStartAllowed,
        ImmutableArray<string> arguments,
        ImmutableArray<string> positional,
        ImmutableArray<string> rest,
        Dictionary<CommandOption, object?>? values = null,
        Func<Invocation, Task<int>>? run = null)
    {
        Command = command;
        ShowHelp = showHelp;
        ShowVersion = showVersion;
        ShellStartAllowed = shellStartAllowed;
        Arguments = arguments;
        Positional = positional;
        Rest = rest;
        _values = values ?? [];
        Run = run;
    }

    public string? Command { get; }

    public bool ShowHelp { get; }

    public bool ShowVersion { get; }

    public bool ShellStartAllowed { get; }

    public bool Verbose => IsSet(CliFlags.Verbose);

    public Func<Invocation, Task<int>>? Run { get; }

    public ImmutableArray<string> Arguments { get; }

    public ImmutableArray<string> Positional { get; }

    public ImmutableArray<string> Rest { get; }

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
