using Winshell.Native;

namespace Winshell.Apps;

internal sealed class WindowsJumpListBackend : IJumpListBackend
{
    public JumpListRead Read(string appUserModelId)
    {
        var (recent, frequent) = DocumentLists.Read(appUserModelId, JumpListRead.PerCategory);
        return JumpListRead.From(recent, frequent);
    }
}
