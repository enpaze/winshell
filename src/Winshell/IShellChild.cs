namespace Winshell;

internal interface IShellChild : IDisposable
{
    Task<int> Exited { get; }

    int Pid { get; }

    ExitIntent Intent { get; }

    Task StopAsync();
}
