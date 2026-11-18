namespace Winshell.Apps;

internal interface IJumpListBackend
{
    JumpListRead Read(string appUserModelId);
}
