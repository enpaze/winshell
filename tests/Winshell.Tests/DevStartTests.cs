using System.ComponentModel;
using System.Diagnostics;

namespace Winshell.Tests;

/// <summary>
/// <c>winshell dev</c> says why <c>dotnet</c> did not start. A missing SDK stays the SDK sentence.
/// </summary>
public class DevStartTests
{
    private const string NeedsSdk = "Building a configuration needs the .NET SDK, from https://dot.net/download.";

    [Fact]
    public void A_dotnet_that_cannot_run_names_that_failure()
    {
        var root = TempFolder.NewPath();
        var path = Path.Combine(root, "path");
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(path, "dotnet.exe"), "not a program");
        File.WriteAllText(Path.Combine(project, "Dev.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = Run(project, path);

        result.AssertExitCode(1);
        Assert.Equal("", result.Output);
        Assert.DoesNotContain(NeedsSdk, result.Error, StringComparison.Ordinal);
        Assert.Contains(FailureReason(Path.Combine(path, "dotnet.exe"), project), result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_dotnet_still_asks_for_the_sdk()
    {
        var root = TempFolder.NewPath();
        var path = Path.Combine(root, "path");
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Dev.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = Run(project, path);

        result.AssertExitCode(1);
        Assert.Equal("", result.Output);
        Assert.Equal(NeedsSdk + Environment.NewLine, result.Error);
    }

    /// <summary>
    /// The runtime's own reason <paramref name="file"/> cannot start. <c>winshell dev</c> prints that sentence, so the
    /// test does not depend on one language's wording.
    /// </summary>
    private static string FailureReason(string file, string folder)
    {
        try
        {
            using var started = Process.Start(new ProcessStartInfo(file)
            {
                WorkingDirectory = folder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            Assert.Fail($"\"{file}\" started.");
            return "";
        }
        catch (Win32Exception e)
        {
            var split = e.Message.LastIndexOf("'. ", StringComparison.Ordinal);
            Assert.True(split >= 0, e.Message);
            return e.Message[(split + 3)..];
        }
    }

    private static ChildProcess Run(string project, string path)
    {
#pragma warning disable CsWinRT1030 // Not marshalled across the WinRT ABI.
        return WinshellCli.Run(
            ["dev", "-p", project],
            ("WINSHELL_CONFIG_DIR", Path.Combine(Path.GetDirectoryName(project)!, "config")),
            ("WINSHELL_LOCAL_DIR", Path.Combine(Path.GetDirectoryName(project)!, "local")),
            ("PATH", path));
#pragma warning restore CsWinRT1030
    }
}
