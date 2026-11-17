using System.Runtime.CompilerServices;

namespace Winshell.TestSupport;

internal sealed partial class TempFolder : IDisposable
{
    internal static readonly string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winshell-tests");

    public TempFolder()
    {
        Path = NewPath();
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public static string NewPath()
    {
        return System.IO.Path.Combine(Root, Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, true);
    }

    [ModuleInitializer]
    internal static void PruneAtStart()
    {
        if (!Directory.Exists(Root))
            return;
        var cutoff = DateTime.UtcNow.AddDays(-1);
        foreach (var dir in Directory.EnumerateDirectories(Root))
            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) >= cutoff)
                    continue;
                Directory.Delete(dir, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
    }
}
