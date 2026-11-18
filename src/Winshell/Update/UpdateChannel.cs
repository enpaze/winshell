namespace Winshell;

internal enum UpdateChannel
{
    Stable,
    Dev
}

internal static class UpdateChannels
{
    internal static bool TryParse(string? text, out UpdateChannel channel)
    {
        if (text is not null && text.Equals("stable", StringComparison.OrdinalIgnoreCase))
        {
            channel = UpdateChannel.Stable;
            return true;
        }

        if (text is not null && text.Equals("dev", StringComparison.OrdinalIgnoreCase))
        {
            channel = UpdateChannel.Dev;
            return true;
        }

        channel = default;
        return false;
    }

    internal static string Name(UpdateChannel channel)
    {
        return channel switch
        {
            UpdateChannel.Stable => "stable",
            UpdateChannel.Dev => "dev",
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };
    }
}

internal abstract record UpdateResult
{
    private UpdateResult()
    {
    }

    internal sealed record NotInstalled : UpdateResult;

    internal sealed record Current(string Version) : UpdateResult;

    internal sealed record Rejected(string Archive) : UpdateResult;

    internal sealed record Updated(string Version, bool Restarted) : UpdateResult;
}

internal sealed record ReleaseListing(string Version, string Commit, string ArchiveUrl, string ChecksumsUrl);

internal interface IReleaseFeed
{
    ReleaseListing Latest(UpdateChannel channel);

    byte[] Download(string url);
}

internal sealed class UpdateFailedException : Exception
{
    internal UpdateFailedException()
        : base("A running shell did not stop.")
    {
    }
}
