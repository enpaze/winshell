using System.ComponentModel;
using System.Runtime.InteropServices;
using SharpGen.Runtime;

namespace Winshell;

/// <summary>A refusal from Windows that arrived as a result rather than an exception.</summary>
internal sealed class WindowsRefusal(string message) : Exception(message);

/// <summary>Tells a Windows refusal apart from a bug.</summary>
internal static class WindowsFailure
{
    /// <summary>
    /// Whether <paramref name="e"/> is a refusal from Windows: access denied, a COM or Win32 error, an IO failure,
    /// a Direct3D <see cref="SharpGenException"/>, or a <see cref="WindowsRefusal"/>. A service's own
    /// <see cref="InvalidOperationException"/>, <see cref="NotSupportedException"/>, or <see cref="ArgumentException"/>
    /// is a bug and is not included.
    /// </summary>
    public static bool IsExpected(Exception e)
    {
        return e is UnauthorizedAccessException or IOException or COMException or Win32Exception
            or SharpGenException or WindowsRefusal;
    }
}
