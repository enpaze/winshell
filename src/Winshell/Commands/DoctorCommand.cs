using Winshell.Diagnostics;

namespace Winshell.Commands;

internal static class DoctorCommand
{
    internal static readonly CommandEntry Entry = new()
    {
        Name = "doctor",
        Overview = """
                          winshell doctor [config options] [-j] [--no-color]
                                                              check this PC's setup for Winshell
                   """,
        Usage = """
                winshell doctor [-c <name> | -p <path>] [-j] [--no-color]
                  Checks this PC's setup for Winshell and changes nothing.
                  -c, --config <name>    a named config in {config-root}
                  -j, --json             print the checks as JSON
                      --no-color         print without color
                """,
        Options = [CliFlags.Path, CliFlags.Config, CliFlags.Json, CliFlags.NoColor],
        Run = static parsed => Task.FromResult(Execute(parsed))
    };

    /// <summary>winshell doctor: checks this PC's setup, prints a line each, and fails when a check fails. It never builds.</summary>
    internal static int Execute(Invocation parsed)
    {
        var checks = Doctor.Run(new DoctorContext(ShellLocations.Current, parsed.Text(CliFlags.Path),
            parsed.Text(CliFlags.Config)));
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
