namespace Winshell;

internal sealed record CommandEntry
{
    public required string Name { get; init; }

    public string Overview { get; init; } = "";

    public string Usage { get; init; } = "";

    public CommandOption[] Options { get; init; } = [];

    public CommandEntry[] Subcommands { get; init; } = [];

    public WordRule Words { get; init; }

    public int MinWords { get; init; }

    public int? MaxWords { get; init; }

    public string? Missing { get; init; }

    public bool RawAfter { get; init; }

    public bool SignedNumberIsWord { get; init; }

    public Action<Invocation>? Check { get; init; }

    public Func<Invocation, Task<int>>? Run { get; init; }

    public bool Accepts(CommandOption? option)
    {
        return option is not null && Array.IndexOf(Options, option) >= 0;
    }

    public bool Owns(CommandOption? option)
    {
        if (Accepts(option))
            return true;
        foreach (var sub in Subcommands)
            if (sub.Accepts(option))
                return true;
        return false;
    }
}

internal enum WordRule
{
    None,
    One,
    Topic,
    Subcommand
}

internal sealed class CommandOption
{
    private readonly Func<string, string?, object?>? _read;

    private CommandOption(CommandWord word, bool takesValue, bool takesNumber, Func<string, string?, object?>? read)
    {
        Word = word;
        TakesValue = takesValue;
        TakesNumber = takesNumber;
        _read = read;
    }

    public CommandWord Word { get; }

    public bool TakesValue { get; }

    public bool TakesNumber { get; }

    public static CommandOption Flag(CommandWord word)
    {
        return new CommandOption(word, false, false, null);
    }

    public static CommandOption Value(CommandWord word, Func<string, string, object?>? read = null)
    {
        return new CommandOption(word, true, false, read is null ? null : (token, text) => read(token, text!));
    }

    public static CommandOption Number(CommandWord word, Func<string, string?, object?> read)
    {
        return new CommandOption(word, false, true, read);
    }

    public object? Read(string token, string? text)
    {
        return _read is null ? text : _read(token, text);
    }

    public void Collect(Dictionary<string, CommandOption> into)
    {
        into.TryAdd(Word.Long, this);
        foreach (var name in Word.Shorts)
            into.TryAdd(name, this);
    }
}
