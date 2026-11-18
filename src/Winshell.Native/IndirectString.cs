using Windows.Win32;

namespace Winshell.Native;

internal static class IndirectString
{
    internal static string Load(string text)
    {
        if (!text.StartsWith('@'))
            return text;
        var buffer = new char[512];
        return PInvoke.SHLoadIndirectString(text, buffer).Value == 0 ? new string(buffer).TrimEnd('\0') : "";
    }
}
