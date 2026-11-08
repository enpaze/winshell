using System.Runtime.CompilerServices;

namespace Winshell.TestSupport;

// Each test project compiles this file, so a use in another project is invisible here.
// CsWinRT1028 requires partial. The WinUI test projects generate the other part.
/// <summary>
/// A new folder under <c>%TEMP%\winshell-tests</c>, deleted on dispose. Every test program prunes folders a crashed
/// run left there once at startup; a folder from today stays. A test gets every temp path here: the ban list rejects
/// <c>Path.GetTempPath</c> anywhere else.
/// </summary>
internal sealed partial class TempFolder : IDisposable
{
#pragma warning disable RS0030 // The one root every test folder lives under.
    internal static readonly string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winshell-tests");
#pragma warning restore RS0030

    public TempFolder()
    {
        Path = NewPath();
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>A folder path under <see cref="Root"/> that nothing has made yet, for a fixture that deletes its own folder.</summary>
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
