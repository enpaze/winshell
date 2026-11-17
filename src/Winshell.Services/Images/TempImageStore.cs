using System.Globalization;
using System.Security.Cryptography;

namespace Winshell;

internal sealed class TempImageStore
{
    internal const int MaxBytes = 8 * 1024 * 1024;

    private const long MaxPixels = 4096 * 4096;

    internal const string Refused = "it is not a picture under the size limit";

    private readonly string _root;
    private readonly string _folder;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, Use> _uses = [];
    private readonly Lock _gate = new();
    private int _cleaning;

    public TempImageStore(string root, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = root;
        _folder = Path.Combine(root, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        _time = time;
    }

    public static TempImageStore Shared { get; } =
        new(Path.Combine(Path.GetTempPath(), "winshell", "images"), TimeProvider.System);

    public string? Claim(Stream input, CancellationToken cancellation = default)
    {
        var data = Read(input, cancellation);
        if (data is null)
            return null;
        if (data.Length == 0)
            return "";
        if (ImageHeader.Read(data) is not (var extension, var width and > 0, var height and > 0) ||
            (ulong)width * height > MaxPixels)
            return null;

        var path = Path.Combine(_folder, Convert.ToHexStringLower(SHA256.HashData(data))[..32] + extension);
        var url = new Uri(path).AbsoluteUri;
        lock (_gate)
        {
            if (_uses.TryGetValue(url, out var use))
            {
                use.Count++;
                return url;
            }

            if (!File.Exists(path) && !TryWrite(path, data))
                return null;
            _uses.Add(url, new Use(path));
            return url;
        }
    }

    public void Release(string url)
    {
        lock (_gate)
        {
            if (!_uses.TryGetValue(url, out var use))
                return;
            if (use.Count > 1)
            {
                use.Count--;
                return;
            }

            _uses.Remove(url);
            try
            {
                File.Delete(use.Path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public void ReleaseLater(string url)
    {
        if (url.Length > 0)
            _ = Task.Run(() => Release(url));
    }

    public void CleanOldOnce(Action<string> failed)
    {
        if (Interlocked.Exchange(ref _cleaning, 1) == 0)
            _ = Task.Run(() =>
            {
                if (CleanOld() is { } failure)
                    failed(failure);
            });
    }

    internal string? CleanOld()
    {
        string? failure = null;
        try
        {
            if (!Directory.Exists(_root) || (File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0)
                return null;
            var cutoff = _time.GetUtcNow().UtcDateTime.AddDays(-1);
            var options = new EnumerationOptions
            { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var file in Directory.EnumerateFiles(_root, "*", options))
                Try(() =>
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                });
            foreach (var dir in Directory.EnumerateDirectories(_root, "*",
                         new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint }))
                Try(() =>
                {
                    if (!string.Equals(dir, _folder, StringComparison.OrdinalIgnoreCase) &&
                        !Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            failure ??= e.Message;
        }

        return failure is null ? null : $"Cannot clean old pictures from {_root}: {failure}";

        void Try(Action delete)
        {
            try
            {
                delete();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failure ??= e.Message;
            }
        }
    }

    private static byte[]? Read(Stream input, CancellationToken cancellation)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return bytes.ToArray();
            if (bytes.Length + read > MaxBytes)
                return null;
            bytes.Write(buffer, 0, read);
        }
    }

    private static bool TryWrite(string path, byte[] data)
    {
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
            }

            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }

            return false;
        }
    }

    private sealed class Use(string path)
    {
        public int Count = 1;
        public string Path { get; } = path;
    }
}
