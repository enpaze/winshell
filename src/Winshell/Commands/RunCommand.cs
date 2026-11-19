using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Winshell.ExplorerTaskbar;

namespace Winshell.Commands;

internal static class RunCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = CommandWords.Run,
        Overview = "Usage: winshell run [options]              start a shell",
        Usage = """
                winshell run [-s <name> | -p <path>] [-d] [-n] [--no-build] [-- <args>...]
                  Starts a shell project's program, built first when a source is newer than its
                  program. With no project, it says how to make one and exits 1; a start with no
                  console window exits 0 and says nothing. A shell already running is asked to quit
                  first, and one that does not quit stays: the start says why and exits 1.
                  -d, --daemonize        run in the background
                  -n, --no-duplicate     exit quietly if this shell is already running
                      --no-build         run the last build, and do not build again
                  Words after -- go to the shell's program.
                """,
        Options =
        [
            CliFlags.Path, CliFlags.Shell, CliFlags.Daemonize, CliFlags.NoDuplicate, CliFlags.NoBuild
        ],
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    internal static ImmutableArray<string> ChildArguments(Invocation parsed)
    {
        if (!parsed.IsSet(CliFlags.Daemonize))
            return [];

        var kept = new List<string>();
        var args = parsed.Arguments;
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (token == CommandWords.End)
            {
                kept.AddRange(args.Skip(i));
                break;
            }

            var option = Entry.Options.FirstOrDefault(candidate => candidate.Word.Is(token));
            if (option == CliFlags.Daemonize)
                continue;
            kept.Add(token);
            if (option is { TakesValue: true } && i + 1 < args.Length)
                kept.Add(args[++i]);
        }

        if (parsed.Command is null)
            kept.Insert(0, CommandWords.Run);
        if (!parsed.IsSet(CliFlags.NoBuild))
            kept.Insert(0, CommandWords.NoBuild.Long);
        return [.. kept];
    }

    internal static int Execute(Invocation parsed)
    {
        var path = parsed.Text(CliFlags.Path);
        var shell = parsed.Text(CliFlags.Shell);
        var project = ShellProject.Find(path, shell, ShellLocations.Current.ShellsRoot, out var error);
        if (error is not null)
        {
            StartupError.Report(error, StartupError.FolderFor(path, ShellLocations.Current.ShellsRoot));
            return 1;
        }

        if (project is null)
            return NoProject(ShellLocations.Current.ShellsRoot, StartupError.Unread(), Console.Error);
        if (parsed.IsSet(CliFlags.NoDuplicate) &&
            project.InstancesOf(ShellProjectBuild.Run,
                ShellLocations.Current.Instances.Running()).Any())
            return 0;
        var reason = project.Prepare(parsed.IsSet(CliFlags.NoBuild), ShellProject.Run);
        if (reason is not null)
        {
            StartupError.Report(reason, project.Folder);
            return 1;
        }

        if (parsed.IsSet(CliFlags.Daemonize))
            return Detach(ChildArguments(parsed));

        var program = new ProcessStartInfo(project.Program) { UseShellExecute = false };
        foreach (var a in CommandLine.ProgramArguments(parsed.IsSet(CliFlags.NoDuplicate), parsed.Verbose, parsed.Rest))
            program.ArgumentList.Add(a);
        return Supervise(program, project);
    }

    internal static int NoProject(string shellsRoot, bool unread, TextWriter error)
    {
        if (unread)
            return 0;
        error.WriteLine($"No shell project in {shellsRoot}, so nothing started. Run winshell new to make one.");
        return 1;
    }

    private static int Detach(IEnumerable<string> childArgs)
    {
        var exe = Environment.ProcessPath!;
        if (InstanceStart.Detach(exe, childArgs) is not { } error)
            return 0;
        StartupError.Report(error, null);
        return 1;
    }

    private static int Supervise(ProcessStartInfo shell, ShellProject project)
    {
        IShellChild? Start()
        {
            shell.FileName = project.Program;
            return ShellProcess.Start(shell, Path.GetDirectoryName(shell.FileName));
        }

        var supervisor = new ShellSupervisor(Start, ExplorerTaskbarWindow.AnyHidden,
            (childPid, hiddenAtStart) => ExplorerTaskbarLease.LeaveHidden(ShellLocations.Current, childPid, hiddenAtStart),
            ExplorerTaskbarLease.Recover, TimeProvider.System);
        using var stop = new CancellationTokenSource();
        Task<int>? supervision = null;

        void Stop()
        {
            stop.Cancel();
            Wait(supervision);
        }

        void Signal(PosixSignalContext context)
        {
            context.Cancel = true;
            Stop();
        }

        void SessionEnded(object? sender, SessionEndedEventArgs e)
        {
            Stop();
        }

        using var ending = ConsoleEnding.Listen(Signal);
#pragma warning disable VSTHRD002
        Task.Run(() => SystemEvents.SessionEnded += SessionEnded).GetAwaiter().GetResult();
        try
        {
            supervision = supervisor.RunAsync(stop.Token);
            return supervision.GetAwaiter().GetResult();
        }
        finally
        {
            SystemEvents.SessionEnded -= SessionEnded;
        }
#pragma warning restore VSTHRD002
    }

    private static void Wait(Task? task)
    {
        try
        {
#pragma warning disable VSTHRD002
            task?.Wait(TimeSpan.FromSeconds(4));
#pragma warning restore VSTHRD002
        }
        catch (AggregateException)
        {
        }
    }
}
