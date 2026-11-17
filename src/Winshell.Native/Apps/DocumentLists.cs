using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.System.Com;

namespace Winshell.Native;

internal static class DocumentLists
{
    private static readonly Guid Clsid = new("86bec222-30f2-47e0-9f25-60d11cd75c28");
    private const uint Recent = 0;
    private const uint Frequent = 1;

    internal static (IReadOnlyList<string>? Recent, IReadOnlyList<string>? Frequent) Read(string appUserModelId,
        int perCategory)
    {
        var lists = Com.Create<IApplicationDocumentLists>(Clsid, CLSCTX.CLSCTX_INPROC_SERVER, out _);
        if (lists is null)
            return (null, null);
        try
        {
            if (lists.SetAppID(appUserModelId) < 0)
                return (null, null);
            return (Category(lists, Recent, perCategory), Category(lists, Frequent, perCategory));
        }
        finally
        {
            Com.Release(lists);
        }
    }

    private static List<string>? Category(IApplicationDocumentLists lists, uint kind, int perCategory)
    {
        var arrayId = typeof(IObjectArray).GUID;
        if (lists.GetList(kind, (uint)perCategory, in arrayId, out var array) < 0 || array is null)
            return null;
        try
        {
            if (array.GetCount(out var count) < 0)
                return null;
            var names = new List<string>();
            var itemId = typeof(IShellItem).GUID;
            var linkId = typeof(IShellLinkW).GUID;
            for (uint i = 0; i < count && names.Count < perCategory; i++)
            {
                var name = NameAt(array, i, itemId, linkId);
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            return names;
        }
        finally
        {
            Com.Release(array);
        }
    }

    private static string? NameAt(IObjectArray array, uint index, Guid itemId, Guid linkId)
    {
        if (array.GetAt(index, in itemId, out var item) >= 0)
            try
            {
                if (item is IShellItem shell && shell.GetDisplayName(0, out var name) >= 0 &&
                    !string.IsNullOrWhiteSpace(name))
                    return name;
            }
            finally
            {
                Com.Release(item);
            }

        if (array.GetAt(index, in linkId, out var link) >= 0)
            try
            {
                if (link is IShellLinkW shellLink)
                {
                    var buffer = new StringBuilder(1024);
                    shellLink.GetPath(buffer, buffer.Capacity, 0, 0);
                    var file = Path.GetFileName(buffer.ToString());
                    return string.IsNullOrWhiteSpace(file) ? null : file;
                }
            }
            catch (COMException)
            {
            }
            finally
            {
                Com.Release(link);
            }

        return null;
    }

    [ComImport]
    [Guid("3c594f9f-9f30-47a1-979a-c9e83d3d0a06")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationDocumentLists
    {
        [PreserveSig]
        int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        [PreserveSig]
        int GetList(uint listType, uint count, in Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IObjectArray? array);
    }
}
