namespace Winshell;

internal static class SharedFile
{
    public static FileStream OpenToRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }
}
