using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Winshell.Native;

internal static unsafe class AppUserModelIds
{
    internal const int MaxCharacters = (int)PInvoke.APPLICATION_USER_MODEL_ID_MAX_LENGTH;

    internal readonly record struct Query(WIN32_ERROR Code, uint Length);

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
        if (processId == 0)
            return "";
        using var process = ProcessHandle.Open(processId);
        if (!process.IsOpen)
            return "";
        var handle = process.Handle;
        return Read(buffer =>
        {
            var length = (uint)buffer.Length;
            fixed (char* text = buffer)
            {
                return new Query(PInvoke.GetApplicationUserModelId(handle, &length, text), length);
            }
        });
    }

    internal static string Read(Func<char[], Query> query)
    {
        return TextBuffer.Read(0, MaxCharacters, buffer =>
        {
            var answer = query(buffer);
            if (answer.Code != WIN32_ERROR.ERROR_SUCCESS)
                return new TextBuffer.Answer(answer.Code, (int)answer.Length);
            return new TextBuffer.Answer(answer.Code,
                answer.Length > (uint)buffer.Length ? -1 : (int)answer.Length - 1);
        }) ?? "";
    }
}
