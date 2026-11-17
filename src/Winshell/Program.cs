namespace Winshell;

// Starts the CLI.
internal static class Program
{
    [STAThread]
#pragma warning disable VSTHRD002
    private static int Main(string[] args) => Cli.RunAsync(args).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
}
