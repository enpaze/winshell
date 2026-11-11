using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

/// <summary>A command line split into arguments as Windows splits it.</summary>
internal static unsafe class CommandLine
{
    /// <summary>The arguments, or none for a blank line: <c>CommandLineToArgvW</c> returns this program's path for one.</summary>
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
