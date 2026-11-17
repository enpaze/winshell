namespace Winshell;

internal sealed class FileWatch : IDisposable
{
    private readonly string _path;
    private readonly IEventLoop _loop;
    private readonly Action _changed;
    private DirectoryWatch? _watch;
    private int _generation;
    private bool _disposed;

    public FileWatch(string path, IEventLoop loop, Action changed)
    {
        _path = path;
        _loop = loop;
        _changed = changed;
        Watch(reportExistingFile: false);
    }

    private void Watch(bool reportExistingFile)
    {
        var full = AtomicFile.FollowLink(_path);
        var dir = Path.GetDirectoryName(full);
        var name = Path.GetFileName(full);
        var parent = false;
        while (dir is not null && !Directory.Exists(dir))
        {
            parent = true;
            name = Path.GetFileName(dir);
            dir = Path.GetDirectoryName(dir);
        }

        if (dir is null)
            return;
        var generation = ++_generation;
        Action arrived = parent ? () => Rewatch(generation) : Report;
        _watch = DirectoryWatch.Start(dir, parent ? DirectoryWatch.NameChanges : DirectoryWatch.FileChanges,
            changedName =>
            {
                if (changedName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    _loop.Post(arrived);
            }, () => _loop.Post(() => Lost(generation)));
        if (_watch is not null && Listed(_watch, name) &&
            (parent ? Directory.Exists(Path.Combine(dir, name)) : reportExistingFile))
            _loop.Post(arrived);
    }

    private static bool Listed(DirectoryWatch watch, string name) =>
        watch.ExistingNames.Any(candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void Lost(int generation) => Rewatch(generation, readFile: true);

    private void Rewatch(int generation, bool readFile = false)
    {
        if (_disposed || generation != _generation)
            return;
        _watch?.Dispose();
        _watch = null;
        Watch(reportExistingFile: true);
        if (readFile && !_disposed)
            _changed();
    }

    private void Report()
    {
        if (!_disposed)
            _changed();
    }

    public void Dispose()
    {
        _disposed = true;
        _watch?.Dispose();
        _watch = null;
    }
}
