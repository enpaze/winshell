using System.Diagnostics;
using System.Text;

namespace Winshell.TestSupport;

internal sealed record ChildProcess(int Code, string Output, string Error)
{
    private string Command { get; init; } = "The process";

    internal static ChildProcess Run(ProcessStartInfo info, TimeSpan patience)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        var what = string.Join(' ', [Path.GetFileName(info.FileName), .. info.ArgumentList]);
        using var process = Process.Start(info)!;
        var output = "";
        var error = "";
        var stdout = process.StandardOutput;
        var stderr = process.StandardError;
        var readOutput = new Thread(() => output = stdout.ReadToEnd()) { IsBackground = true };
        var readError = new Thread(() => error = stderr.ReadToEnd()) { IsBackground = true };
        readOutput.Start();
        readError.Start();
        if (!process.WaitForExit(patience))
        {
            process.Kill(true);
            process.WaitForExit();
            readOutput.Join(patience);
            readError.Join(patience);
            Assert.Fail($"{what} did not exit.\nOutput:\n{output}\nError:\n{error}");
        }

        Assert.IsTrue(readOutput.Join(patience) && readError.Join(patience), $"{what} left its output open");
        return new ChildProcess(process.ExitCode, output, error) { Command = what };
    }

    internal static ChildProcess Run(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file);
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return Run(info, TimeSpan.FromMinutes(1));
    }

    internal static Process Start(ProcessStartInfo info, StringBuilder? printed)
    {
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        var process = Process.Start(info)!;

        void Print(object sender, DataReceivedEventArgs e)
        {
            if (printed is not null && e.Data is not null)
                lock (printed)
                {
                    printed.AppendLine(e.Data);
                }
        }

        process.ErrorDataReceived += Print;
        process.OutputDataReceived += Print;
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        return process;
    }

    internal static ChildProcess Exited(string command, Process process, StringBuilder printed)
    {
        lock (printed)
        {
            return new ChildProcess(process.ExitCode, printed.ToString(), "") { Command = command };
        }
    }

    internal void AssertNoError()
    {
        if (Error.Length > 0)
            Assert.Fail($"{Command} wrote to its standard error.\nError:\n{Error}\nOutput:\n{Output}");
    }

    internal void AssertExitCode(int expected)
    {
        if (Code != expected)
            Assert.Fail($"{Command} exited with {Code}, not {expected}.\nOutput:\n{Output}\nError:\n{Error}");
    }
}
