namespace Winshell.ShellTests;

[Collection(nameof(ShellPathsTests))]
public sealed class ShellPathsTests
{
    private sealed class Section(string name) : Preferences(name);

    [Fact]
    public async Task Changing_the_config_folder_after_start_moves_neither_the_shells_folder_nor_the_preferences_file()
    {
        await ShellFixture.On(async shell =>
        {
            var shells = shell.Paths.Shells;
            var path = Path.Combine(shells, "preferences", shell.ShellName + ".toml");
            var moved = TempFolder.NewPath();
            var name = "paths" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(ShellLocations.ConfigVariable, moved);
            try
            {
                Assert.Equal(moved, ShellLocations.Current.ShellsRoot);
                Assert.Equal(shells, shell.Paths.Shells);

                using var section = new Section(name);
                await Wait.UntilAsync(
                    () => File.Exists(path) && File.ReadAllText(path).Contains($"[{name}]", StringComparison.Ordinal),
                    "the new section in the preferences file under the folder the shell started with");
                Assert.False(Directory.Exists(moved));
            }
            finally
            {
                Environment.SetEnvironmentVariable(ShellLocations.ConfigVariable, shells);
                if (File.Exists(path))
                    File.Delete(path);
            }

            return true;
        });
    }
}

[CollectionDefinition(nameof(ShellPathsTests), DisableParallelization = true)]
public sealed class ShellPathsCollection;
