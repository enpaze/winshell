using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Winshell.Tests;

internal static class ProbeShell
{
    internal const string OtherShellName = "other";

    internal const string OtherDataDir = "data";

    internal const string OtherStateDir = "other-state";

    internal const string OtherCacheDir = "other-cache";

    private const string ProbeProperties = "<ShellName>probe</ShellName>";

    private const string PartsEndingVariable = "PROBE_PARTS_ENDING";

    private static readonly object Gate = new();

    private static string? _program;

    private static string? _other;

    private static string? _copy;

    internal static string Program => Built(ref _program, Path.Combine(TempFolder.Root, "probe-shell-" + Checkout),
        ProbeProperties);

    internal static string Other => Built(ref _other, OtherFolder, $"""
        <ShellName>{OtherShellName}</ShellName>
        <ShellDataDir>{OtherDataDir}</ShellDataDir>
        <ShellStateDir>$BASE\{OtherStateDir}</ShellStateDir>
        <ShellCacheDir>%WINSHELL_LOCAL_DIR%\{OtherCacheDir}</ShellCacheDir>
        """);

    internal static string Copy
    {
        get
        {
            var program = Program;
            if (Volatile.Read(ref _copy) is { } copied && File.Exists(copied))
                return copied;
            lock (Gate)
            {
                if (_copy is null || !File.Exists(_copy))
                    Volatile.Write(ref _copy, CopyLocked(program, Path.Combine(TempFolder.Root, "probe-copy-" + Checkout)));
                return _copy;
            }
        }
    }

    internal static string OtherFolder => Path.Combine(TempFolder.Root, "probe-other-" + Checkout);

    private static string Checkout => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Repo.Root)))[..12];

    internal static Process Start(string config, string local, string? program = null)
    {
        var process = Launch(config, local, [], program: program);
        Wait.Until(() => new Instances(Path.Combine(local, "run")).Running().Any(info => info.Pid == process.Id),
            $"probe {process.Id} to publish its instance");
        return process;
    }

    internal static Process Launch(string config, string local, string[] args, StringBuilder? printed = null,
        string? program = null)
    {
        var info = new ProcessStartInfo(program ?? Program);
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        foreach (var (name, value) in IsolatedFolders.Of(config, local))
            info.Environment[name] = value;
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

    private static string Built(ref string? program, string folder, string shellProperties)
    {
        if (Volatile.Read(ref program) is { } built && File.Exists(built))
            return built;
        lock (Gate)
        {
            if (program is null || !File.Exists(program))
                Volatile.Write(ref program, BuildLocked(folder, shellProperties, Source));
            return program;
        }
    }

    internal static string Build(string folder, string source = Source)
    {
        lock (Gate)
        {
            return BuildLocked(folder, ProbeProperties, source);
        }
    }

    private static string BuildLocked(string folder, string shellProperties, string source)
    {
        var root = Repo.Root;
        Directory.CreateDirectory(folder);
        Directory.SetLastWriteTimeUtc(folder, DateTime.UtcNow);
        WriteIfDifferent(Path.Combine(folder, "global.json"), File.ReadAllText(Path.Combine(root, "global.json")));
        var project = Path.Combine(folder, "Probe.csproj");
        WriteIfDifferent(project,
            Project(Path.Combine(root, "src", "Winshell.Shell", "Winshell.Shell.csproj"), shellProperties));
        WriteIfDifferent(Path.Combine(folder, "Program.cs"), source);
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

    private static string CopyLocked(string program, string folder)
    {
        var source = Path.GetDirectoryName(program)!;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(folder, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }

        return Path.Combine(folder, Path.GetFileName(program));
    }

    internal static string BuiltAssembly(string project)
    {
        lock (Gate)
        {
            var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(project)! };
            foreach (var argument in (string[])
                     ["build", project, "-c", Configuration, "--nologo", "--disable-build-servers", "-t:Build", "-getProperty:TargetPath"])
                info.ArgumentList.Add(argument);
            var built = ChildProcess.Run(info, TimeSpan.FromMinutes(10));
            built.AssertExitCode(0);
            return built.Output.Trim();
        }
    }

    private static string Configuration =>
        typeof(ProbeShell).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;

    private static void WriteIfDifferent(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return;
        File.WriteAllText(path, text);
    }


    private static string Project(string shellProject, string shellProperties)
    {
        var props = Path.Combine(Repo.Root, "src", "Winshell.Package", "buildTransitive", "Winshell.props");
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
                    {shellProperties}
                  </PropertyGroup>
                  <!-- The SDK adds transitive references too. Give all of them the tested configuration and keep
                       this probe's -o path out of their builds, including when reload builds the probe in Release. -->
                  <ItemDefinitionGroup>
                    <ProjectReference>
                      <SetConfiguration>Configuration={Configuration}</SetConfiguration>
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

    internal const string PartsMarker = "probe-parts";

    private const string Source =
        $$"""
          using System.Diagnostics;
          using Microsoft.UI.Xaml.Controls;
          using Windows.Foundation;

          Shell.Run(shell =>
          {
              shell.Ipc.Handle("probe", "note", (string text) => { });
              shell.Ipc.Property("probe", "name", () => "probe");
              shell.Ipc.Property("paths", "data", () => shell.Paths.Data);
              shell.Ipc.Property("paths", "state", () => shell.Paths.State);
              shell.Ipc.Property("paths", "cache", () => shell.Paths.Cache);
              shell.Ipc.Property("paths", "logs", () => shell.Paths.Logs);
              shell.Ipc.Property("paths", "program", () => shell.Paths.Program);
              shell.Ipc.Property("paths", "shells", () => shell.Paths.Shells);
              var config = Environment.GetEnvironmentVariable("WINSHELL_CONFIG_DIR");
              // A test that puts {{CrashMarker}} there gets a shell that crashes as it starts, once.
              if (config is not null && File.Exists(Path.Combine(config, "{{CrashMarker}}")))
              {
                  File.Delete(Path.Combine(config, "{{CrashMarker}}"));
                  throw new InvalidOperationException("The probe was asked to crash as it started.");
              }
              // A test that puts {{PreferencesMarker}} there gets one section during start and one posted for the
              // turn after. preferences-report.txt then says both, missing, or partial. Nothing is shown.
              if (config is not null && File.Exists(Path.Combine(config, "{{PreferencesMarker}}")))
              {
                  new EarlySection();
                  shell.Loop.Post(() => new LateSection());
                  shell.Loop.Post(() =>
                  {
                      var path = Path.Combine(shell.Paths.Shells, "preferences", shell.ShellId + ".toml");
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
                  var popup = shell.Add(new PopupWindow { Content = new ProbePopup { Children = { new TextBlock { Text = "Opened" } } } });
                  var open = new Button { Name = "Open", Content = "Open" };
                  var panel = shell.Add(new PanelWindow
                  {
                      Anchors = Edges.Top | Edges.Left,
                      Width = 160,
                      Height = 60,
                      Content = new ProbePage { Children = { open } }
                  });
                  open.Click += (_, _) =>
                  {
                      popup.Position = PopupPosition.At(new Point(panel.Origin.X, panel.Origin.Y + panel.ActualHeight));
                      popup.Width = 160;
                      popup.Height = 60;
                      popup.Visible = true;
                  };
              }
              var marker = config is null ? null : Path.Combine(config, "{{PartsMarker}}");
              var ending = Environment.GetEnvironmentVariable("{{PartsEndingVariable}}") ??
                           (marker is not null && File.Exists(marker) ? File.ReadAllText(marker).Trim() : null);
              if (config is not null && ending is not null)
              {
                  var report = Path.Combine(config, $"parts-{ending}.txt");
                  var first = shell.Add(new ProbePart("first", report));
                  shell.Add(new ProbePart("second", report));
                  shell.Add(first);
                  shell.Add(new ProbePart("disposed", report)).Dispose();
                  shell.Loop.Post(() =>
                  {
                      ProbePart.Write(report, ending);
                      if (ending != "reload")
                      {
                          shell.Quit();
                          return;
                      }

                      Environment.SetEnvironmentVariable("{{PartsEndingVariable}}", "quit");
                      ProbePart.Write(report, $"reloaded {shell.Reload()}");
                  });
                  return;
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
          });

          // What the turn after start can already see in the preferences file.
          static string PreferencesReport(string path)
          {
              if (!File.Exists(path))
                  return "missing";
              var text = File.ReadAllText(path);
              return text.Contains("[early]") && text.Contains("[late]") ? "both" : "partial";
          }

          public sealed class ProbePage : StackPanel;

          public sealed class ProbePopup : StackPanel;

          sealed class EarlySection() : Preferences("early");

          sealed class ProbePart(string name, string report) : IDisposable
          {
              public static void Write(string report, string line)
              {
                  File.AppendAllText(report, $"{Environment.ProcessId} {line}\n");
              }

              public void Dispose()
              {
                  var added = "added";
                  try
                  {
                      Shell.Current.Add(this);
                  }
                  catch (ObjectDisposedException)
                  {
                      added = "refused";
                  }

                  Write(report, $"dispose {name} {added}");
              }
          }

          sealed class LateSection() : Preferences("late");
          """;
}
