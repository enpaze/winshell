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
                winshell run [-c <name> | -p <path>] [-d] [-n] [--no-build] [-- <args>...]
                  Starts a configuration project's shell, built first when a source is newer than its
                  program. With no project, nothing starts.
                  -d, --daemonize        run in the background
                  -n, --no-duplicate     exit quietly if this config is already running
                      --no-build         run the last build, and do not build again
                  Words after -- go to the configuration's program.
                """,
        Options =
        [
            CliFlags.Path, CliFlags.Config, CliFlags.Daemonize, CliFlags.NoDuplicate, CliFlags.NoBuild
        ],
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    /// <summary>
    /// What a <c>-d</c> child runs. Empty unless <paramref name="parsed"/> daemonizes. The flag itself is gone, and a
    /// <c>-d</c> that is the value of <c>-c</c> or <c>-p</c> stays. <c>--no-build</c> is added when this process builds,
    /// so the child does not build again. A line with no command word is named <c>run</c>.
    /// </summary>
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

            // Only run's own value options take the next word. A shared flag is one token.
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
        var config = parsed.Text(CliFlags.Config);
        var project = ConfigurationProject.Find(path, config, ShellLocations.Current.ConfigRoot, out var error);
        if (error is not null)
        {
            // Before any instance log exists. An attached console prints it. Start and sign-in have no window.
            StartupError.Report(error, StartupError.FolderFor(path, ShellLocations.Current.ConfigRoot));
            return 1;
        }

        if (project is null)
            return 0;
        if (parsed.IsSet(CliFlags.NoDuplicate) &&
            project.InstancesOf(ConfigurationBuild.Run,
                ShellLocations.Current.Instances.List(false).Select(i => i.Info)).Any())
            return 0;
        var reason = project.Prepare(parsed.IsSet(CliFlags.NoBuild), ConfigurationProject.Run);
        if (reason is not null)
        {
            StartupError.Report(reason, project.Folder);
            return 1;
        }

        // -d leaves the program to a child that supervises it. The build is done, and its errors were in this console.
        // The child runs run's own argument list, so a -d that is the value of -c or -p is not dropped.
        if (parsed.IsSet(CliFlags.Daemonize))
            return Detach(ChildArguments(parsed));

        var program = new ProcessStartInfo(project.Program) { UseShellExecute = false };
        foreach (var a in CommandLine.ProgramArguments(parsed.IsSet(CliFlags.NoDuplicate), parsed.Verbose, parsed.Rest))
            program.ArgumentList.Add(a);
        return Supervise(program, project);
    }

    /// <summary>Starts <c>winshell.exe</c> again with <paramref name="childArgs"/>. The child supervises the shell.</summary>
    private static int Detach(IEnumerable<string> childArgs)
    {
        var exe = Environment.ProcessPath!; // The CLI runs from its apphost.
        if (InstanceStart.Detach(exe, childArgs) is not { } error)
            return 0;
        StartupError.Report(error, null);
        return 1;
    }

    /// <summary>
    /// Runs the shell <paramref name="shell"/> starts under a <see cref="ShellSupervisor"/>, and returns its last exit code.
    /// <paramref name="project"/> is the configuration project whose program that is.
    /// </summary>
    private static int Supervise(ProcessStartInfo shell, ConfigurationProject project)
    {
        IShellChild? Start()
        {
            // A reload can build beside a program that still runs, so each start asks for the last build. A dialog about
            // a start that failed offers its folder.
            shell.FileName = project.Program;
            return ShellProcess.Start(shell, Path.GetDirectoryName(shell.FileName));
        }

        var supervisor = new ShellSupervisor(Start, ExplorerTaskbarRecovery.AnyHidden,
            ExplorerTaskbarRecovery.RestoreHidden, TimeProvider.System);
        using var stop = new CancellationTokenSource();
        Task<int>? supervision = null;

        // Ctrl+C and Ctrl+Break stop the shell and supervision. Closing the console and the end of the session end this
        // process once the handler returns, so it waits for the shell to stop and the taskbar to come back. The session's
        // end is a window message: Windows sends no console signal for it to a process that has loaded user32. SystemEvents
        // makes its window on the thread that first subscribes when that thread is STA, and this one pumps no messages, so a
        // pool thread subscribes and SystemEvents makes a thread of its own.
        void Stop()
        {
            // The handler has to cancel this source, which is disposed only as this method leaves.
            stop.Cancel();
            try
            {
#pragma warning disable VSTHRD002 // A handler must finish before Windows ends this process. Supervision runs on the pool.
                // Registered before the task exists, so this wait has to see the assignment below.
                supervision?.Wait(TimeSpan.FromSeconds(4));
            }
            catch (AggregateException)
            {
            }
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

        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Signal);
        using var breaks = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Signal);
        using var close = PosixSignalRegistration.Create(PosixSignal.SIGHUP, Signal);
        // A cancelled token would skip the session subscription and throw, which this startup never did.
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
}
