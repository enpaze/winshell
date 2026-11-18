using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Winshell.Tests;

internal static class ProbeShell
{
    private const string ShellIdVariable = "PROBE_SHELL_ID";

    private static readonly object Gate = new();

    private static string? _program;

    internal static string Program
    {
        get
        {
            Ensure();
            return _program!;
        }
    }

    internal static Process Start(string config, string local, string? shellId = null, string? program = null)
    {
        var process = Launch(config, local, [], shellId, program: program);
        Wait.Until(() => new Instances(Path.Combine(local, "run")).Running().Any(info => info.Pid == process.Id),
            $"probe {process.Id} to publish its instance");
        return process;
    }

    internal static Process Launch(string config, string local, string[] args, string? shellId = null,
        StringBuilder? printed = null, string? program = null)
    {
        var info = new ProcessStartInfo(program ?? Program);
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        foreach (var (name, value) in IsolatedFolders.Of(config, local))
            info.Environment[name] = value;
        if (shellId is not null)
            info.Environment[ShellIdVariable] = shellId;
        return ChildProcess.Start(info, printed);
    }

    internal static ChildProcess Detach(string config, string local, string[] args)
    {
        var info = new ProcessStartInfo(Program);
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        foreach (var (name, value) in IsolatedFolders.Of(config, local))
            info.Environment[name] = value;
        return ChildProcess.Run(info, TimeSpan.FromSeconds(20));
    }

    internal static void Ensure()
    {
        if (Volatile.Read(ref _program) is { } built && File.Exists(built))
            return;
        lock (Gate)
        {
            if (_program is not null && File.Exists(_program))
                return;
            var checkout = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Repo.Root)))[..12];
            Volatile.Write(ref _program, Build(Path.Combine(TempFolder.Root, "probe-shell-" + checkout)));
        }
    }

    internal static string Build(string folder)
    {
        lock (Gate)
        {
            return BuildLocked(folder);
        }
    }

    private static string BuildLocked(string folder)
    {
        var root = Repo.Root;
        Directory.CreateDirectory(folder);
        Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow);
        WriteIfDifferent(Path.Combine(folder, "global.json"), File.ReadAllText(Path.Combine(root, "global.json")));
        var project = Path.Combine(folder, "Probe.csproj");
        WriteIfDifferent(project, Project(Path.Combine(root, "src", "Winshell.Shell", "Winshell.Shell.csproj")));
        WriteIfDifferent(Path.Combine(folder, "Program.cs"), Source);
        var program = Path.Combine(folder, "bin", "shell", "Probe.exe");
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = folder };
        info.ArgumentList.Add("build");
        info.ArgumentList.Add(project);
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("Release");
        info.ArgumentList.Add("-o");
        info.ArgumentList.Add(Path.Combine(folder, "bin", "shell"));
        info.ArgumentList.Add("--nologo");
        info.ArgumentList.Add("--disable-build-servers");
        ChildProcess.Run(info, TimeSpan.FromMinutes(10)).AssertExitCode(0);
        Assert.True(File.Exists(program), $"The probe built, but {program} is not there.");
        return program;
    }

    private static void WriteIfDifferent(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return;
        File.WriteAllText(path, text);
    }


    private static string Project(string shellProject)
    {
        var props = Path.Combine(Repo.Root, "src", "Winshell.Package", "buildTransitive", "Winshell.props");
        var configuration = typeof(ProbeShell).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        return $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="{props}" />
                  <PropertyGroup>
                    <OutputType>WinExe</OutputType>
                    <TargetFramework>net11.0-windows10.0.22621.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <RuntimeIdentifier Condition="'$(RuntimeIdentifier)' == ''">$(NETCoreSdkRuntimeIdentifier)</RuntimeIdentifier>
                    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
                    <AssemblyName>Probe</AssemblyName>
                    <DisableWinExeOutputInference>true</DisableWinExeOutputInference>
                  </PropertyGroup>
                  <!-- The SDK adds transitive references too. Give all of them the tested configuration and keep
                       this probe's -o path out of their builds, including when reload builds the probe in Release. -->
                  <ItemDefinitionGroup>
                    <ProjectReference>
                      <SetConfiguration>Configuration={configuration}</SetConfiguration>
                      <GlobalPropertiesToRemove>OutputPath</GlobalPropertiesToRemove>
                    </ProjectReference>
                  </ItemDefinitionGroup>
                  <ItemGroup>
                    <ProjectReference Include="{shellProject}" />
                  </ItemGroup>
                </Project>
                """;
    }

    internal const string HangMarker = "probe-hang";

    internal const string HideTaskbarMarker = "probe-hide-taskbar";

    internal const string CrashMarker = "probe-crash";

    internal const string WindowMarker = "probe-window";

    internal const string PreferencesMarker = "probe-preferences";

    private const string Source =
        $$"""
          using Winshell;

          using System.Diagnostics;
          using Microsoft.UI.Xaml.Controls;
          using Windows.Foundation;

          public sealed class ProbePage : StackPanel;

          public sealed class ProbePopup : StackPanel;

          sealed class EarlySection() : Preferences("early");

          sealed class LateSection() : Preferences("late");

          internal sealed class Bare : ShellConfiguration
          {
              public override void Start(Shell shell)
              {
                  shell.Ipc.Handle("probe", "note", (string text) => { });
                  shell.Ipc.Property("probe", "name", () => "probe");
                  var config = Environment.GetEnvironmentVariable("WINSHELL_CONFIG_DIR");
                  // A test that puts {{CrashMarker}} there gets a shell that crashes as it starts, once.
                  if (config is not null && File.Exists(Path.Combine(config, "{{CrashMarker}}")))
                  {
                      File.Delete(Path.Combine(config, "{{CrashMarker}}"));
                      throw new InvalidOperationException("The probe was asked to crash as it started.");
                  }
                  // A test that puts {{PreferencesMarker}} there gets one section during Start and one posted for the
                  // turn after. preferences-report.txt then says both, missing, or partial. Nothing is shown.
                  if (config is not null && File.Exists(Path.Combine(config, "{{PreferencesMarker}}")))
                  {
                      new EarlySection();
                      shell.Loop.Post(() => new LateSection());
                      shell.Loop.Post(() =>
                      {
                          var path = Path.Combine(shell.ConfigRoot, "preferences", shell.ShellId + ".toml");
                          var report = Path.Combine(config, "preferences-report.txt");
                          // The test reads the report from another process, so it appears once and whole: written
                          // beside it, then moved to a name that does not exist yet, which no reader can hold open.
                          // Antivirus can lock a file just written, so a refused move is tried again shortly.
                          var written = report + ".tmp";
                          void Publish()
                          {
                              try
                              {
                                  File.Move(written, report);
                              }
                              catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                              {
                                  shell.Loop.Schedule(TimeSpan.FromMilliseconds(25), Publish);
                              }
                          }

                          void Report()
                          {
                              File.WriteAllText(written, PreferencesReport(path));
                              Publish();
                          }

                          if (File.Exists(path))
                              Report();
                          else
                              shell.Loop.Post(Report);
                      });
                  }
                  // A test that puts {{WindowMarker}} there gets a panel whose button opens a popup.
                  if (config is not null && File.Exists(Path.Combine(config, "{{WindowMarker}}")))
                  {
                      var popup = new PopupWindow { Content = new ProbePopup { Children = { new TextBlock { Text = "Opened" } } } };
                      var open = new Button { Name = "Open", Content = "Open" };
                      var panel = new PanelWindow
                      {
                          Anchors = Edges.Top | Edges.Left,
                          Width = 160,
                          Height = 60,
                          Content = new ProbePage { Children = { open } },
                          Visible = true,
                      };
                      open.Click += (_, _) =>
                      {
                          popup.Rect = new Rect(panel.Origin.X, panel.Origin.Y + panel.ActualHeight, 160, 60);
                          popup.Visible = true;
                      };
                  }
                  // A test that puts {{HideTaskbarMarker}} there gets a shell that hides Explorer's taskbar once.
                  if (config is not null && File.Exists(Path.Combine(config, "{{HideTaskbarMarker}}")))
                  {
                      shell.ExplorerTaskbar.Hidden = true;
                      File.Delete(Path.Combine(config, "{{HideTaskbarMarker}}"));
                  }
                  // A test that puts {{HangMarker}} there gets a shell that starts a program and then never exits.
                  if (config is null || !File.Exists(Path.Combine(config, "{{HangMarker}}")))
                      return;
                  var child = Process.Start(new ProcessStartInfo("ping", "-n 120 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true })!;
                  // Moved into place whole, so a test that sees the file can read it.
                  File.WriteAllText(Path.Combine(config, "child.pid.tmp"), child.Id.ToString());
                  File.Move(Path.Combine(config, "child.pid.tmp"), Path.Combine(config, "child.pid"));
                  AppDomain.CurrentDomain.ProcessExit += (_, _) => Thread.Sleep(Timeout.Infinite);
              }

              /// <summary>What the turn after Start can already see in the preferences file.</summary>
              private static string PreferencesReport(string path)
              {
                  if (!File.Exists(path))
                      return "missing";
                  var text = File.ReadAllText(path);
                  return text.Contains("[early]") && text.Contains("[late]") ? "both" : "partial";
              }
          }

          internal static class Program
          {
              [STAThread]
              private static void Main() =>
                  Shell.Run<Bare>(new ShellOptions { ShellId = Environment.GetEnvironmentVariable("{{ShellIdVariable}}") ?? "probe" });
          }
          """;
}
