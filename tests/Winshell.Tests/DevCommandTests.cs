using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.Json;
using Windows.Win32;

namespace Winshell.Tests;

/// <summary>The real <c>winshell dev</c>, on a real project, editing its source.</summary>
public sealed class DevCommandTests : IDisposable
{
    private static readonly TimeSpan BuildPatience = TimeSpan.FromSeconds(140);

    private readonly string _dir = TempFolder.NewPath();
    private readonly StringBuilder _printed = new();
    private Process? _dev;

    private string Project => Path.Combine(_dir, "project");

    private (string, string)[] Folders =>
    [
        ("WINSHELL_CONFIG_DIR", Path.Combine(_dir, "config")),
        ("WINSHELL_LOCAL_DIR", Path.Combine(_dir, "local"))
    ];

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
                                                                    <TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>
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
                                                               using Winshell;

                                                               internal sealed class Bare : ShellConfiguration
                                                               {
                                                                   public override void Start(Shell shell)
                                                                   {
                                                                       var view = new Dev.PanelView();
                                                                       _ = new PanelWindow { Anchors = Edges.Top | Edges.Left, Width = 200, Height = 32, Content = view, Visible = true };
                                                                       shell.Ipc.Handle("probe", "value", Marker.Value);
                                                                       shell.Ipc.Handle("probe", "label", view.Shown);
                                                                   }
                                                               }

                                                               internal static class Program
                                                               {
                                                                   [STAThread]
                                                                   private static void Main() => Shell.Run<Bare>(new ShellOptions { ShellId = "dev-probe" });
                                                               }
                                                               """);
        File.WriteAllText(Path.Combine(Project, "PanelView.xaml.cs"), """
                                                                      using Microsoft.UI.Xaml.Controls;

                                                                      namespace Dev;

                                                                      public sealed partial class PanelView : UserControl
                                                                      {
                                                                          public PanelView() => InitializeComponent();

                                                                          public string Shown() => Label.Text;
                                                                      }
                                                                      """);
        Label("one");
        Mark("""=> "one";""");
    }

    public void Dispose()
    {
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

    /// <param name="needsBuild">Marks the label x:Load, which only a build applies.</param>
    private void Label(string text, bool needsBuild = false)
    {
        File.WriteAllText(Path.Combine(Project, "PanelView.xaml"), $"""
                                                                    <UserControl
                                                                        x:Class="Dev.PanelView"
                                                                        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                                                                        <TextBlock x:Name="Label" {(needsBuild ? "x:Load=\"True\" " : "")}Text="{text}" />
                                                                    </UserControl>
                                                                    """);
    }

    private async Task<string> Shown()
    {
        return (await ReadValue("label")).Text;
    }

    private Task<(int Code, string Text)> Value()
    {
        return ReadValue("value");
    }

    private async Task<(int Code, string Text)> ReadValue(string member)
    {
        var instance = new Instances(Path.Combine(_dir, "local", "run")).List(false).SingleOrDefault().Info;
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
        return new Instances(Path.Combine(_dir, "local", "run")).List(false).Single().Info.Id;
    }

    private int ShellProcess()
    {
        return new Instances(Path.Combine(_dir, "local", "run")).List(false).Single().Info.Pid;
    }

    /// <param name="ownGroup">
    /// In a process group of its own, on this test's console, so a Ctrl+Break can be sent to it alone.
    /// </param>
    private void StartDev(bool ownGroup = false)
    {
        var info = WinshellCli.StartInfo(["dev", "-p", Project]);
        info.CreateNoWindow = !ownGroup;
        info.CreateNewProcessGroup = ownGroup;
        foreach (var (name, value) in Folders)
            info.Environment[name] = value;
        // dotnet watch's builds leave idle MSBuild nodes that another test's build can take, and those nodes end
        // when dotnet watch does, failing that build with MSB4166. Without node reuse, dotnet watch's builds leave none.
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
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

    /// <summary>Every process this project started: dotnet watch, the build, and the shell, by command line or path.</summary>
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

    private bool Printed(string text)
    {
        lock (_printed)
        {
            return _printed.ToString().Contains(text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_downloaded_project_never_reaches_a_build()
    {
        File.WriteAllText(Path.Combine(Project, "Dev.csproj") + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");

        var result = WinshellCli.Run(["dev", "-p", Project], Folders);

        result.AssertExitCode(1);
        Assert.Contains("Dev.csproj", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(Project, "obj")), "dotnet ran on a downloaded project.");
    }

    [Fact]
    public void With_no_project_it_says_how_to_make_one()
    {
        var result = WinshellCli.Run(["dev"], Folders);

        result.AssertExitCode(1);
        Assert.Contains("Run winshell new to make one.", result.Error, StringComparison.Ordinal);
    }

    // One watch host covers in-place edits, a failed edit, a rebuild and graceful console shutdown.
    [Fact(Timeout = 300_000)]
    public async Task Edits_rebuilds_and_Ctrl_Break_keep_the_watch_lifecycle_correct()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        Assert.True(ProcessConsole.Window() != 0 || PInvoke.GetConsoleProcessList(new uint[1]) > 0,
            "The test program has no console to send a Ctrl+Break on.");
        StartDev(true);
        await Wait.UntilAsync(async () => await Value() == (0, "one"), "the first build", BuildPatience);
        Assert.True(Printed("dotnet watch : "), "winshell dev printed nothing of dotnet watch's.");
        Assert.False(Printed("Ctrl+R") || Printed("Ctrl + R"), "winshell dev printed dotnet watch's Ctrl+R hint.");
        Assert.Contains(FromProject(), p => p.StartsWith("Dev.exe ", StringComparison.Ordinal));
        var shell = ShellProcess();
        var id = InstanceId();
        // The first edit also warms the compiler up.
        Mark("""=> "two";""");
        await Wait.UntilAsync(async () => (await Value()).Text == "two", "the first edit", BuildPatience);

        Mark("""=> "broken" """);
        Wait.Until(() => Printed("error CS1002"), "the compile error");
        var value = CallValue();
        value.AssertExitCode(0);
        Assert.Equal("two", value.Output.Trim());
        Wait.Until(
            () => WinshellCli.Run(["log", "-t", "20"], Folders).Output
                .Contains("Your configuration did not build", StringComparison.Ordinal), "the notice");

        Mark("""=> "three";""");
        await Wait.UntilAsync(async () => (await Value()).Text == "three", "the fix", BuildPatience);

        Assert.Equal(shell, ShellProcess());

        Label("two");
        await Wait.UntilAsync(async () => await Shown() == "two", "the XAML edit", BuildPatience);
        Assert.Equal(shell, ShellProcess());

        Label("three", true);
        Wait.Until(() => Printed("Building, because the XAML you saved needs it."), "the build the XAML needs");
        await Wait.UntilAsync(async () => await Shown() == "three", "the shell to start again", BuildPatience);
        Assert.NotEqual(shell, ShellProcess());
        // A rebuild lets the old shell quit before replacing it: a forced end would not mark this record.
        var record = File.ReadAllText(Path.Combine(_dir, "local", "run", id, "instance.json"));
        Assert.NotNull(JsonSerializer.Deserialize(record, InstanceJson.Default.InstanceInfo)?.ExitedAt);
        var restartedId = InstanceId();

        // Ctrl+C reaches every process on the console, this test's too. Ctrl+Break reaches only this process group.
        Assert.True(PInvoke.GenerateConsoleCtrlEvent(CtrlBreakEvent, (uint)_dev!.Id), "Ctrl+Break was not sent.");
        Assert.True(_dev.WaitForExit(TimeSpan.FromSeconds(30)), "winshell dev did not exit.");
        Wait.Until(() => FromProject().Count == 0, "dotnet watch and the shell to end", TimeSpan.FromSeconds(30));
        // Shutdown also asks the replacement shell to quit before ending watch.
        record = File.ReadAllText(Path.Combine(_dir, "local", "run", restartedId, "instance.json"));
        Assert.NotNull(JsonSerializer.Deserialize(record, InstanceJson.Default.InstanceInfo)?.ExitedAt);
    }

    private const uint CtrlBreakEvent = 1;
}
