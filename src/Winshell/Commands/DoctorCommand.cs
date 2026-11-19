using Winshell.Diagnostics;

namespace Winshell.Commands;

internal static class DoctorCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "doctor",
        Overview = """
                          winshell doctor [shell options] [-j] [--no-color]
                                                              check this PC's setup for Winshell
                   """,
        Usage = """
                winshell doctor [-s <name> | -p <path>] [-j] [--no-color]
                  Checks this PC's setup for Winshell and changes nothing.
                  -s, --shell <name>     a named shell in {shells-root}
                  -j, --json             print the checks as JSON
                      --no-color         print without color
                """,
        Options = [CliFlags.Path, CliFlags.Shell, CliFlags.Json, CliFlags.NoColor],
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    private static int Execute(Invocation parsed)
    {
        var checks = Doctor.Run(new DoctorContext(ShellLocations.Current, parsed.Text(CliFlags.Path),
            parsed.Text(CliFlags.Shell)));
        if (parsed.IsSet(CliFlags.Json))
        {
            Console.WriteLine(DoctorReport.ToJson(checks, Shell.Version));
        }
        else
        {
            var color = !parsed.IsSet(CliFlags.NoColor) && !Console.IsOutputRedirected &&
                        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            Console.Write(DoctorReport.Format(checks, Shell.Version, color));
        }

        return checks.Any(c => c.Status == CheckStatus.Fail) ? 1 : 0;
    }
}
