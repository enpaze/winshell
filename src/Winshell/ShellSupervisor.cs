namespace Winshell;

internal delegate bool LeaveTaskbar(int childPid, bool hiddenAtStart);

internal sealed class ShellSupervisor(
    Func<IShellChild?> start,
    Func<bool> taskbarHidden,
    LeaveTaskbar leaveTaskbar,
    Action restoreTaskbar,
    TimeProvider time)
{
    private const int CrashesBeforeGivingUp = 6;

    private static readonly TimeSpan FirstWait = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan RunningWell = TimeSpan.FromMinutes(1);

    public async Task<int> RunAsync(CancellationToken stop)
    {
        var crashes = 0;
        while (true)
        {
            var hidden = taskbarHidden();
            var started = time.GetTimestamp();
            var exit = (Code: InstanceStart.CouldNotStart, Intent: ExitIntent.None, Pid: 0);
            try
            {
                exit = await RunOnceAsync(stop);
            }
            finally
            {
                if (exit.Code != 0 && !leaveTaskbar(exit.Pid, hidden))
                    restoreTaskbar();
            }

            var code = exit.Code;
            if (exit.Intent == ExitIntent.Stop || stop.IsCancellationRequested)
                return code;
            if (exit.Intent == ExitIntent.Reload)
            {
                crashes = 0;
                continue;
            }

            if (code is 0 or InstanceStart.CouldNotStart)
                return code;
            crashes = time.GetElapsedTime(started) >= RunningWell ? 1 : crashes + 1;
            if (crashes == CrashesBeforeGivingUp)
            {
                await Console.Error.WriteLineAsync(
                    $"The shell crashed {crashes} times in a row, the last with exit code {code}. It is not started again.".AsMemory(),
                    CancellationToken.None);
                return code;
            }

            var wait = TimeSpan.FromTicks(Math.Min(FirstWait.Ticks << (crashes - 1), LongestWait.Ticks));
            await Console.Error.WriteLineAsync(
                $"The shell exited with code {code}. Starting it again in {wait.TotalSeconds:0} s.".AsMemory(),
                CancellationToken.None);
            try
            {
                await Task.Delay(wait, time, stop);
            }
            catch (OperationCanceledException)
            {
                return code;
            }
        }
    }

    private async Task<(int Code, ExitIntent Intent, int Pid)> RunOnceAsync(CancellationToken stop)
    {
        using var child = start();
        if (child is null)
            return (InstanceStart.CouldNotStart, ExitIntent.None, 0);
        int code;
        try
        {
            code = await child.Exited.WaitAsync(stop);
        }
        catch (OperationCanceledException)
        {
            await child.StopAsync();
            code = await child.Exited;
        }

        return (code, child.Intent, child.Pid);
    }
}
