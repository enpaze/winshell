using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Winshell.Commands;

internal static class DevCommand
{
    private const int ErrorFileNotFound = 2;

    internal static readonly CommandEntry Entry = new()
    {
        Name = "dev",
        Overview = """
                          winshell dev [-c <name> | -p <path>]
                                                              run a project and apply each edit to it
                   """,
        Usage = """
                winshell dev [-c <name> | -p <path>]
                  Runs a configuration project and applies each edit to the running shell, until Ctrl+C.
                  Reactor's hot reload applies a saved C# file in place, and the other windows are left alone.
                  What does not compile shows on screen, and the last version that did keeps running.
                  Needs the .NET SDK. See the guide for what an edit can change.
                """,
        Options = [CliFlags.Path, CliFlags.Config],
        Run = Execute
    };

    private static async Task<int> Execute(Invocation parsed)
    {
        var project = ShellProject.Find(parsed.Text(CliFlags.Path), parsed.Text(CliFlags.Config),
            ShellLocations.Current.ConfigRoot, out var error);
        if (error is not null)
        {
            await Console.Error.WriteLineAsync(error);
            return 1;
        }

        if (project is null)
        {
            await Console.Error.WriteLineAsync(
                "winshell dev needs a configuration project. Run winshell new to make one.");
            return 1;
        }

        return await WatchAsync(project);
    }

    private static async Task<int> WatchAsync(ShellProject project)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = project.Folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 {
                     "watch", "--non-interactive", "--disable-build-servers", "--project", project.ProjectFile, "run",
                     "--no-launch-profile"
                 })
            start.ArgumentList.Add(argument);
        DevOutput.Prepare(start.Environment);
        start.Environment[Shell.DevProjectVariable] = project.Folder;
        using var process = Start(start, out var failure);
        if (process is null)
        {
            await Console.Error.WriteLineAsync(failure);
            return 1;
        }

        using var ending = ConsoleEnding.Listen(context =>
            StopShellAndWatch(project, process, PatienceFor(context.Signal)));
        var lines = Channel.CreateUnbounded<string>();
        var pumps = Task.WhenAll(Pump(process.StandardOutput, lines.Writer), Pump(process.StandardError, lines.Writer));
        _ = pumps.ContinueWith(_ => lines.Writer.Complete(), TaskScheduler.Default);
        var output = new DevOutput(project.Folder);
        await foreach (var line in lines.Reader.ReadAllAsync())
        {
            if (DevOutput.Shows(line))
                Console.WriteLine(line);
            if (output.Read(line) is { } notice)
                await Show(project, notice);
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static QuitPatience PatienceFor(PosixSignal signal) =>
        signal == PosixSignal.SIGHUP ? QuitPatience.Closing : QuitPatience.Interactive;

    private static void StopShellAndWatch(ShellProject project, Process watch, QuitPatience patience)
    {
        if (ShellOf(project) is { } shell)
        {
#pragma warning disable VSTHRD002
            _ = ShellExit.QuitAsync(shell, ExitIntent.None, patience, ShellExit.Ending.Always).GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002
        }

        try
        {
            watch.Kill(true);
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception or AggregateException)
        {
        }
    }

    private static Process? Start(ProcessStartInfo start, out string failure)
    {
        failure = ShellProject.NeedsSdk;
        try
        {
            return Process.Start(start);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorFileNotFound)
        {
            return null;
        }
        catch (Win32Exception e)
        {
            failure = e.Message;
            return null;
        }
    }

    private static async Task Pump(StreamReader stream, ChannelWriter<string> lines)
    {
        while (await stream.ReadLineAsync() is { } line)
            await lines.WriteAsync(line);
    }

    private static InstanceInfo? ShellOf(ShellProject project)
    {
        return project.InstancesOf(ShellProjectBuild.Dev, ShellLocations.Current.Instances.Running())
            .FirstOrDefault();
    }

    private static async Task Show(ShellProject project, string notice)
    {
        if (ShellOf(project) is not { } shell)
            return;
        await BuildNotice.Show(shell, notice, project.ProjectFile);
    }
}
