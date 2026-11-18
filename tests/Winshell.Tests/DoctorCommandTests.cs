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
        Assert.AreEqual("No configuration project to build.", messages["built"]);
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
        Assert.AreEqual("No configuration project to build.", messages["built"]);
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

        var (_, output, _) = WinshellCli.Run(["doctor", "--json"], (ShellLocations.ConfigVariable, empty),
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
        Assert.StartsWith("Winshell cannot read the config folder", project.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    private ChildProcess Run(params string[] args)
    {
        return RunIn(_root, args);
    }

    private static ChildProcess RunIn(string configRoot, params string[] args)
    {
#pragma warning disable CsWinRT1030
        return WinshellCli.Run(args, (ShellLocations.ConfigVariable, configRoot));
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
