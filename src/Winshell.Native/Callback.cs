using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Winshell.Native;

#pragma warning disable CA1031
internal static class Callback
{
    internal static void Run(Action<Exception>? failed, Action body)
    {
        try
        {
            body();
        }
        catch (Exception e)
        {
            Report(failed, e);
        }
    }

    internal static T Run<T>(Action<Exception>? failed, Func<T> body, T fallback)
    {
        try
        {
            return body();
        }
        catch (Exception e)
        {
            Report(failed, e);
            return fallback;
        }
    }

    private static void Report(Action<Exception>? failed, Exception e)
    {
        if (failed is null)
            return;
        try
        {
            failed(e);
        }
        catch (Exception)
        {
        }
    }

    internal static List<nint> Collect(Action<nint> enumerate)
    {
        var collection = new Collection();
        var handle = GCHandle.Alloc(collection);
        try
        {
            enumerate(GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        collection.Failure?.Throw();
        return collection.Handles;
    }

    internal static int Collected(nint context, nint handle)
    {
        var collection = (Collection)GCHandle.FromIntPtr(context).Target!;
        try
        {
            collection.Handles.Add(handle);
            return 1;
        }
        catch (Exception e)
        {
            collection.Failure = ExceptionDispatchInfo.Capture(e);
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    internal static BOOL CollectWindow(HWND hwnd, LPARAM context)
    {
        return (BOOL)Collected(context, hwnd);
    }

    private sealed class Collection
    {
        public List<nint> Handles { get; } = [];

        public ExceptionDispatchInfo? Failure { get; set; }
    }
}
#pragma warning restore CA1031
