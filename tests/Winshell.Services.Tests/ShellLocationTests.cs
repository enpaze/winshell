namespace Winshell.Services.Tests;

[TestClass]
public class ShellLocationTests
{
    [TestMethod]
    [DataRow(Environment.SpecialFolder.ApplicationData)]
    [DataRow(Environment.SpecialFolder.LocalApplicationData)]
    public void A_blank_override_uses_the_windows_profile(Environment.SpecialFolder profile)
    {
        var expected = Path.Combine(Environment.GetFolderPath(profile), "winshell");

        Assert.AreEqual(expected, ShellLocations.RootFrom(null, profile));
        Assert.AreEqual(expected, ShellLocations.RootFrom("  ", profile));
    }

    [TestMethod]
    public void The_local_override_names_the_local_root()
    {
        var local = Environment.GetEnvironmentVariable("WINSHELL_LOCAL_DIR")!;

        Assert.AreEqual(local, ShellLocations.Current.LocalRoot);
        Assert.AreNotEqual(local, ShellLocations.Current.ConfigRoot);
    }

    [TestMethod]
    public void An_override_is_a_fully_qualified_root()
    {
        var dir = TempFolder.NewPath();
        var profile = Environment.SpecialFolder.LocalApplicationData;

        Assert.AreEqual(dir, ShellLocations.RootFrom(dir, profile));
        Assert.AreEqual(dir, ShellLocations.RootFrom("  " + dir + "  ", profile));
        Assert.AreEqual(Path.GetFullPath("winshell-relative"), ShellLocations.RootFrom("winshell-relative", profile));
    }

    [TestMethod]
    public void The_local_folders_are_under_the_local_root()
    {
        var folders = new ShellLocations(@"C:\config", @"C:\local");

        Assert.AreEqual(@"C:\local\cache", folders.CacheRoot);
        Assert.AreEqual(@"C:\local\state", folders.StateRoot);
        Assert.AreEqual(@"C:\local\data", folders.DataRoot);
        Assert.AreEqual(@"C:\local\run", folders.RunRoot);
        Assert.AreEqual(@"C:\local\crashes", folders.CrashRoot);
    }
}
