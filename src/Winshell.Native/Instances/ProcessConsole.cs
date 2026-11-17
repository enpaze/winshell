using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;

namespace Winshell.Native;

internal enum StandardStream
{
    Input,
    Output,
    Error
}

internal enum StreamTarget
{
    Other,
    File,
    Pipe
}

internal static class ProcessConsole
{
    internal static nint Window()
    {
        return PInvoke.GetConsoleWindow();
    }

    internal static StreamTarget Target(StandardStream stream)
    {
        if (Handle(stream) is not { } handle)
            return StreamTarget.Other;
        return (PInvoke.GetFileType(handle) & ~FILE_TYPE.FILE_TYPE_REMOTE) switch
        {
            FILE_TYPE.FILE_TYPE_DISK => StreamTarget.File,
            FILE_TYPE.FILE_TYPE_PIPE => StreamTarget.Pipe,
            _ => StreamTarget.Other
        };
    }

    private static HANDLE? Handle(StandardStream stream)
    {
        var handle = PInvoke.GetStdHandle(stream switch
        {
            StandardStream.Input => STD_HANDLE.STD_INPUT_HANDLE,
            StandardStream.Output => STD_HANDLE.STD_OUTPUT_HANDLE,
            _ => STD_HANDLE.STD_ERROR_HANDLE
        });
        return (nint)handle is 0 or -1 ? null : handle;
    }
}
