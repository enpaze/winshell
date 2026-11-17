using System.Text.RegularExpressions;

namespace Winshell;

internal sealed partial class DevOutput(string projectFolder)
{
    private const string Prefix = "dotnet watch : ";

    private const string RestartHint = Prefix + "Press Ctrl+R to restart.";

    private readonly List<string> _errors = [];

    public static void Prepare(IDictionary<string, string?> environment)
    {
        environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        environment["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1";
    }

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
