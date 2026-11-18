using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;

namespace Winshell;

internal static class DevSession
{
    private const int ErrorFileNotFound = 2;

    private static void StopShellAndWatch(ShellProject project, Process watch, ShellExit.Patience patience)
    {
        if (ShellOf(project) is { } shell)
            ConsoleEnding.Quit(shell, patience);
        ConsoleEnding.Kill(watch);
    }

    public static async Task<int> Run(ShellProject project)
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
            StopShellAndWatch(project, process, ConsoleEnding.PatienceFor(context.Signal)));
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
        return project
            .InstancesOf(ShellProjectBuild.Dev, ShellLocations.Current.Instances.List(false).Select(i => i.Info))
            .FirstOrDefault();
    }

    private static async Task Show(ShellProject project, string notice)
    {
        if (ShellOf(project) is not { } shell)
            return;
        await BuildNotice.Show(shell, notice, project.ProjectFile);
    }
}
