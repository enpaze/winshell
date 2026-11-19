using System.ComponentModel;
using System.Diagnostics;

namespace Winshell.Tests;

public class DevStartTests
{
    private const string NeedsSdk = "Building a shell needs the .NET SDK, from https://dot.net/download.";

    [Fact]
    public void A_dotnet_that_cannot_run_names_that_failure()
    {
        var (path, project) = Folders();
        File.WriteAllText(Path.Combine(path, "dotnet.exe"), "not a program");
        File.WriteAllText(Path.Combine(project, "Dev.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = Run(project, path);

        result.AssertExitCode(1);
        Assert.Equal("", result.Output);
        Assert.Equal(FailureReason(Path.Combine(path, "dotnet.exe"), project) + Environment.NewLine, result.Error);
    }

    [Fact]
    public void A_missing_dotnet_still_asks_for_the_sdk()
    {
        var (path, project) = Folders();
        File.WriteAllText(Path.Combine(project, "Dev.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = Run(project, path);

        result.AssertExitCode(1);
        Assert.Equal("", result.Output);
        Assert.Equal(NeedsSdk + Environment.NewLine, result.Error);
    }

    private static (string Path, string Project) Folders()
    {
        var root = TempFolder.NewPath();
        var path = Path.Combine(root, "path");
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(project);
        return (path, project);
    }

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
            Assert.Contains(file, e.Message, StringComparison.Ordinal);
            return e.Message.Replace(file, "dotnet", StringComparison.Ordinal);
        }
    }

    private static ChildProcess Run(string project, string path)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(
            ["dev", "-p", project],
            [.. IsolatedFolders.Under(Path.GetDirectoryName(project)!), ("PATH", path)]);
#pragma warning restore CsWinRT1030
    }
}
