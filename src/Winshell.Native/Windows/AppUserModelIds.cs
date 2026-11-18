using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static unsafe class AppUserModelIds
{
    internal const int MaxCharacters = (int)PInvoke.APPLICATION_USER_MODEL_ID_MAX_LENGTH;

    internal static string OfWindow(nint hwnd)
    {
        var iid = typeof(IPropertyStore).GUID;
        void* pointer = null;
        if (PInvoke.SHGetPropertyStoreForWindow((HWND)hwnd, &iid, &pointer).Failed ||
            Com.Wrap<IPropertyStore>((nint)pointer) is not { } store)
            return "";
        try
        {
            return PropertyStore.ReadString(store, PropertyStore.AppUserModelId) ?? "";
        }
        catch (COMException)
        {
            return "";
        }
        finally
        {
            Com.Release(store);
        }
    }

    internal static string OfProcess(uint processId)
    {
        return ProcessHandle.ReadText(processId, MaxCharacters,
            (process, length, text) => PInvoke.GetApplicationUserModelId(process, length, text));
    }
}
