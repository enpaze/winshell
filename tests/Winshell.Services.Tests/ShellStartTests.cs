using System.Diagnostics;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class ShellStartTests
{
    private static readonly ShellLocations Locations = new(@"C:\config", @"C:\local");

    [TestMethod]
    public void After_and_no_duplicate_are_read_and_the_rest_stays_the_programs()
    {
        var arguments = CommandLine.ReadProgram(["--theme", "dark", "--after", "4242", "-n"]);

        Assert.AreEqual(4242, arguments.After);
        Assert.IsTrue(arguments.NoDuplicate);
        Assert.AreSequenceEqual(["--theme", "dark", "-n"], arguments.Kept);
    }

    [TestMethod]
    [DataRow("--after")]
    [DataRow("--after soon")]
    [DataRow("-- --after 12 -n")]
    public void Without_a_process_id_or_after_a_double_dash_nothing_is_taken(string line)
    {
        var given = line.Split(' ');

        var arguments = CommandLine.ReadProgram(given);

        Assert.IsNull(arguments.After);
        Assert.IsFalse(arguments.NoDuplicate);
        Assert.AreSequenceEqual(given, arguments.Kept);
    }

    [TestMethod]
    public void The_next_process_keeps_the_arguments_and_names_only_this_one()
    {
        var arguments = CommandLine.ReadProgram(["--after", "1", "--no-duplicate", "--", "a"]);

        Assert.AreSequenceEqual(["--after", "2", "--no-duplicate", "--", "a"], arguments.ForNext(2));
        Assert.AreEqual(2, CommandLine.ReadProgram(arguments.ForNext(2)).After);
        Assert.IsFalse(CommandLine.ReadProgram(arguments.ForNext(2)).Daemonize);
    }

    [TestMethod]
    public void A_detached_start_drops_d_and_keeps_the_handoff()
    {
        var arguments = CommandLine.ReadProgram(["-d", "--after", "7", "--theme", "dark", "--daemonize", "--", "-d"]);

        Assert.IsTrue(arguments.Daemonize);
        Assert.AreEqual(7, arguments.After);
        Assert.AreSequenceEqual(["--theme", "dark", "--", "-d"], arguments.Kept);
        Assert.AreSequenceEqual(["--after", "7", "--theme", "dark", "--", "-d"], arguments.Detached);
        Assert.AreSequenceEqual(["--after", "8", "--theme", "dark", "--", "-d"], arguments.ForNext(8));
        Assert.IsFalse(CommandLine.ReadProgram(arguments.ForNext(8)).Daemonize);

        var start = InstanceStart.DetachedStart(@"C:\shells\winshell-next.exe", arguments.Detached);
        Assert.AreEqual(@"C:\shells\winshell-next.exe", start.FileName);
        Assert.IsFalse(start.UseShellExecute);
        Assert.IsTrue(start.CreateNoWindow);
        Assert.AreEqual(ProcessWindowStyle.Normal, start.WindowStyle);
        Assert.AreSequenceEqual(["--after", "7", "--theme", "dark", "--", "-d"], start.ArgumentList);
    }

    [TestMethod]
    public void A_given_shell_id_names_the_folders()
    {
        var identity = ShellIdentity.Resolve(@"C:\shells\mine\mine.exe", Locations, "mine-id", null, null, null);

        Assert.AreEqual("mine-id", identity.ShellId);
        Assert.AreEqual(@"C:\local\data\by-shell\mine-id", identity.DataDir);
        Assert.AreEqual(@"C:\local\state\by-shell\mine-id", identity.StateDir);
        Assert.AreEqual(@"C:\local\cache\by-shell\mine-id", identity.CacheDir);
    }

    [TestMethod]
    [DataRow(@"C:\config\bin\shell\Mine.exe", "root")]
    [DataRow(@"C:\config\Bar\bin\shell\Bar.exe", "bar")]
    [DataRow(@"C:\config\Bar\bin\shell.next\Bar.exe", "bar")]
    public void A_configuration_in_the_config_folder_is_named_after_its_folder(string program, string id)
    {
        Assert.AreEqual(id, IdOf(program));
    }

    [TestMethod]
    public void A_configuration_anywhere_else_is_named_after_its_path_in_any_case_from_either_build_folder()
    {
        var one = IdOf(@"C:\shells\mine\bin\shell\Mine.exe");

        Assert.AreEqual("cea0f30d0e45e5a6bf33f1d21c3e6351", one);
        Assert.AreEqual(one, IdOf(@"C:\shells\mine\bin\shell.next\Mine.exe"));
        Assert.AreEqual(one, IdOf(@"C:\SHELLS\MINE\BIN\SHELL\MINE.EXE"));
        Assert.AreNotEqual(one, IdOf(@"C:\shells\yours\bin\shell\Yours.exe"));
    }

    [TestMethod]
    public void Folders_named_after_the_earlier_hash_move_to_the_configurations_name_once()
    {
        using var temp = new TempFolder();
        var locations = new ShellLocations(Path.Combine(temp.Path, "config"), Path.Combine(temp.Path, "local"));
        var program = Path.Combine(locations.ConfigRoot, "bar", "bin", "shell", "Bar.exe");
        var earlier = ShellIdentity.Resolve(program, locations with { ConfigRoot = "" }, null, null, null, null);
        Directory.CreateDirectory(earlier.DataDir);
        File.WriteAllText(Path.Combine(earlier.DataDir, "kept.txt"), "data");
        Directory.CreateDirectory(earlier.StateDir);
        var messages = new List<string>();
        var log = new Log { Sink = (_, category, message) => messages.Add($"{category}: {message}") };

        var identity = ShellIdentity.Resolve(program, locations, null, null, null, null);
        identity.CarryOver(log);

        Assert.AreEqual("data", File.ReadAllText(Path.Combine(identity.DataDir, "kept.txt")));
        Assert.IsTrue(Directory.Exists(identity.StateDir));
        Assert.IsFalse(Directory.Exists(identity.CacheDir));
        Assert.IsFalse(Directory.Exists(earlier.DataDir));
        Assert.AreSequenceEqual(
        [
            $"shell: Moved \"{earlier.DataDir}\" to \"{identity.DataDir}\"",
            $"shell: Moved \"{earlier.StateDir}\" to \"{identity.StateDir}\""
        ], messages);

        Directory.CreateDirectory(earlier.DataDir);
        identity.CarryOver(log);

        Assert.IsTrue(Directory.Exists(earlier.DataDir));
        Assert.AreEqual(2, messages.Count);
    }

    [TestMethod]
    public void A_given_shell_id_moves_nothing_and_a_given_folder_is_not_moved_into()
    {
        using var temp = new TempFolder();
        var locations = new ShellLocations(Path.Combine(temp.Path, "config"), Path.Combine(temp.Path, "local"));
        var program = Path.Combine(locations.ConfigRoot, "bar", "bin", "shell", "Bar.exe");
        var earlier = ShellIdentity.Resolve(program, locations with { ConfigRoot = "" }, null, null, null, null);
        Directory.CreateDirectory(earlier.DataDir);
        Directory.CreateDirectory(earlier.StateDir);
        var given = Path.Combine(temp.Path, "state");

        ShellIdentity.Resolve(program, locations, "bar", null, null, null).CarryOver(new Log());
        Assert.IsTrue(Directory.Exists(earlier.DataDir));

        ShellIdentity.Resolve(program, locations, null, null, given, null).CarryOver(new Log());
        Assert.IsTrue(Directory.Exists(earlier.StateDir));
        Assert.IsFalse(Directory.Exists(given));
    }

    [TestMethod]
    public void A_given_folder_is_next_to_the_program_and_can_name_the_base()
    {
        var identity = ShellIdentity.Resolve(@"C:\shells\mine.exe", Locations with { ConfigRoot = "" }, "id", "data",
            @"$BASE\other", @"D:\cache");

        Assert.AreEqual(@"C:\shells\data", identity.DataDir);
        Assert.AreEqual(@"C:\local\state\other", identity.StateDir);
        Assert.AreEqual(@"D:\cache", identity.CacheDir);
    }

    [TestMethod]
    public void A_relative_folder_is_in_the_project_or_next_to_a_program_outside_its_build()
    {
        var built = ShellIdentity.Resolve(@"C:\shells\mine\bin\shell\Mine.exe", Locations, "id", "data", "state",
            "cache");

        Assert.AreEqual(@"C:\shells\mine\data", built.DataDir);
        Assert.AreEqual(@"C:\shells\mine\state", built.StateDir);
        Assert.AreEqual(@"C:\shells\mine\cache", built.CacheDir);

        var reloaded = ShellIdentity.Resolve(@"C:\shells\mine\bin\shell.next\Mine.exe", Locations, "id", "data",
            "state", "cache");

        Assert.AreEqual(@"C:\shells\mine\data", reloaded.DataDir);
        Assert.AreEqual(@"C:\shells\mine\state", reloaded.StateDir);
        Assert.AreEqual(@"C:\shells\mine\cache", reloaded.CacheDir);

        var loose = ShellIdentity.Resolve(@"C:\shells\mine\bin\Debug\Mine.exe", Locations, "id", "data", null, null);

        Assert.AreEqual(@"C:\shells\mine\bin\Debug\data", loose.DataDir);
    }

    [TestMethod]
    public void A_shell_id_names_its_mutex_in_any_case()
    {
        Assert.AreEqual(HeldMutex.NoDuplicateName(Locations, "Mine"), HeldMutex.NoDuplicateName(Locations, "mine"));
    }

    private static string IdOf(string program)
    {
        return ShellIdentity.Resolve(program, Locations, null, null, null, null).ShellId;
    }
}
