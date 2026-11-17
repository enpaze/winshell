namespace Winshell.Winland;

internal interface IWinlandBackend
{
    bool IsConnected { get; }

    string PipePath { get; }

    int ProcessId { get; }

    IDisposable Listen(Action<bool> connectionChanged, Action<string> eventReceived, Action<Exception> failed);

    Task<string?> RequestAsync(string command);
}
