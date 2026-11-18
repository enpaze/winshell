using System.Diagnostics;

namespace Winshell.Services.Tests;

public sealed class ShellStartTests
{
    private static readonly ShellLocations Locations = new(@"C:\config", @"C:\local");

    [Fact]
    public void After_and_no_duplicate_are_read_and_the_rest_stays_the_programs()
    {
        var arguments = CommandLine.ReadProgram(["--theme", "dark", "--after", "4242", "-n"]);

        Assert.Equal(4242, arguments.After);
        Assert.True(arguments.NoDuplicate);
        Assert.Equal(["--theme", "dark", "-n"], arguments.Kept);
    }

    [Theory]
    [InlineData("--after")]
    [InlineData("--after soon")]
    [InlineData("-- --after 12 -n")]
    public void Without_a_process_id_or_after_a_double_dash_nothing_is_taken(string line)
    {
        var given = line.Split(' ');

        var arguments = CommandLine.ReadProgram(given);

        Assert.Null(arguments.After);
        Assert.False(arguments.NoDuplicate);
        Assert.Equal(given, arguments.Kept);
    }

    [Fact]
    public void The_next_process_keeps_the_arguments_and_names_only_this_one()
    {
        var arguments = CommandLine.ReadProgram(["--after", "1", "--no-duplicate", "--", "a"]);

        Assert.Equal(["--after", "2", "--no-duplicate", "--", "a"], arguments.ForNext(2));
        Assert.Equal(2, CommandLine.ReadProgram(arguments.ForNext(2)).After);
        Assert.False(CommandLine.ReadProgram(arguments.ForNext(2)).Daemonize);
    }

    [Fact]
    public void A_detached_start_drops_d_and_keeps_the_handoff()
    {
        var arguments = CommandLine.ReadProgram(["-d", "--after", "7", "--theme", "dark", "--daemonize", "--", "-d"]);

        Assert.True(arguments.Daemonize);
        Assert.Equal(7, arguments.After);
        Assert.Equal(["--theme", "dark", "--", "-d"], arguments.Kept);
        Assert.Equal(["--after", "7", "--theme", "dark", "--", "-d"], arguments.Detached);
        Assert.Equal(["--after", "8", "--theme", "dark", "--", "-d"], arguments.ForNext(8));
        Assert.False(CommandLine.ReadProgram(arguments.ForNext(8)).Daemonize);

        var start = InstanceStart.DetachedStart(@"C:\shells\winshell-next.exe", arguments.Detached);
        Assert.Equal(@"C:\shells\winshell-next.exe", start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Normal, start.WindowStyle);
        Assert.Equal(["--after", "7", "--theme", "dark", "--", "-d"], start.ArgumentList);
    }

    [Fact]
    public void A_given_shell_id_names_the_folders()
    {
        var identity = ShellIdentity.Resolve(@"C:\shells\mine\mine.exe", Locations, new ShellProperties("mine-id"));

        Assert.Equal("mine-id", identity.ShellId);
        Assert.Equal(@"C:\local\data\by-shell\mine-id", identity.Data);
        Assert.Equal(@"C:\local\state\by-shell\mine-id", identity.State);
        Assert.Equal(@"C:\local\cache\by-shell\mine-id", identity.Cache);
    }

    [Theory]
    [InlineData(@"C:\config\bin\shell\Mine.exe", "root")]
    [InlineData(@"C:\config\Bar\bin\shell\Bar.exe", "bar")]
    [InlineData(@"C:\config\Bar\bin\shell.next\Bar.exe", "bar")]
    public void A_configuration_in_the_config_folder_is_named_after_its_folder(string program, string id)
    {
        Assert.Equal(id, IdOf(program));
    }

    [Fact]
    public void A_configuration_anywhere_else_is_named_after_its_path_in_any_case_from_either_build_folder()
    {
        var one = IdOf(@"C:\shells\mine\bin\shell\Mine.exe");

        Assert.Equal("cea0f30d0e45e5a6bf33f1d21c3e6351", one);
        Assert.Equal(one, IdOf(@"C:\shells\mine\bin\shell.next\Mine.exe"));
        Assert.Equal(one, IdOf(@"C:\SHELLS\MINE\BIN\SHELL\MINE.EXE"));
        Assert.NotEqual(one, IdOf(@"C:\shells\yours\bin\shell\Yours.exe"));
    }

    [Fact]
    public void Folders_named_after_the_earlier_hash_move_to_the_configurations_name_once()
    {
        using var temp = new TempFolder();
        var locations = new ShellLocations(Path.Combine(temp.Path, "config"), Path.Combine(temp.Path, "local"));
        var program = Path.Combine(locations.ConfigRoot, "bar", "bin", "shell", "Bar.exe");
        var earlier = ShellIdentity.Resolve(program, locations with { ConfigRoot = "" }, new ShellProperties());
        Directory.CreateDirectory(earlier.Data);
        File.WriteAllText(Path.Combine(earlier.Data, "kept.txt"), "data");
        Directory.CreateDirectory(earlier.State);
        var messages = new List<string>();
        var log = new Log { Sink = (_, category, message) => messages.Add($"{category}: {message}") };

        var identity = ShellIdentity.Resolve(program, locations, new ShellProperties());
        identity.CarryOver(log);

        Assert.Equal("data", File.ReadAllText(Path.Combine(identity.Data, "kept.txt")));
        Assert.True(Directory.Exists(identity.State));
        Assert.False(Directory.Exists(identity.Cache));
        Assert.False(Directory.Exists(earlier.Data));
        Assert.Equal(
        [
            $"shell: Moved \"{earlier.Data}\" to \"{identity.Data}\"",
            $"shell: Moved \"{earlier.State}\" to \"{identity.State}\""
        ], messages);

        Directory.CreateDirectory(earlier.Data);
        identity.CarryOver(log);

        Assert.True(Directory.Exists(earlier.Data));
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public void A_given_shell_id_moves_nothing_and_a_given_folder_is_not_moved_into()
    {
        using var temp = new TempFolder();
        var locations = new ShellLocations(Path.Combine(temp.Path, "config"), Path.Combine(temp.Path, "local"));
        var program = Path.Combine(locations.ConfigRoot, "bar", "bin", "shell", "Bar.exe");
        var earlier = ShellIdentity.Resolve(program, locations with { ConfigRoot = "" }, new ShellProperties());
        Directory.CreateDirectory(earlier.Data);
        Directory.CreateDirectory(earlier.State);
        var given = Path.Combine(temp.Path, "state");

        ShellIdentity.Resolve(program, locations, new ShellProperties("bar")).CarryOver(new Log());
        Assert.True(Directory.Exists(earlier.Data));

        ShellIdentity.Resolve(program, locations, new ShellProperties(ShellStateDir: given)).CarryOver(new Log());
        Assert.True(Directory.Exists(earlier.State));
        Assert.False(Directory.Exists(given));
    }

    [Fact]
    public void A_given_folder_is_next_to_the_program_and_can_name_the_base()
    {
        var identity = ShellIdentity.Resolve(@"C:\shells\mine.exe", Locations with { ConfigRoot = "" },
            new ShellProperties("id", "data", @"$BASE\other", @"D:\cache"));

        Assert.Equal(@"C:\shells\data", identity.Data);
        Assert.Equal(@"C:\local\state\other", identity.State);
        Assert.Equal(@"D:\cache", identity.Cache);
    }

    [Fact]
    public void A_relative_folder_is_in_the_project_or_next_to_a_program_outside_its_build()
    {
        var given = new ShellProperties("id", "data", "state", "cache");
        var built = ShellIdentity.Resolve(@"C:\shells\mine\bin\shell\Mine.exe", Locations, given);

        Assert.Equal(@"C:\shells\mine\data", built.Data);
        Assert.Equal(@"C:\shells\mine\state", built.State);
        Assert.Equal(@"C:\shells\mine\cache", built.Cache);

        var reloaded = ShellIdentity.Resolve(@"C:\shells\mine\bin\shell.next\Mine.exe", Locations, given);

        Assert.Equal(@"C:\shells\mine\data", reloaded.Data);
        Assert.Equal(@"C:\shells\mine\state", reloaded.State);
        Assert.Equal(@"C:\shells\mine\cache", reloaded.Cache);

        var loose = ShellIdentity.Resolve(@"C:\shells\mine\bin\Debug\Mine.exe", Locations, given);

        Assert.Equal(@"C:\shells\mine\bin\Debug\data", loose.Data);
    }

    [Fact]
    public void A_given_folder_expands_environment_variables()
    {
        var identity = ShellIdentity.Resolve(@"C:\shells\mine\bin\shell\Mine.exe", Locations,
            new ShellProperties("id", @"%SystemRoot%\data", @"%SystemRoot%", "%WINSHELL_UNSET_VARIABLE%"));

        var windows = Environment.GetEnvironmentVariable("SystemRoot")!;
        Assert.Equal(Path.Combine(windows, "data"), identity.Data);
        Assert.Equal(windows, identity.State);
        Assert.Equal(@"C:\shells\mine\%WINSHELL_UNSET_VARIABLE%", identity.Cache);
    }

    [Fact]
    public void A_shell_id_names_its_mutex_in_any_case()
    {
        Assert.Equal(HeldMutex.NoDuplicateName(Locations, "Mine"), HeldMutex.NoDuplicateName(Locations, "mine"));
    }

    private static string IdOf(string program)
    {
        return ShellIdentity.Resolve(program, Locations, new ShellProperties()).ShellId;
    }
}
