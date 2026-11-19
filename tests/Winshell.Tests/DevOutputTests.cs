namespace Winshell.Tests;

[TestClass]
public sealed class DevOutputTests
{
    private const string Broken = @"C:\Shells\Mine\Marker.cs(4,45): error CS1002: ; expected";

    private const string Shown = "Marker.cs(4,45): error CS1002: ; expected";

    private readonly DevOutput _output = new(@"C:\Shells\Mine");

    [TestMethod]
    [DataRow("dotnet watch : File updated: .\\Marker.cs")]
    [DataRow("dotnet watch : Waiting for changes")]
    [DataRow("dotnet watch : No managed code changes to apply.")]
    [DataRow("dotnet watch : Restart is needed to apply the changes.")]
    [DataRow(" INFO shell: Shell name: \"Mine\"")]
    [DataRow("  Mine -> C:\\Shells\\Mine\\bin\\Debug\\Mine.dll")]
    [DataRow(" WARN notes: the word error: appears in this log line")]
    public void A_line_that_is_not_a_result_changes_nothing_on_screen(string line)
    {
        Assert.IsNull(_output.Read(line));
    }

    [TestMethod]
    public void A_compile_error_in_an_edit_is_what_the_notice_says_from_the_project_folder()
    {
        _output.Read("dotnet watch : Unable to apply changes due to compilation errors.");

        Assert.AreEqual(Shown, _output.Read("dotnet watch : " + Broken));
    }

    [TestMethod]
    [DataRow(@"C:\Shared\Lib.cs(1,1): error CS0103: The name 'x' does not exist")]
    [DataRow(@"C:\Shells\Mine2\Lib.cs(1,1): error CS0103: The name 'x' does not exist")]
    public void An_error_outside_the_project_keeps_its_full_path(string line)
    {
        Assert.AreEqual(line, _output.Read(line));
    }

    [TestMethod]
    public void A_compile_error_in_a_full_build_loses_its_project_and_comes_once()
    {
        const string line = Broken + @" [C:\Shells\Mine\Mine.csproj::TargetFramework=net11.0-windows10.0.22621.0]";

        Assert.AreEqual(Shown, _output.Read("  " + line));
        Assert.IsNull(_output.Read("  " + line));
    }

    [TestMethod]
    public void A_xaml_compiler_error_is_what_the_notice_says()
    {
        const string line =
            @"C:\Shells\Mine\PanelView.xaml(2,5): XamlCompiler error WMC0130: Invalid value for 'PanelView'.";

        Assert.AreEqual("PanelView.xaml(2,5): XamlCompiler error WMC0130: Invalid value for 'PanelView'.",
            _output.Read(line));
    }

    [TestMethod]
    public void A_second_error_joins_the_first_and_the_list_stops_at_ten()
    {
        var shown = Enumerable.Range(1, 12).Select(i => _output.Read($"A.cs({i},1): error CS0103: No name {i}"))
            .ToList();

        Assert.AreEqual("A.cs(1,1): error CS0103: No name 1\nA.cs(2,1): error CS0103: No name 2", shown[1]);
        Assert.AreEqual(10, shown[9]!.Split('\n').Length);
        Assert.AreSequenceEqual([null, null], shown[10..]);
    }

    [TestMethod]
    public void An_edit_that_still_fails_replaces_the_errors_of_the_last_one()
    {
        _output.Read("dotnet watch : " + Broken);

        _output.Read("dotnet watch : Unable to apply changes due to compilation errors.");

        Assert.AreEqual("B.cs(1,1): error CS1513: } expected",
            _output.Read("dotnet watch : B.cs(1,1): error CS1513: } expected"));
    }

    [TestMethod]
    [DataRow("dotnet watch : C# and Razor changes applied in 103ms.")]
    [DataRow("Build succeeded.")]
    public void Code_that_applies_takes_the_notice_away_and_forgets_the_errors(string line)
    {
        _output.Read("dotnet watch : " + Broken);

        Assert.AreEqual("", _output.Read(line));
        Assert.AreEqual(Shown, _output.Read("dotnet watch : " + Broken));
    }
}
