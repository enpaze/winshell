using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Winshell.Tests;

[TestClass]
public sealed partial class DoctorCommandTests : IDisposable
{
    private static readonly string[] Ids =
    [
        "windows", "dotnet", "project", "built", "build",
        "folders", "appBars", "dpi", "signIn", "notifications", "instances", "crashes"
    ];

    private readonly string _root =
        TempFolder.NewPath();

    public DoctorCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    [TestMethod]
    public void Json_lists_every_check_and_an_unbuilt_project_fails()
    {
        var project = Path.Combine(_root, "broken");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "Broken.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = Run("doctor", "--json", "-p", project);

        result.AssertExitCode(1);
        using var json = JsonDocument.Parse(result.Output);
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToList();
        Assert.AreSequenceEqual(Ids, checks.Select(c => c.GetProperty("id").GetString()));
        var built = checks.Single(c => c.GetProperty("id").GetString() == "built");
        Assert.AreEqual("fail", built.GetProperty("status").GetString());
        Assert.Contains("has not been built", built.GetProperty("message").GetString()!, StringComparison.Ordinal);
        Assert.Contains("Run winshell run once", built.GetProperty("fix").GetString()!, StringComparison.Ordinal);
        Assert.IsTrue(json.RootElement.GetProperty("summary").GetProperty("fail").GetInt32() >= 1);
    }

    [TestMethod]
    public void A_path_that_is_not_there_checks_no_project_beside_it()
    {
        var folder = Path.Combine(_root, "beside");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Other.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var missing = Path.Combine(folder, "missing");

        var result = Run("doctor", "--json", "-p", missing);

        result.AssertExitCode(1);
        using var json = JsonDocument.Parse(result.Output);
        var messages = json.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetString() ?? "", c => c.GetProperty("message").GetString());
        Assert.StartsWith("No .csproj at ", messages["project"], StringComparison.Ordinal);
        Assert.EndsWith(@"\beside\missing.", messages["project"], StringComparison.Ordinal);
        Assert.AreEqual("No shell project to build.", messages["built"]);
    }

    [TestMethod]
    public void A_folder_given_with_p_is_not_searched_like_the_config_folder()
    {
        var folder = Path.Combine(_root, "holder");
        Directory.CreateDirectory(Path.Combine(folder, "default"));
        File.WriteAllText(Path.Combine(folder, "default", "Inner.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = Run("doctor", "--json", "-p", folder);

        result.AssertExitCode(1);
        using var json = JsonDocument.Parse(result.Output);
        var messages = json.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetString() ?? "", c => c.GetProperty("message").GetString());
        Assert.StartsWith("No .csproj at ", messages["project"], StringComparison.Ordinal);
        Assert.EndsWith(@"\holder.", messages["project"], StringComparison.Ordinal);
        Assert.AreEqual("No shell project to build.", messages["built"]);
    }

    [TestMethod]
    public void Text_has_a_line_a_check_and_no_color_when_redirected()
    {
        var empty = Path.Combine(_root, "empty");
        Directory.CreateDirectory(empty);

        var (_, output, _) = RunIn(empty, "doctor");

        Assert.DoesNotContain("\e[", output);
        Assert.Contains("  WARN  Project", output, StringComparison.Ordinal);
        Assert.MatchesRegex(@"\d+ passed, \d+ warnings?, \d+ failed\.", output);
    }

    [TestMethod]
    public void Without_a_config_it_warns_that_nothing_runs_checks_this_PC_and_writes_nothing()
    {
        var empty = Path.Combine(_root, "none");
        var local = Path.Combine(_root, "local");
        Directory.CreateDirectory(empty);

        var (_, output, _) = WinshellCli.Run(["doctor", "--json"], (ShellLocations.ShellsVariable, empty),
            (ShellLocations.LocalVariable, local));

        using var json = JsonDocument.Parse(output);
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToList();
        var project = checks.Single(c => c.GetProperty("id").GetString() == "project");
        Assert.AreEqual("warn", project.GetProperty("status").GetString());
        Assert.Contains("starts nothing", project.GetProperty("message").GetString()!, StringComparison.Ordinal);
        Assert.AreEqual("pass", checks.Single(c => c.GetProperty("id").GetString() == "crashes").GetProperty("status")
            .GetString());
        var messages = checks.Select(c => c.GetProperty("message").GetString()!).ToList();
        Assert.DoesNotContain(m => m.StartsWith("Could not check", StringComparison.Ordinal), messages);
        foreach (var c in checks.Where(c => c.GetProperty("status").GetString() != "pass"))
            Assert.IsFalse(string.IsNullOrEmpty(c.GetProperty("fix").GetString()));
        Assert.DoesNotContain(m => m.Contains(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase), messages);
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(empty));
        Assert.IsFalse(Directory.Exists(local));
    }

    [TestMethod]
    public void A_config_folder_it_cannot_list_fails_the_project_check_and_the_others_still_run()
    {
        var unreadable = Path.Combine(_root, "unreadable");
        Directory.CreateDirectory(unreadable);
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory,
            AccessControlType.Deny);
        var folder = new DirectoryInfo(unreadable);
        var acl = folder.GetAccessControl();
        acl.AddAccessRule(deny);
        folder.SetAccessControl(acl);
        ChildProcess result;
        try
        {
            result = RunIn(unreadable, "doctor", "--json");
        }
        finally
        {
            acl.RemoveAccessRule(deny);
            folder.SetAccessControl(acl);
        }

        result.AssertExitCode(1);
        Assert.IsEmpty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        var checks = json.RootElement.GetProperty("checks").EnumerateArray().ToList();
        Assert.AreEqual(Ids.Length, checks.Count);
        var project = checks.Single(c => c.GetProperty("id").GetString() == "project");
        Assert.AreEqual("fail", project.GetProperty("status").GetString());
        Assert.StartsWith("Winshell cannot read the shells folder", project.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void Json_without_a_temp_folder_still_checks_the_preferences_file_of_the_evaluated_shell_name()
    {
        var shell = Path.Combine(_root, "Work");
        Directory.CreateDirectory(shell);
        File.WriteAllText(Path.Combine(shell, "Work.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><ShellName>desk</ShellName></PropertyGroup></Project>");
        Preferences("desk");

        var project = ProjectCheck(RunWithoutTemp("doctor", "--json", "-p", shell));

        Assert.AreEqual("warn", project.GetProperty("status").GetString());
        var message = project.GetProperty("message").GetString();
        Assert.IsNotNull(message);
        Assert.Contains(@"\preferences\desk.toml[", message, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be read", message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Json_without_a_temp_folder_reports_the_assumed_shell_name_when_msbuild_cannot_load_the_project()
    {
        var shell = Path.Combine(_root, "Work");
        Directory.CreateDirectory(shell);
        File.WriteAllText(Path.Combine(shell, "Work.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><ShellName>desk</ShellName>");
        Preferences("work");

        var project = ProjectCheck(RunWithoutTemp("doctor", "--json", "-p", shell));

        Assert.AreEqual("warn", project.GetProperty("status").GetString());
        var message = project.GetProperty("message").GetString();
        Assert.IsNotNull(message);
        Assert.Contains("The shell name could not be read from Work.csproj, so doctor assumed \"work\" and checked ",
            message, StringComparison.Ordinal);
        Assert.Contains("MSB4025", message, StringComparison.Ordinal);
        Assert.Contains(@"\preferences\work.toml[", message, StringComparison.Ordinal);
        Assert.Contains("dotnet msbuild", project.GetProperty("fix").GetString()!, StringComparison.Ordinal);
    }

    private void Preferences(string name)
    {
        var preferences = Path.Combine(_root, "preferences");
        Directory.CreateDirectory(preferences);
        File.WriteAllText(Path.Combine(preferences, name + ".toml"), "colorScheme =\n");
    }

    private static JsonElement ProjectCheck(ChildProcess result)
    {
        result.AssertExitCode(1);
        Assert.IsEmpty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == "project").Clone();
    }

    private ChildProcess RunWithoutTemp(params string[] args)
    {
        var missing = Path.Combine(_root, "no-temp");
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, (ShellLocations.ShellsVariable, _root), ("TMP", missing), ("TEMP", missing));
#pragma warning restore CsWinRT1030
    }

    private ChildProcess Run(params string[] args)
    {
        return RunIn(_root, args);
    }

    private static ChildProcess RunIn(string shellsRoot, params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, (ShellLocations.ShellsVariable, shellsRoot));
#pragma warning restore CsWinRT1030
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
