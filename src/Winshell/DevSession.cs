using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;

namespace Winshell;

/// <summary>
/// <c>winshell dev</c>: runs a configuration under <c>dotnet watch</c>. Reactor's hot reload applies an edit to a
/// component's <c>Render</c> in the running shell, and a compile error shows on that shell's screen. It stays until
/// Ctrl+C, so a failed build costs nothing.
/// </summary>
internal static class DevSession
{
    /// <summary>ERROR_FILE_NOT_FOUND. <c>dotnet</c> is not on PATH.</summary>
    private const int MissingFile = 2;

    /// <summary>
    /// Asks this project's shell to quit, as <c>winshell kill</c> does, so its app bar and tray go and its instance is
    /// marked exited. Then ends dotnet watch and what it started.
    /// </summary>
    /// <param name="project">The configuration whose shell is asked to quit.</param>
    /// <param name="watch">The <c>dotnet watch</c> process, and what it started.</param>
    /// <param name="patience">How long the handler may wait for the shell.</param>
    private static void StopShellAndWatch(ConfigurationProject project, Process watch, ShellExit.Patience patience)
    {
        if (ShellOf(project) is { } shell)
            ConsoleEnding.Quit(shell, patience);
        ConsoleEnding.Kill(watch);
    }

    /// <summary>How <c>dotnet watch</c> ended.</summary>
    public static async Task<int> Run(ConfigurationProject project)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = project.Folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // The build must not leave a reusable MSBuild node. Those nodes are not children of dotnet watch, and
        // they end with it, so stopping winshell dev fails another build that borrowed one (MSB4166).
        // --disable-build-servers forwards /nodeReuse:false to that build. It is not an environment variable,
        // so the shell and the apps it starts do not inherit it.
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

        // dotnet watch has a console of its own, which Ctrl+C, Ctrl+Break and closing this one never reach. Each of
        // those ends this process once its handler returns, so the handler stops the shell and dotnet watch first.
        // The handler has to stop this process, which is disposed only as this method leaves. It does not cancel the
        // signal: Windows ends this process when the handler returns.
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

    /// <summary>
    /// Starts <c>dotnet</c>, or returns null and the sentence to print. A missing <c>dotnet</c> is
    /// <see cref="ConfigurationProject.NeedsSdk"/>. Any other failure is the <see cref="Win32Exception"/> itself.
    /// </summary>
    private static Process? Start(ProcessStartInfo start, out string failure)
    {
        failure = ConfigurationProject.NeedsSdk;
        try
        {
            return Process.Start(start);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == MissingFile)
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

    /// <summary>The running shell dotnet watch started for this project, or null before its first start.</summary>
    private static InstanceInfo? ShellOf(ConfigurationProject project)
    {
        return project
            .InstancesOf(ConfigurationBuild.Dev, ShellLocations.Current.Instances.List(false).Select(i => i.Info))
            .FirstOrDefault();
    }

    /// <summary>Tells the shell of this project. Before its first start there is none, and the console has the error.</summary>
    private static async Task Show(ConfigurationProject project, string notice)
    {
        if (ShellOf(project) is not { } shell)
            return;
        await BuildNotice.Show(shell, notice, project.ProjectFile);
    }
}
