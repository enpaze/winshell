namespace Winshell;

internal sealed record QuitPatience(TimeSpan Ask, TimeSpan Wait)
{
    public static readonly QuitPatience Interactive =
        new(IpcClient.ConnectLimit + TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

    public static readonly QuitPatience Closing = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
}
