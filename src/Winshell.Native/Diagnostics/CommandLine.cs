using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static unsafe class CommandLine
{
    internal static string[] Split(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return [];
        var argv = PInvoke.CommandLineToArgv(command, out var count);
        if (argv is null)
            return [];
        try
        {
            var args = new string[count];
            for (var i = 0; i < count; i++)
                args[i] = Marshal.PtrToStringUni((nint)argv[i].Value) ?? "";
            return args;
        }
        finally
        {
            PInvoke.LocalFree(new HLOCAL((nint)argv));
        }
    }
}
