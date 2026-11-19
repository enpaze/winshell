using System.Diagnostics;
using System.Text.Json;
using Winshell.Commands;

namespace Winshell.Tests;

public class CliRunCommandTests
{
    [Fact]
    public void No_arguments_print_help_and_do_not_start_a_shell()
    {
        var result = WinshellCli.RunWithEmptyConfig([]);

        result.AssertExitCode(0);
        result.AssertNoError();
        Assert.Contains("winshell run", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_with_a_missing_path_fails_before_a_shell_starts()
    {
        var missing = TempFolder.NewPath();
        var result = WinshellCli.RunWithEmptyConfig(["run", "-p", missing]);

        result.AssertExitCode(1);
        Assert.Equal("", result.Output);
        Assert.Contains("No .csproj", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("--no-build --no-duplicate")]
    [InlineData("--no-duplicate")]
    public void Run_with_no_shell_from_a_console_says_how_to_make_one_exits_1_and_starts_nothing(string args)
    {
        var root = TempFolder.NewPath();
        var config = Path.Combine(root, "config");
        var local = Path.Combine(root, "local");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(local);
        try
        {
#pragma warning disable CsWinRT1030
            var result = WinshellCli.Run(args.Split(' '), IsolatedFolders.Of(config, local));
#pragma warning restore CsWinRT1030

            Assert.Equal(
                (1, "", $"No shell project in {config}, so nothing started. Run winshell new to make one." +
                        Environment.NewLine),
                (result.Code, result.Output, result.Error));
            AssertNothingRuns(config, local);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("run")]
    [InlineData("--no-build --no-duplicate")]
    [InlineData("--no-duplicate")]
    public void Run_with_no_shell_and_no_console_window_exits_0_says_nothing_and_starts_nothing(string args)
    {
        var root = TempFolder.NewPath();
        var config = Path.Combine(root, "config");
        var local = Path.Combine(root, "local");
        var output = Path.Combine(root, "output.txt");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(local);
        try
        {
#pragma warning disable CsWinRT1030
            var info = WinshellCli.StartInfo(args.Split(' '));
#pragma warning restore CsWinRT1030
            info.RedirectStandardOutput = false;
            info.RedirectStandardError = false;
            info.StandardOutputEncoding = null;
            info.StandardErrorEncoding = null;
            foreach (var (name, value) in IsolatedFolders.Of(config, local))
                info.Environment[name] = value;
            using (var none = File.OpenNullHandle())
            using (var written = File.OpenHandle(output, FileMode.Create, FileAccess.Write))
            {
                info.InheritedHandles = [];
                info.StandardInputHandle = none;
                info.StandardOutputHandle = written;
                info.StandardErrorHandle = none;
                using var process = Process.Start(info)!;
                Assert.True(process.WaitForExit(TimeSpan.FromSeconds(60)), "winshell did not exit.");
                Assert.Equal(0, process.ExitCode);
            }

            Assert.Equal("", File.ReadAllText(output));
            AssertNothingRuns(config, local);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void The_sign_in_entry_with_no_shell_exits_0_and_starts_nothing()
    {
        var root = TempFolder.NewPath();
        var config = Path.Combine(root, "config");
        var local = Path.Combine(root, "local");
        var signIn = Path.Combine(root, "sign-in.cmd");
        var exit = Path.Combine(root, "exit.txt");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(local);
        File.WriteAllLines(signIn,
        [
            .. IsolatedFolders.Of(config, local).Select(static v => $"@set \"{v.Name}={v.Value}\""),
            $"@\"{WinshellCli.Exe}\" --no-build --no-duplicate",
            $"@>\"{exit}\" echo %errorlevel%"
        ]);
        try
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "conhost.exe"))
            {
                UseShellExecute = true
            };
            foreach (var arg in new[] { "--headless", "cmd.exe", "/d", "/c", signIn })
                info.ArgumentList.Add(arg);
            using (var process = Process.Start(info)!)
                Assert.True(process.WaitForExit(TimeSpan.FromSeconds(60)), "conhost did not exit.");

            Assert.Equal("0", File.ReadAllText(exit).Trim());
            AssertNothingRuns(config, local);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Run_with_no_shell_that_nobody_reads_exits_0_and_writes_nothing()
    {
        using var error = new StringWriter();

        var exit = RunCommand.NoProject(@"C:\shells", true, error);

        Assert.Equal((0, ""), (exit, error.ToString()));
    }

    [Fact]
    public void Run_with_no_shell_that_someone_reads_says_how_to_make_one_and_exits_1()
    {
        using var error = new StringWriter();

        var exit = RunCommand.NoProject(@"C:\shells", false, error);

        Assert.Equal(
            (1, @"No shell project in C:\shells, so nothing started. Run winshell new to make one." + Environment.NewLine),
            (exit, error.ToString()));
    }

    private static void AssertNothingRuns(string config, string local)
    {
        var listed = WinshellCli.Run(["list", "--json"], IsolatedFolders.Of(config, local));
        listed.AssertExitCode(0);
        Assert.Equal("[]", listed.Output.Trim());
    }

    [Fact]
    public void No_duplicate_exits_quietly_before_building_when_the_project_already_runs()
    {
        var root = TempFolder.NewPath();
        try
        {
            var project = Path.Combine(root, "MyProject");
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "Bar.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(project, "Program.cs"), "this is not C#");
            var dir = Path.Combine(root, "run", "runningabc");
            Directory.CreateDirectory(dir);
            using var self = Process.GetCurrentProcess();
            File.WriteAllText(Path.Combine(dir, "instance.json"), JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["id"] = "runningabc",
                    ["pid"] = self.Id,
                    ["shellName"] = "bar",
                    ["program"] = Path.Combine(project, "bin", "shell.next", "Bar.exe"),
                    ["launchTime"] = DateTime.Now,
                    ["pipe"] = "winshell-runningabc",
                    ["log"] = Path.Combine(dir, "log.txt"),
                    ["processName"] = self.ProcessName,
                    ["processStart"] = self.StartTime.ToUniversalTime().Ticks
                }));

            var result = WinshellCli.Run(["run", "-n", "-p", project], ("WINSHELL_LOCAL_DIR", root));

            result.AssertExitCode(0);
            Assert.Equal("", result.Output);
            result.AssertNoError();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
