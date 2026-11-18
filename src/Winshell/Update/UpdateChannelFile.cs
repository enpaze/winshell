using System.Text;

namespace Winshell;

internal static class UpdateChannelFile
{
    internal const string FileName = "update.toml";

    internal const string Invalid = "The saved update channel is not stable or dev.";

    internal static UpdateChannel Read(string configRoot)
    {
        var path = Path.Combine(configRoot, FileName);
        if (!File.Exists(path))
            return UpdateChannel.Stable;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(Invalid);
        }

        if (!TomlDocument.TryParse(path, text, out var document, out _)
            || document.Values.Count != 1
            || !document.Values.TryGetValue("channel", out var value)
            || value is not string word
            || !UpdateChannels.TryParse(word, out var channel))
            throw new InvalidOperationException(Invalid);

        return channel;
    }

    internal static void Write(string configRoot, UpdateChannel channel)
    {
        Directory.CreateDirectory(configRoot);
        var path = Path.Combine(configRoot, FileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, "channel = \"" + UpdateChannels.Name(channel) + "\"\n", new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}
