using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Winshell.TestSupport;

internal static class WinshellCli
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static string Exe { get; } =
        typeof(WinshellCli).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "WinshellCli")?.Value ??
        Path.Combine(AppContext.BaseDirectory, "winshell.exe");

    internal static ProcessStartInfo StartInfo(IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(Exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return info;
    }

    internal static ChildProcess Run(IEnumerable<string> args, params (string Name, string Value)[] environment)
    {
        Assert.True(File.Exists(Exe), $"No CLI at {Exe}");
        var info = StartInfo(args);
        foreach (var (name, value) in environment)
            info.Environment[name] = value;
        return ChildProcess.Run(info, Patience);
    }
}
