using Windows.Win32;
using Windows.Win32.System.LibraryLoader;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal sealed class OwnedIcon : IDisposable
{
    private OwnedIcon(nint handle)
    {
        Handle = handle;
    }

    internal nint Handle { get; private set; }

    internal static unsafe OwnedIcon? Load(string module, ushort id, string name, int size)
    {
        var library = PInvoke.LoadLibraryEx(module,
            LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        if (library == default)
            return null;
        try
        {
            var icon = id != 0
                ? PInvoke.LoadImage(library, (char*)id, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, default)
                : PInvoke.LoadImage(library, name, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, default);
            return icon.IsNull ? null : new OwnedIcon(icon);
        }
        finally
        {
            PInvoke.FreeLibrary(library);
        }
    }

    public void Dispose()
    {
        if (Handle == 0)
            return;
        PInvoke.DestroyIcon((HICON)Handle);
        Handle = 0;
    }
}
