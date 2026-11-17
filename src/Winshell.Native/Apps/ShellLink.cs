using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.System.Com;

namespace Winshell.Native;

internal sealed record ShellLinkInfo(
    string Target,
    string Arguments,
    string WorkingDirectory,
    string Description,
    string AppUserModelId);

internal static class ShellLink
{
    private static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");

    public static ShellLinkInfo? Read(string path)
    {
        var shell = Com.Create<IShellLinkW>(ClsidShellLink, CLSCTX.CLSCTX_SERVER, out _);
        if (shell is null)
            return null;
        try
        {
            if (shell is not IPersistFile file)
                return null;
            file.Load(path, 0);
            var buffer = new StringBuilder(1024);
            shell.GetPath(buffer, buffer.Capacity, 0, 0);
            var target = buffer.ToString();
            buffer.Clear();
            shell.GetArguments(buffer, buffer.Capacity);
            var arguments = buffer.ToString();
            buffer.Clear();
            shell.GetWorkingDirectory(buffer, buffer.Capacity);
            var workingDirectory = Environment.ExpandEnvironmentVariables(buffer.ToString());
            buffer.Clear();
            shell.GetDescription(buffer, buffer.Capacity);
            var description = buffer.ToString();
            var aumid = shell is IPropertyStore store
                ? PropertyStore.ReadString(store, PropertyStore.AppUserModelId) ?? ""
                : "";
            return new ShellLinkInfo(target, arguments, workingDirectory, description, aumid);
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or IOException or ArgumentException
                                      or InvalidCastException)
        {
            return null;
        }
        finally
        {
            Com.Release(shell);
        }
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);

        [PreserveSig]
        int IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, int mode);

        void Save([MarshalAs(UnmanagedType.LPWStr)] string? fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
    }
}
