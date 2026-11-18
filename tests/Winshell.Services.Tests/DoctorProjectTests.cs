using Winshell.Diagnostics;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class DoctorProjectTests : IDisposable
{
    private readonly string _root = TempFolder.NewPath();

    public DoctorProjectTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    private const string Plain = "<Project Sdk=\"Microsoft.NET.Sdk\" />";

    private static readonly DateTime Long = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void No_project_warns_that_nothing_runs_and_has_nothing_to_build()
    {
        var checks = Checks(Path.Combine(_root, "missing"));

        Assert.AreSequenceEqual(["project", "built", "build"], checks.Select(c => c.Id));
        Assert.AreEqual(CheckStatus.Warn, checks[0].Status);
        Assert.AreEqual("No configuration project, so winshell run starts nothing.", checks[0].Message);
        Assert.AreEqual("Run winshell new to make one.", checks[0].Fix);
        foreach (var c in checks.Skip(1))
            Assert.AreEqual(CheckStatus.Pass, c.Status);
        Assert.Contains("No configuration project", checks[1].Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Two_projects_say_to_pick_one_and_are_not_built()
    {
        Project("a\\A.csproj");
        Project("b\\B.csproj");

        var project = Assert.ContainsSingle(c => c.Id == "project", Checks(_root));

        Assert.AreEqual(CheckStatus.Fail, project.Status);
        Assert.Contains("pick one with -c: a, b", project.Message, StringComparison.Ordinal);
        Assert.Contains("winshell -c", project.Fix!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_project_names_itself_and_a_missing_program_fails_once_and_asks_for_one_run()
    {
        var file = Project("Mine.csproj");
        var before = Entries();

        var checks = Checks(_root);

        Assert.AreEqual(CheckStatus.Pass, checks[0].Status);
        Assert.AreEqual(file, checks[0].Message);
        Assert.AreEqual(CheckStatus.Fail, checks[1].Status);
        Assert.AreEqual("Mine has not been built.", checks[1].Message);
        Assert.AreEqual("Run winshell run once to build it.", checks[1].Fix);
        Assert.AreEqual(CheckStatus.Pass, checks[2].Status);
        Assert.AreEqual("Nothing to compare until Mine is built.", checks[2].Message);
        Assert.IsNull(checks[2].Fix);
        Assert.AreSequenceEqual(before, Entries());
    }

    [TestMethod]
    public void A_built_program_passes_until_a_source_is_newer()
    {
        var file = Project("Mine.csproj",
            "<Project><PropertyGroup><AssemblyName>Shell</AssemblyName></PropertyGroup></Project>");
        File.SetLastWriteTimeUtc(file, Long);
        var program = Path.Combine(_root, "bin", "shell", "Shell.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(program)!);
        File.WriteAllText(program, "");
        File.SetLastWriteTimeUtc(program, Long.AddMinutes(1));

        var built = Checks(_root);
        Assert.AreEqual(CheckStatus.Pass, built[1].Status);
        Assert.AreEqual(program, built[1].Message);
        Assert.AreEqual(CheckStatus.Pass, built[2].Status);
        Assert.AreEqual("Mine is up to date.", built[2].Message);

        var source = Project("Views\\Mine.xaml");
        File.SetLastWriteTimeUtc(source, Long.AddMinutes(2));
        var stale = Checks(_root);
        Assert.AreEqual(CheckStatus.Pass, stale[1].Status);
        Assert.AreEqual(CheckStatus.Warn, stale[2].Status);
        Assert.AreEqual("A file in Mine is newer than the program.", stale[2].Message);
    }

    private static IReadOnlyList<DoctorCheck> Checks(string configRoot)
    {
        var project = ShellProject.Find(null, null, configRoot, out var problem);
        return DoctorProject.Checks(project, problem);
    }

    private string Project(string relative, string text = Plain)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private string[] Entries()
    {
        return Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();
    }
}
