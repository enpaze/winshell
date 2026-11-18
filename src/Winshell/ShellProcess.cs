using System.ComponentModel;
using System.Diagnostics;

namespace Winshell;

internal sealed class ShellProcess : IShellChild
{
    private readonly Process _process;
    private readonly ShellExit.Supervision _supervision;

    private ShellProcess(Process process)
    {
        _process = process;
        _supervision = new ShellExit.Supervision(process.Id);
        Exited = ExitAsync();
    }

    public Task<int> Exited { get; }

    public int Pid => _process.Id;

    public ExitIntent Intent => _supervision.Intent;

    public static ShellProcess? Start(ProcessStartInfo info, string? folder)
    {
        try
        {
            return new ShellProcess(Process.Start(info)!);
        }
        catch (Win32Exception e)
        {
            StartupError.Report($"Could not start \"{info.FileName}\": {e.Message}", folder);
            return null;
        }
    }

    public async Task StopAsync()
    {
        var info = ShellLocations.Current.Instances.List(false).Select(row => row.Info)
            .FirstOrDefault(candidate => candidate.Pid == _process.Id);
        _ = await ShellExit.QuitAsync(_process, info, ExitIntent.None, ShellExit.Patience.Closing,
            ShellExit.Ending.Always);
    }

    public void Dispose()
    {
        _supervision.Dispose();
        _process.Dispose();
    }

    private async Task<int> ExitAsync()
    {
        await _process.WaitForExitAsync();
        return _process.ExitCode;
    }
}
