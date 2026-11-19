using System.Runtime.InteropServices;
using System.Text;

namespace Winshell.Tests;

public sealed class WidgetStartTests(WidgetStartTests.ClashingProbe probe) : IClassFixture<WidgetStartTests.ClashingProbe>
{
    [Fact]
    public void Two_widgets_with_one_name_stop_the_shell_before_start_with_a_crash_report_naming_both()
    {
        using var folder = new TempFolder();
        var config = Path.Combine(folder.Path, "config");
        var local = Path.Combine(folder.Path, "local");
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(local);
        var printed = new StringBuilder();

        using var process = ProbeShell.Launch(config, local, [], printed: printed, program: probe.Program);
        Wait.Until(() => process.HasExited, "the probe to stop");

        ChildProcess.Exited("The probe", process, printed).AssertExitCode(1);
        var report = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(local, "crashes"))));
        Assert.Contains("Winshell closed: yes", report, StringComparison.Ordinal);
        Assert.Contains("Two widgets are named clock: Clashing.Clock and Clashing.ClockWidget.", report,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(config, "started")), "start ran.");
    }

    [Fact]
    public void Discovery_reads_the_program_s_own_assemblies_and_none_of_the_shared_framework()
    {
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        Assert.False(string.Equals(Path.TrimEndingDirectorySeparator(runtime),
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase));

        var paths = WidgetRegistry.ProgramAssemblyPaths().ToList();

        Assert.Contains(typeof(Shell).Assembly.Location, paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(typeof(WidgetStartTests).Assembly.Location, paths, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(paths, path => path.StartsWith(runtime, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class ClashingProbe : IDisposable
    {
        private const string Source =
            """
            Shell.Run(shell => File.WriteAllText(
                Path.Combine(Environment.GetEnvironmentVariable("WINSHELL_CONFIG_DIR")!, "started"), ""));

            namespace Clashing
            {
                public sealed class Clock : Widget
                {
                    public override Element Render() => TextBlock("clock");
                }

                public sealed class ClockWidget : Widget
                {
                    public override Element Render() => TextBlock("clock widget");
                }
            }
            """;

        private readonly string _dir = TempFolder.NewPath();

        public ClashingProbe()
        {
            _ = ProbeShell.Program;
            Program = ProbeShell.Build(_dir, Source);
        }

        public string Program { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
