using System.Text.RegularExpressions;

namespace Winshell;

/// <summary>
/// Reads what <c>dotnet watch</c> prints. <see cref="Prepare"/> makes it print in the one form this reads.
/// <see cref="Read"/> gives the text of the notice a line calls for: the compile errors so far, or empty once the code
/// is applied. Null means the line changes nothing on screen.
/// </summary>
/// <param name="projectFolder">The project's folder. An error in a file under it shows the file's path from there.</param>
internal sealed partial class DevOutput(string projectFolder)
{
    private const string Prefix = "dotnet watch : ";

    // No key reaches dotnet watch, so its Ctrl+R hint would point at a key that does nothing.
    private const string RestartHint = Prefix + "Press Ctrl+R to restart.";

    private readonly List<string> _errors = [];

    /// <summary>Sets the variables that make <c>dotnet watch</c> print in English and without emoji.</summary>
    public static void Prepare(IDictionary<string, string?> environment)
    {
        environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        environment["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1";
    }

    /// <summary>Whether the console shows <paramref name="line"/>.</summary>
    public static bool Shows(string line)
    {
        return line != RestartHint;
    }

    public string? Read(string line)
    {
        var text = line.StartsWith(Prefix, StringComparison.Ordinal) ? line[Prefix.Length..] : line.Trim();
        if (ConfigurationProject.IsErrorLine(text))
        {
            var error = ConfigurationProject.ErrorLine(text, projectFolder);
            if (_errors.Contains(error) || _errors.Count == ConfigurationProject.MostErrors)
                return null;
            _errors.Add(error);
            return string.Join('\n', _errors);
        }

        if (text.StartsWith("Unable to apply changes", StringComparison.Ordinal))
            _errors.Clear();
        if (!Applied().IsMatch(text))
            return null;
        _errors.Clear();
        return "";
    }

    [GeneratedRegex(@"changes applied in \d+ms|^Build succeeded")]
    private static partial Regex Applied();
}
