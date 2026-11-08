using System.ComponentModel;
using System.Diagnostics;

namespace Winshell.ShellTests;

/// <summary>
/// xUnit learns an assembly by running it with <c>-assemblyInfo</c> and waiting 60 seconds for that process to exit.
/// The probe only prints assembly info, so it leaves the self-contained Windows App Runtime unloaded. A test run
/// still loads that runtime.
/// </summary>
public sealed class DiscoveryProbeTests
{
    [Fact]
    public void The_discovery_probe_answers_without_loading_the_windows_app_runtime()
    {
        var exe = Environment.ProcessPath;
        Assert.NotNull(exe);
        using var probe = Process.Start(new ProcessStartInfo(exe, "-assemblyInfo")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe)
        });
        Assert.NotNull(probe);

        var sawRuntime = false;
        while (!probe.HasExited)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            sawRuntime |= LoadedWindowsAppRuntime(probe);
        }

        Assert.True(probe.WaitForExit(TimeSpan.FromMinutes(1)));
        var output = probe.StandardOutput.ReadToEnd();
        Assert.Equal(0, probe.ExitCode);
        Assert.Contains("\"core-framework\":", output, StringComparison.Ordinal);
        Assert.False(sawRuntime);
    }

    [Fact]
    public void A_test_run_loads_the_windows_app_runtime()
    {
        Assert.Contains(
            Process.GetCurrentProcess().Modules.Cast<ProcessModule>(),
            module => module.ModuleName.Equals("Microsoft.WindowsAppRuntime.dll", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LoadedWindowsAppRuntime(Process probe)
    {
        try
        {
            probe.Refresh();
            foreach (ProcessModule module in probe.Modules)
                if (module.ModuleName.Equals("Microsoft.WindowsAppRuntime.dll", StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        catch (InvalidOperationException)
        {
            // The probe exited while its module list was being read.
        }
        catch (Win32Exception)
        {
            // The probe exited while its module list was being read.
        }

        return false;
    }
}
