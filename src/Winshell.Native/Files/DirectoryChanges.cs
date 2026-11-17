using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Winshell.Native;

[Flags]
internal enum DirectoryChange : uint
{
    FileName = FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_FILE_NAME,

    FolderName = FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_DIR_NAME,

    Size = FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_SIZE,

    LastWrite = FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_LAST_WRITE
}

internal sealed unsafe class DirectoryChanges : IDisposable
{
    private const int BufferSize = 16 * 1024;

    private const int HeaderSize = 3 * sizeof(uint);

    private readonly SafeFileHandle _handle;
    private readonly DirectoryChange _filter;
    private readonly bool _subtree;
    private readonly ManualResetEvent _completed = new(false);
    private readonly byte* _buffer = (byte*)NativeMemory.Alloc(BufferSize);

    private readonly NativeOverlapped* _overlapped =
        (NativeOverlapped*)NativeMemory.AllocZeroed((nuint)sizeof(NativeOverlapped));

    private int _disposed;

    private DirectoryChanges(SafeFileHandle handle, DirectoryChange filter, bool subtree)
    {
        _handle = handle;
        _filter = filter;
        _subtree = subtree;
        _overlapped->EventHandle = _completed.SafeWaitHandle.DangerousGetHandle();
    }

    internal static DirectoryChanges? Open(string path, DirectoryChange filter, bool subtree)
    {
        var handle = new SafeFileHandle(PInvoke.CreateFile(path, (uint)FILE_ACCESS_RIGHTS.FILE_LIST_DIRECTORY,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
            null, FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OVERLAPPED,
            default), ownsHandle: true);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        return new DirectoryChanges(handle, filter, subtree);
    }

    internal bool Read()
    {
        return PInvoke.ReadDirectoryChanges(Handle, _buffer, BufferSize, _subtree, (FILE_NOTIFY_CHANGE)_filter, null,
            _overlapped, null);
    }

    internal bool Wait(List<string> names)
    {
        uint returned;
        if (!PInvoke.GetOverlappedResult(Handle, _overlapped, &returned, true) || returned == 0)
            return false;
        var count = (int)Math.Min(returned, BufferSize);
        var offset = 0;
        while (offset + HeaderSize <= count)
        {
            var entry = (FILE_NOTIFY_INFORMATION*)(_buffer + offset);
            if (entry->FileNameLength > (uint)(count - offset - HeaderSize))
                break;
            names.Add(new string((char*)&entry->FileName, 0, (int)entry->FileNameLength / sizeof(char)));
            if ((int)entry->NextEntryOffset <= 0)
                break;
            offset += (int)entry->NextEntryOffset;
        }

        return true;
    }

    internal void Cancel()
    {
        PInvoke.CancelIoEx(Handle, null);
    }

    private HANDLE Handle => new(_handle.DangerousGetHandle());

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _handle.Dispose();
        _completed.Dispose();
        NativeMemory.Free(_buffer);
        NativeMemory.Free(_overlapped);
    }
}
