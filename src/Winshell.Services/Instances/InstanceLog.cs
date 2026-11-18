using System.Runtime.CompilerServices;
using System.Text;

namespace Winshell;

internal sealed class InstanceLog : IDisposable
{
    private const long DefaultCap = 4 * 1024 * 1024;

    public const string FileName = "log.txt";

    private const string RolledName = "log.1.txt";

    private static readonly TimeSpan FollowPoll = TimeSpan.FromMilliseconds(200);

    private readonly string _path;
    private readonly long _cap;
    private readonly Lock _gate = new();
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);
    private readonly ITimer _timer;
    private StreamWriter? _writer;
    private readonly StringBuilder _pending = new();
    private bool _closed;

    public InstanceLog(string path, long cap = DefaultCap, TimeProvider? time = null)
    {
        _path = path;
        _cap = cap;
        _writer = Open();
        _timer = (time ?? TimeProvider.System).CreateTimer(_ => FlushFromTimer(), null, Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public void Write(LogLevel level, string stampedLine)
    {
        lock (_gate)
        {
            if (_closed)
                return;
            var waiting = _pending.Length > 0;
            _pending.Append(stampedLine).Append('\n');
            try
            {
                if (level >= LogLevel.Warn)
                    FlushLocked();
            }
            finally
            {
                if (!waiting && _pending.Length > 0)
                    _timer.Change(Delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            FlushLocked();
        }
    }

    private void FlushFromTimer()
    {
        lock (_gate)
        {
            try
            {
                FlushLocked();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _pending.Clear();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
                return;
            try
            {
                FlushLocked();
            }
            finally
            {
                _closed = true;
                _writer?.Dispose();
                _writer = null;
            }
        }

        _timer.Dispose();
    }

    private void FlushLocked()
    {
        if (_closed || _pending.Length == 0)
            return;
        _writer ??= Open();
        if (_writer.BaseStream.Length >= _cap)
            Roll();
        _writer.Write(_pending.ToString());
        _writer.Flush();
        _pending.Clear();
    }

    private void Roll()
    {
        _writer!.Dispose();
        _writer = null;
        var rolled = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_path))!, RolledName);
        try
        {
            File.Copy(_path, rolled, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _writer = Open();
            return;
        }

        _writer = Open(FileMode.Create);
    }

    private StreamWriter Open(FileMode mode = FileMode.Append)
    {
        return new StreamWriter(new FileStream(_path, mode, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            AutoFlush = false
        };
    }

    public static async IAsyncEnumerable<string> Follow(StreamReader reader, TimeProvider? time = null,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        var stream = reader.BaseStream;
        while (true)
        {
            if (await reader.ReadLineAsync(cancel) is { } line)
            {
                yield return line;
                continue;
            }

            if (stream.Length < stream.Position)
            {
                if (stream is FileStream { Name: var name })
                    foreach (var missed in Rest(Path.Combine(Path.GetDirectoryName(name)!, RolledName),
                                 stream.Position))
                        yield return missed;
                stream.Position = 0;
                reader.DiscardBufferedData();
                continue;
            }

            await Task.Delay(FollowPoll, time ?? TimeProvider.System, cancel);
        }
    }

    private static List<string> Rest(string file, long from)
    {
        var lines = new List<string>();
        try
        {
            using var stream = SharedFile.OpenToRead(file);
            if (stream.Length <= from)
                return lines;
            stream.Position = from;
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
                lines.Add(line);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return lines;
    }
}
