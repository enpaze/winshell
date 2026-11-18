using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Windows.Win32;

namespace Winshell.Tests;

public sealed partial class DevCommandTests : IDisposable
{
    private static readonly TimeSpan BuildPatience = TimeSpan.FromSeconds(140);

    private const string ProbeApp = "WinshellDevProbe";

    private readonly string _dir = TempFolder.NewPath();
    private readonly StringBuilder _printed = new();
    private readonly Dictionary<int, Process> _nodes = [];
    private Process? _dev;
    private string? _probeLink;

    private string Project => Path.Combine(_dir, "project");

    private (string, string)[] Folders => IsolatedFolders.Under(_dir);

    public DevCommandTests()
    {
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Path.Combine(_dir, "config"));
        Directory.CreateDirectory(Path.Combine(_dir, "local"));
        File.Copy(Path.Combine(Repo.Root, "global.json"), Path.Combine(Project, "global.json"));
        var props = Path.Combine(Repo.Root, "src", "Winshell.Package", "buildTransitive", "Winshell.props");
        File.WriteAllText(Path.Combine(Project, "Dev.csproj"), $"""
                                                                <Project Sdk="Microsoft.NET.Sdk">
                                                                  <Import Project="{props}" />
                                                                  <PropertyGroup>
                                                                    <OutputType>WinExe</OutputType>
                                                                    <TargetFramework>net11.0-windows10.0.22621.0</TargetFramework>
                                                                    <ImplicitUsings>enable</ImplicitUsings>
                                                                    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
                                                                    <SelfContained>true</SelfContained>
                                                                    <AssemblyName>Dev</AssemblyName>
                                                                  </PropertyGroup>
                                                                  <ItemGroup>
                                                                    <ProjectReference Include="{Path.Combine(Repo.Root, "src", "Winshell.Shell", "Winshell.Shell.csproj")}" />
                                                                  </ItemGroup>
                                                                </Project>
                                                                """);
        File.WriteAllText(Path.Combine(Project, "Program.cs"), """
                                                               internal sealed class Bare : ShellConfiguration
                                                               {
                                                                   public override void Start(Shell shell)
                                                                   {
                                                                       _ = new PanelWindow { Anchors = Edges.Top | Edges.Left, Width = 200, Height = 32, Content = new Dev.PanelView(), Visible = true };
                                                                       shell.Ipc.Handle("probe", "value", Marker.Value);
                                                                       shell.Ipc.Handle("probe", "reuse", () => Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE") ?? "");
                                                                       shell.Ipc.Handle("probe", "launch", (string name) =>
                                                                       {
                                                                           var app = shell.Apps.Find(name);
                                                                           if (app is null)
                                                                               return "missing";
                                                                           app.Launch();
                                                                           return "launched";
                                                                       });
                                                                   }
                                                               }

                                                               internal static class Program
                                                               {
                                                                   [STAThread]
                                                                   private static void Main() => Shell.Run<Bare>(new ShellOptions { ShellId = "dev-probe" });
                                                               }
                                                               """);
        File.WriteAllText(Path.Combine(Project, "PanelView.cs"), """
                                                                 using Microsoft.UI.Xaml.Controls;

                                                                 namespace Dev;

                                                                 public sealed class PanelView : UserControl
                                                                 {
                                                                     public PanelView() => Content = new TextBlock { Text = "probe" };
                                                                 }
                                                                 """);
        Mark("""=> "one";""");
    }

    public void Dispose()
    {
        using (var probe = ProbeProcess())
        {
            try
            {
                probe?.Kill();
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
            }
        }

        if (_probeLink is not null)
            Wait.Until(RemovedProbe, "the probe shortcut to be removed");

        if (_dev is not null)
        {
            _dev.Kill(true);
            _dev.WaitForExit();
            _dev.Dispose();
            lock (_printed)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(_printed.ToString());
            }
        }

        foreach (var node in _nodes.Values)
            node.Dispose();
        Wait.Until(Removed, "the test folder to be free");
    }

    private bool Removed()
    {
        try
        {
            Directory.Delete(_dir, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Mark(string body)
    {
        File.WriteAllText(Path.Combine(Project, "Marker.cs"),
            $"internal static class Marker {{ public static string Value() {body} }}");
    }

    private Task<(int Code, string Text)> Value()
    {
        return ReadValue("value");
    }

    private async Task<(int Code, string Text)> ReadValue(string member)
    {
        var instance = new Instances(Path.Combine(_dir, "local", "run")).Running().SingleOrDefault();
        if (instance is null)
            return (1, "");
        var answer = await InstanceCall.AskAsync(instance, IpcRequest.ForCall("probe", member, []),
            TestContext.Current.CancellationToken);
        return answer.Response is { Ok: true } response ? (0, response.Value?.Trim() ?? "") : (1, "");
    }

    private ChildProcess CallValue()
    {
        return WinshellCli.Run(["ipc", "call", "probe", "value"], Folders);
    }

    private string InstanceId()
    {
        return new Instances(Path.Combine(_dir, "local", "run")).Running().Single().Id;
    }

    private int ShellProcess()
    {
        return new Instances(Path.Combine(_dir, "local", "run")).Running().Single().Pid;
    }

    private void StartDev(bool ownGroup = false)
    {
        var info = WinshellCli.StartInfo(["dev", "-p", Project]);
        info.CreateNoWindow = !ownGroup;
        info.CreateNewProcessGroup = ownGroup;
        foreach (var (name, value) in Folders)
            info.Environment[name] = value;
        info.Environment["MSBUILDNODEHANDSHAKESALT"] = _dir;
        info.Environment.Remove("MSBUILDDISABLENODEREUSE");
        _dev = Process.Start(info)!;
        foreach (var stream in new[] { _dev.StandardOutput, _dev.StandardError })
            new Thread(() =>
                {
                    while (stream.ReadLine() is { } line)
                        lock (_printed)
                        {
                            _printed.AppendLine(line);
                        }
                })
            { IsBackground = true }.Start();
    }

    private List<string> FromProject()
    {
        using var search =
            new ManagementObjectSearcher("SELECT ProcessId, Name, CommandLine, ExecutablePath FROM Win32_Process");
        using var found = search.Get();
        return found.Cast<ManagementObject>()
            .Where(p => $"{p["CommandLine"]} {p["ExecutablePath"]}".Contains(Project,
                StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p["Name"]} {p["ProcessId"]}")
            .ToList();
    }

    private void NoteDevNodes()
    {
        if (_dev is null)
            return;
        var rows = Processes();
        var byId = new Dictionary<int, ProcessRow>(rows.Count);
        foreach (var row in rows)
            byId[row.Pid] = row;
        foreach (var row in rows)
        {
            if (!row.Command.Contains("nodemode", StringComparison.OrdinalIgnoreCase) || _nodes.ContainsKey(row.Pid))
                continue;
            if (Reaches(row, byId, _dev.Id) && Open(row) is { } node)
                _nodes[row.Pid] = node;
        }
    }

    private async Task NodesExit()
    {
        var token = TestContext.Current.CancellationToken;
        try
        {
            await Task.WhenAll(_nodes.Values.Select(node => node.WaitForExitAsync(token)))
                .WaitAsync(BuildPatience, TimeProvider.System, token);
        }
        catch (TimeoutException)
        {
            var alive = _nodes.Values.Where(node => !node.HasExited).Select(node => node.Id).ToList();
            var commands = Processes().ToDictionary(row => row.Pid, row => row.Command);
            Assert.Fail("Timed out: winshell dev's MSBuild nodes to exit: " + string.Join("; ",
                alive.Select(pid => $"{pid} {commands.GetValueOrDefault(pid, "(has exited since)")}")));
        }
    }

    private static bool Reaches(ProcessRow row, Dictionary<int, ProcessRow> byId, int root)
    {
        var seen = new HashSet<int>();
        while (row.Pid != root)
        {
            if (!seen.Add(row.Pid) || !byId.TryGetValue(row.Parent, out var parent) || parent.Created > row.Created)
                return false;
            row = parent;
        }

        return true;
    }

    private static Process? Open(ProcessRow row)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(row.Pid);
            _ = process.Handle;
            if ((process.StartTime - row.Created).Duration() < TimeSpan.FromMilliseconds(1))
                return process;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
        }

        process?.Dispose();
        return null;
    }

    private readonly record struct ProcessRow(int Pid, DateTime Created, int Parent, string Command);

    private static List<ProcessRow> Processes()
    {
        using var search =
            new ManagementObjectSearcher(
                "SELECT ProcessId, CreationDate, ParentProcessId, CommandLine FROM Win32_Process");
        using var found = search.Get();
        return found.Cast<ManagementObject>()
            .Select(p => new ProcessRow(
                Number(p["ProcessId"]),
                p["CreationDate"] is string created ? ManagementDateTimeConverter.ToDateTime(created) : default,
                Number(p["ParentProcessId"]),
                p["CommandLine"] as string ?? ""))
            .ToList();
    }

    private static int Number(object? value)
    {
        return value switch
        {
            uint number => unchecked((int)number),
            int number => number,
            _ => 0
        };
    }

    private bool Printed(string text)
    {
        lock (_printed)
        {
            return _printed.ToString().Contains(text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void With_no_project_it_says_how_to_make_one()
    {
        var result = WinshellCli.Run(["dev"], Folders);

        result.AssertExitCode(1);
        Assert.Contains("Run winshell new to make one.", result.Error, StringComparison.Ordinal);
    }

    [Fact(Timeout = 420_000)]
    [Trait("Category", "Window")]
    public async Task Edits_an_opened_app_and_Ctrl_Break_keep_the_watch_lifecycle_correct()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        Assert.True(ProcessConsole.Window() != 0 || PInvoke.GetConsoleProcessList(new uint[1]) > 0,
            "The test program has no console to send a Ctrl+Break on.");
        CreateProbeShortcut();
        StartDev(true);
        await Wait.UntilAsync(async () =>
        {
            NoteDevNodes();
            return await Value() == (0, "one");
        }, "the first build", BuildPatience);
        Assert.True(Printed("dotnet watch : "), "winshell dev printed nothing of dotnet watch's.");
        Assert.False(Printed("Ctrl+R") || Printed("Ctrl + R"), "winshell dev printed dotnet watch's Ctrl+R hint.");
        Assert.Contains(FromProject(), p => p.StartsWith("Dev.exe ", StringComparison.Ordinal));
        var reuse = await ReadValue("reuse");
        Assert.Equal(0, reuse.Code);
        Assert.Equal("", reuse.Text);
        await NodesExit();
        var shell = ShellProcess();
        var id = InstanceId();
        await Wait.UntilAsync(async () => await LaunchProbe() == "launched", "the app to open",
            TimeSpan.FromSeconds(60));
        await Wait.UntilAsync(ProbeAlive, "the opened app");
        await Wait.UntilAsync(() => File.Exists(Path.Combine(_dir, "reuse.txt")), "the opened app's environment");
        Assert.Equal("", (await File.ReadAllTextAsync(Path.Combine(_dir, "reuse.txt"),
            TestContext.Current.CancellationToken)).Trim());
        Mark("""=> "two";""");
        await Wait.UntilAsync(async () => (await Value()).Text == "two", "the first edit", BuildPatience);

        Mark("""=> "broken" """);
        await Wait.UntilAsync(() => Printed("error CS1002"), "the compile error");
        var value = CallValue();
        value.AssertExitCode(0);
        Assert.Equal("two", value.Output.Trim());
        await Wait.UntilAsync(
            () => WinshellCli.Run(["log", "-t", "20"], Folders).Output
                .Contains("Your configuration did not build", StringComparison.Ordinal), "the notice");

        Mark("""=> "three";""");
        await Wait.UntilAsync(async () => (await Value()).Text == "three", "the fix", BuildPatience);

        Assert.Equal(shell, ShellProcess());
        Assert.True(ProbeAlive(), "The app opened from the shell ended when the edit was applied.");

        Assert.True(PInvoke.GenerateConsoleCtrlEvent(CtrlBreakEvent, (uint)_dev!.Id), "Ctrl+Break was not sent.");
        Wait.Until(() => _dev.HasExited, "winshell dev to exit");
        Wait.Until(() => FromProject().Count == 0, "dotnet watch and the shell to end", TimeSpan.FromSeconds(30));
        var record = await File.ReadAllTextAsync(Path.Combine(_dir, "local", "run", id, "instance.json"),
            TestContext.Current.CancellationToken);
        Assert.NotNull(JsonSerializer.Deserialize(record, InstanceJson.Default.InstanceInfo)?.ExitedAt);
        Assert.True(ProbeAlive(), "The app opened from the shell ended when winshell dev ended.");
    }

    private const uint CtrlBreakEvent = 1;

    private void CreateProbeShortcut()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        _probeLink = Path.Combine(programs, ProbeApp + ".lnk");
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var pidFile = Path.Combine(_dir, "probe.pid");
        var arguments = "-NoProfile -WindowStyle Hidden -Command \"Set-Content -LiteralPath '" + pidFile +
                        "' -Value $PID; Set-Content -LiteralPath '" + Path.Combine(_dir, "reuse.txt") +
                        "' -Value ([string]$env:MSBUILDDISABLENODEREUSE); Start-Sleep -Seconds 900\"";
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        Assert.NotNull(shellType);
        var shell = Activator.CreateInstance(shellType)!;
        var link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [_probeLink],
            CultureInfo.InvariantCulture)!;
        var linkType = link.GetType();
        linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, [powershell],
            CultureInfo.InvariantCulture);
        linkType.InvokeMember("Arguments", BindingFlags.SetProperty, null, link, [arguments],
            CultureInfo.InvariantCulture);
        linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, [_dir],
            CultureInfo.InvariantCulture);
        linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null, CultureInfo.InvariantCulture);
    }

    private async Task<string> LaunchProbe()
    {
        var instance = new Instances(Path.Combine(_dir, "local", "run")).Running().SingleOrDefault();
        if (instance is null)
            return "";
        var answer = await InstanceCall.AskAsync(instance, IpcRequest.ForCall("probe", "launch", [ProbeApp]),
            TestContext.Current.CancellationToken);
        return answer.Response is { Ok: true } response ? response.Value?.Trim() ?? "" : "";
    }

    private bool ProbeAlive()
    {
        using var process = ProbeProcess();
        return process is not null;
    }

    private Process? ProbeProcess()
    {
        var file = Path.Combine(_dir, "probe.pid");
        if (!File.Exists(file) || !int.TryParse(File.ReadAllText(file).Trim(), out var pid))
            return null;
        try
        {
            var process = Process.GetProcessById(pid);
            if (!process.HasExited && process.ProcessName.Equals("powershell", StringComparison.OrdinalIgnoreCase))
                return process;
            process.Dispose();
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private bool RemovedProbe()
    {
        try
        {
            if (_probeLink is not null)
                File.Delete(_probeLink);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
