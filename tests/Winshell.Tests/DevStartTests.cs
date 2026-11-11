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
    /// The part of <see cref="Process.Start(ProcessStartInfo)"/>'s failure that does not name <paramref name="file"/> or
    /// <paramref name="folder"/>. <c>winshell dev</c> prints that same failure for <c>dotnet</c>.
    /// </summary>
    private static string FailureReason(string file, string folder)
    {
        var elsewhere = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(folder)!, "other")).FullName;
        var here = StartFailure(file, folder);
        var there = StartFailure(file, elsewhere);
        var same = 0;
        while (same < here.Length && same < there.Length && here[here.Length - 1 - same] == there[there.Length - 1 - same])
            same++;
        var reason = here[^same..];
        var toldApart = here[..^same];
        Assert.Contains(file, toldApart, StringComparison.Ordinal);
        Assert.Contains(folder, toldApart, StringComparison.Ordinal);
        return reason;
    }

    private static string StartFailure(string file, string folder)
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
            return e.Message;
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
