using System.ComponentModel;
using System.Runtime.InteropServices;
using SharpGen.Runtime;

namespace Winshell;

internal sealed class WindowsRefusal(string message) : Exception(message);

internal static class WindowsFailure
{
    public static bool IsExpected(Exception e)
    {
        return e is UnauthorizedAccessException or IOException or COMException or Win32Exception
            or SharpGenException or WindowsRefusal;
    }
}
