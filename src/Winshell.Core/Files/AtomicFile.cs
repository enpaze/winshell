using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Winshell.Native;

namespace Winshell;

internal enum AtomicSave
{
    Saved,

    HandSaveWon
}

internal static class AtomicFile
{
    public static AtomicSave Save(string path, string text, bool bom, string? expectedOnDisk)
    {
        var destination = SaveTarget(path, out var hardLink);
        if (Path.GetDirectoryName(destination) is { Length: > 0 } folder)
            Directory.CreateDirectory(folder);
        DeleteStaleTemps(destination);
        var written = Write(destination, text, bom, hardLink,
            () => HandSaveWins(ReadText(destination), expectedOnDisk, text));
        return written ? AtomicSave.Saved : AtomicSave.HandSaveWon;
    }

    private static bool HandSaveWins(string? onDisk, string? diskAtStart, string incoming)
    {
        return onDisk is not null
               && !string.Equals(onDisk, diskAtStart, StringComparison.Ordinal)
               && !string.Equals(onDisk, incoming, StringComparison.Ordinal);
    }

    private static string? ReadText(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException
                                      or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool Write(string destination, string text, bool bom, bool inPlace, Func<bool> handSaved)
    {
        if (inPlace)
        {
            if (handSaved())
                return false;
            WriteText(destination, text, bom);
            return true;
        }

        var temp = destination + ".tmp-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
        WriteText(temp, text, bom);
        try
        {
            if (handSaved())
                return false;
            MoveReplacing(temp, destination);
            return true;
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static void WriteText(string path, string text, bool bom)
    {
        var encoding = new UTF8Encoding(bom);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using (var writer = new StreamWriter(stream, encoding, leaveOpen: true))
        {
            writer.Write(text);
            writer.Flush();
        }

        stream.Flush(true);
    }

    private static void MoveReplacing(string temp, string path)
    {
        int[] delays = [25, 50, 100, 200];
        for (var attempt = 0; ; attempt++)
            try
            {
                File.Move(temp, path, true);
                return;
            }
            catch (Exception e) when (FileErrors.IsLock(e) && attempt < delays.Length)
            {
                Thread.Sleep(delays[attempt]);
            }
    }

    private static void DeleteStaleTemps(string path)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        var name = Path.GetFileName(full);
        if (dir is null || name.Length == 0 || !Directory.Exists(dir))
            return;
        var prefix = name + ".tmp-";
        foreach (var candidate in Directory.EnumerateFiles(dir, prefix + "*"))
        {
            var file = Path.GetFileName(candidate);
            if (file.Length <= prefix.Length || !file.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var suffix = file[prefix.Length..];
            var dash = suffix.IndexOf('-');
            if (dash > 0
                && int.TryParse(suffix.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                && ProcessIsRunning(pid))
                continue;
            try
            {
                File.Delete(candidate);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool ProcessIsRunning(int pid)
    {
        if (pid == Environment.ProcessId)
            return true;
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return e is not ArgumentException;
        }
    }

    public static string FollowLink(string path)
    {
        var full = Path.GetFullPath(path);
        if (FileLinks.TryReparsePoint(full, out var reparse) && !reparse)
            return full;
        try
        {
            var target = new FileInfo(full).ResolveLinkTarget(true);
            if (target is not null)
                return target.FullName;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return full;
    }

    private static string SaveTarget(string path, out bool hardLink)
    {
        hardLink = false;
        var full = FollowLink(path);
        if (File.Exists(full))
            hardLink = LinkCount(full) > 1;
        return full;
    }

    private static int LinkCount(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return FileLinks.Count(stream.SafeFileHandle) ?? 1;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 1;
        }
    }
}
