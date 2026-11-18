namespace Winshell;

public sealed record LauncherCommand(string Name, Action Run);
