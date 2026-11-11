namespace Winshell;

/// <summary>
/// One command. It owns the words it answers to, the flags it takes, the checks it runs and the handler that runs it.
/// A subcommand is an entry too, and it owns the flags that belong to it alone.
/// </summary>
internal sealed record CommandEntry
{
    public required string Name { get; init; }

    /// <summary>The lines this command adds to the overview, already spaced as help prints them.</summary>
    public string Overview { get; init; } = "";

    /// <summary>The command's own help. Empty when the overview is what <c>help</c> prints for it.</summary>
    public string Usage { get; init; } = "";

    public CommandOption[] Options { get; init; } = [];

    public CommandEntry[] Subcommands { get; init; } = [];

    /// <summary>How many words follow a top-level command. A subcommand uses <see cref="MinWords"/> instead.</summary>
    public WordRule Words { get; init; }

    /// <summary>How many words must follow a subcommand.</summary>
    public int MinWords { get; init; }

    /// <summary>The most words that may follow a subcommand. Null means a spare word is kept.</summary>
    public int? MaxWords { get; init; }

    /// <summary>The word that must come first after a subcommand, such as <c>prop</c>'s <c>get</c>.</summary>
    public string? FirstWord { get; init; }

    /// <summary>What a subcommand names when its words are missing.</summary>
    public string? Missing { get; init; }

    /// <summary>After the required words, the rest are arguments even when they look like options.</summary>
    public bool RawAfter { get; init; }

    /// <summary>A signed whole number is a word of this subcommand, as <c>inspect scroll</c>'s steps are.</summary>
    public bool SignedNumberIsWord { get; init; }

    /// <summary>Refuses a line this command cannot run. Parsing has already checked the words and the flags.</summary>
    public Action<Invocation>? Check { get; init; }

    public Func<Invocation, Task<int>>? Run { get; init; }

    public bool Accepts(CommandOption? option)
    {
        return option is not null && Array.IndexOf(Options, option) >= 0;
    }

    /// <summary>This command or one of its subcommands takes <paramref name="option"/>.</summary>
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

/// <summary>How many positional words a top-level command takes after its name.</summary>
internal enum WordRule
{
    None,
    One,
    Topic,
    Subcommand
}

/// <summary>An option's spellings and what a value of it has to be.</summary>
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

    /// <summary>Takes the next word as its value only when that is a whole number, as <c>--screen [n]</c> does.</summary>
    public bool TakesNumber { get; }

    public static CommandOption Flag(CommandWord word)
    {
        return new CommandOption(word, false, false, null);
    }

    public static CommandOption Value(CommandWord word, Func<string, string, object?>? read = null)
    {
        return new CommandOption(word, true, false, read is null ? null : (token, text) => read(token, text!));
    }

    /// <summary>An option with an optional whole number after it. Without one, the reader is given null.</summary>
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
