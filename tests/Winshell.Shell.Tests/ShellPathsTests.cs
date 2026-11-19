namespace Winshell.ShellTests;

[TestClass]
[DoNotParallelize]
public sealed class ShellPathsTests
{
    private sealed class Section(string name) : Preferences(name);

    [TestMethod]
    public async Task Changing_the_config_folder_after_start_moves_neither_the_shells_folder_nor_the_preferences_file()
    {
        await ShellFixture.On(async shell =>
        {
            var shells = shell.Paths.Shells;
            var path = Path.Combine(shells, "preferences", shell.ShellName + ".toml");
            var moved = TempFolder.NewPath();
            var name = "paths" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(ShellLocations.ShellsVariable, moved);
            try
            {
                Assert.AreEqual(moved, ShellLocations.Current.ShellsRoot);
                Assert.AreEqual(shells, shell.Paths.Shells);

                using var section = new Section(name);
                await Wait.UntilAsync(
                    () => File.Exists(path) && File.ReadAllText(path).Contains($"[{name}]", StringComparison.Ordinal),
                    "the new section in the preferences file under the folder the shell started with");
                Assert.IsFalse(Directory.Exists(moved));
            }
            finally
            {
                Environment.SetEnvironmentVariable(ShellLocations.ShellsVariable, shells);
                if (File.Exists(path))
                    File.Delete(path);
            }

            return true;
        });
    }
}

