using Winshell.Native;

namespace Winshell;

/// <summary>
/// Watches one folder on a thread of its own. FileSystemWatcher delivers on thread pool workers, so a busy pool
/// holds a change back. The first read is queued before <see cref="Start"/> returns, then <see cref="ExistingNames"/>
/// is taken, so a name made in that gap is in the list or in the callback. That thread makes every read, because
/// Windows cancels a read when the thread that made it ends, and it waits while the list is taken so a long list
/// cannot overflow the read. Callbacks run on the watch thread and must not block.
/// </summary>
internal sealed class DirectoryWatch : IDisposable
{
    /// <summary>A file was created, deleted, renamed or written.</summary>
    public const DirectoryChange FileChanges =
        DirectoryChange.FileName | DirectoryChange.Size | DirectoryChange.LastWrite;

    /// <summary>A file or folder was created, deleted or renamed.</summary>
    public const DirectoryChange NameChanges = DirectoryChange.FileName | DirectoryChange.FolderName;

    private readonly DirectoryChanges _changes;
    private readonly Action<string> _changed;
    private readonly Action _lost;
    private readonly ManualResetEventSlim _queued = new(false);
    private readonly Thread _thread;
    private bool _reading;
    private volatile bool _stopped;
    private int _disposed;

    /// <summary>
    /// Names, relative to the folder, taken after the watch was queued and before <see cref="Start"/> returns.
    /// A name made after that arrives through the callback.
    /// </summary>
    internal IReadOnlyList<string> ExistingNames { get; private set; } = [];

    private DirectoryWatch(DirectoryChanges changes, Action<string> changed, Action lost)
    {
        _changes = changes;
        _changed = changed;
        _lost = lost;
        _thread = new Thread(Run) { IsBackground = true, Name = "Directory watch" };
    }

    /// <summary>Starts watching, or returns null when the folder can't be opened.</summary>
    /// <param name="path">The folder.</param>
    /// <param name="filter"><see cref="FileChanges"/> or <see cref="NameChanges"/>.</param>
    /// <param name="changed">Called with the name of each thing that changed, relative to the folder.</param>
    /// <param name="lost">Called once when the folder went away or changes were dropped. Nothing is reported after it.</param>
    /// <param name="subtree">Also report changes in folders under <paramref name="path"/>.</param>
    public static DirectoryWatch? Start(string path, DirectoryChange filter, Action<string> changed, Action lost,
        bool subtree = false)
    {
        if (DirectoryChanges.Open(path, filter, subtree) is not { } changes)
            return null;
        var watch = new DirectoryWatch(changes, changed, lost);
        watch._thread.Start();
        // The thread only queues the read, then waits. This list runs while that wait drains the buffer.
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
            // Dispose cancelled once. A read queued after that has to be cancelled here, or this wait never returns.
            if (_stopped)
                _changes.Cancel();
            names.Clear();
            var ok = _changes.Wait(names);
            if (_stopped)
                return;
            // No names means the folder went away or the buffer dropped changes. Cancel is the branch above.
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

    /// <summary>Names <paramref name="filter"/> already matches under <paramref name="path"/>.</summary>
    private static string[] ListNames(string path, DirectoryChange filter, bool subtree)
    {
        var option = subtree ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var names = new List<string>();
        try
        {
            // FileName is in both presets, so a name watch lists files as well as folders.
            if (ReportsFiles(filter))
                foreach (var file in Directory.EnumerateFiles(path, "*", option))
                    names.Add(Path.GetRelativePath(path, file));
            if (ReportsFolders(filter))
                foreach (var folder in Directory.EnumerateDirectories(path, "*", option))
                    names.Add(Path.GetRelativePath(path, folder));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Listing can stop early when the folder goes away. The watch thread still reports a change, or ends it.
        }

        return names.ToArray();
    }

    /// <summary>True when <paramref name="filter"/> reports files. <see cref="NameChanges"/> does: it includes file names.</summary>
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
