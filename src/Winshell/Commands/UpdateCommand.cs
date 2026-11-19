namespace Winshell.Commands;

internal static class UpdateCommand
{
    internal static readonly CommandOption Channel = CommandOption.Value(CommandWords.Channel, ReadChannel);

    internal static readonly CommandEntry Entry = new()
    {
        Name = "update",
        Overview = "       winshell update [--channel <name>]  install the newest build of the saved channel",
        Usage = """
                winshell update [--channel <name>]
                  Installs the newest build of the saved channel.
                  stable and dev are the channels. With no --channel, update.toml in the shells
                  folder names the channel, and a missing file means stable.
                  --channel stable or --channel dev selects that channel and remembers it when
                  this copy is already current or the update finishes.
                  The running winshell.exe has to be the one in %LOCALAPPDATA%\Programs\winshell\bin.
                """,
        Words = WordRule.None,
        Options = [Channel],
        Run = Execute
    };

    internal static string InstallRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "winshell");

    private static object ReadChannel(string token, string text)
    {
        _ = token;
        if (!UpdateChannels.TryParse(text, out var channel))
            throw new ArgumentException("winshell update --channel needs stable or dev.");
        return UpdateChannels.Name(channel);
    }

    private static Task<int> Execute(Invocation parsed)
    {
        return RunAsync(
            ReleaseSources.Create(Environment.GetEnvironmentVariable("WINSHELL_RELEASE_BASE")),
            InstallRoot,
            ShellLocations.Current.ShellsRoot,
            Environment.ProcessPath ?? "",
            Shell.Version,
            parsed.Text(Channel),
            Console.Out,
            Console.Error,
            StopShells);
    }

    internal static async Task<int> RunAsync(
        IReleaseFeed feed,
        string installRoot,
        string shellsRoot,
        string executablePath,
        string informationalVersion,
        string? requestedChannel,
        TextWriter output,
        TextWriter error,
        Func<Task<bool>>? stopShells = null)
    {
        var channel = requestedChannel is null
            ? UpdateChannelFile.Read(shellsRoot)
            : UpdateChannels.TryParse(requestedChannel, out var chosen)
                ? chosen
                : throw new ArgumentException("winshell update --channel needs stable or dev.");
        try
        {
            var result = await ReleaseUpdate.ApplyAsync(feed, channel, installRoot, executablePath, informationalVersion, stopShells);
            switch (result)
            {
                case UpdateResult.NotInstalled:
                    error.WriteLine("Winshell is not installed. Run the install script.");
                    return 1;
                case UpdateResult.Rejected rejected:
                    error.WriteLine(
                        "Refusing the update: the SHA-256 of " + rejected.Archive
                        + " does not match SHA256SUMS.txt. The release may still be uploading. Try again in a minute.");
                    return 1;
                case UpdateResult.Current:
                    Remember(shellsRoot, channel, requestedChannel);
                    output.WriteLine("Winshell is up to date.");
                    return 0;
                case UpdateResult.Updated updated:
                    Remember(shellsRoot, channel, requestedChannel);
                    output.WriteLine("Winshell is updated to " + updated.Version + ".");
                    return 0;
                default:
                    return 1;
            }
        }
        catch (UpdateFailedException)
        {
            return 1;
        }
    }

    private static void Remember(string shellsRoot, UpdateChannel channel, string? requestedChannel)
    {
        if (requestedChannel is not null)
            UpdateChannelFile.Write(shellsRoot, channel);
    }

    private static async Task<bool> StopShells()
    {
        foreach (var info in ShellLocations.Current.Instances.Running())
        {
            if (!await KillCommand.StopAsync(info, false))
                return false;
        }

        return true;
    }
}
