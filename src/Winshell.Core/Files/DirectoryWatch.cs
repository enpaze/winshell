using Winshell.Native;

namespace Winshell;

internal sealed class DirectoryWatch : IDisposable
{
    public const DirectoryChange FileChanges =
        DirectoryChange.FileName | DirectoryChange.Size | DirectoryChange.LastWrite;

    public const DirectoryChange NameChanges = DirectoryChange.FileName | DirectoryChange.FolderName;

    private readonly DirectoryChanges _changes;
    private readonly Action<string> _changed;
    private readonly Action _lost;
    private readonly ManualResetEventSlim _queued = new(false);
    private readonly Thread _thread;
    private bool _reading;
    private volatile bool _stopped;
    private int _disposed;

    internal IReadOnlyList<string> ExistingNames { get; private set; } = [];

    private DirectoryWatch(DirectoryChanges changes, Action<string> changed, Action lost)
    {
        _changes = changes;
        _changed = changed;
        _lost = lost;
        _thread = new Thread(Run) { IsBackground = true, Name = "Directory watch" };
    }

    public static DirectoryWatch? Start(string path, DirectoryChange filter, Action<string> changed, Action lost,
        bool subtree = false)
    {
        if (DirectoryChanges.Open(path, filter, subtree) is not { } changes)
            return null;
        var watch = new DirectoryWatch(changes, changed, lost);
        watch._thread.Start();
        watch._queued.Wait();
        if (!watch._reading)
        {
            watch.Dispose();
            return null;
        }

        watch.ExistingNames = ListNames(path, filter, subtree);
        return watch;
    }

    private void Run()
    {
        try
        {
            _reading = _changes.Read();
        }
        finally
        {
            _queued.Set();
        }

        if (!_reading)
            return;
        var names = new List<string>();
        while (true)
        {
            if (_stopped)
                _changes.Cancel();
            names.Clear();
            var ok = _changes.Wait(names);
            if (_stopped)
                return;
            if (!ok)
            {
                _lost();
                return;
            }

            foreach (var name in names)
                _changed(name);
            if (_stopped)
                return;
            if (!_changes.Read())
            {
                if (!_stopped)
                    _lost();
                return;
            }
        }
    }

    private static string[] ListNames(string path, DirectoryChange filter, bool subtree)
    {
        var option = subtree ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var names = new List<string>();
        try
        {
            if (ReportsFiles(filter))
                foreach (var file in Directory.EnumerateFiles(path, "*", option))
                    names.Add(Path.GetRelativePath(path, file));
            if (ReportsFolders(filter))
                foreach (var folder in Directory.EnumerateDirectories(path, "*", option))
                    names.Add(Path.GetRelativePath(path, folder));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return names.ToArray();
    }

    private static bool ReportsFiles(DirectoryChange filter) => (filter & FileChanges) != 0;

    private static bool ReportsFolders(DirectoryChange filter) => (filter & DirectoryChange.FolderName) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _stopped = true;
        if (_thread.ThreadState != ThreadState.Unstarted)
        {
            _changes.Cancel();
            _thread.Join();
        }

        _changes.Dispose();
        _queued.Dispose();
    }
}
