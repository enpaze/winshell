using Winshell.Native;

namespace Winshell.ShellTests;

public sealed class LauncherTests
{
    private const uint CtrlAltShift = 0x7;
    private const int F22 = 0x85;
    private const int F23 = 0x86;

    [Fact]
    public async Task A_changed_hotkey_in_the_preferences_file_frees_the_old_chord_and_claims_the_new_one()
    {
        await ShellFixture.On(async shell =>
        {
            var path = PreferencesTests.PathOf(shell);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = "launcher" + Guid.NewGuid().ToString("N");
            File.WriteAllText(path, $"[{name}]\nhotkey = \"Ctrl+Alt+Shift+F22\"\n");
            try
            {
                using var launcher = new Launcher { Name = name };

                await Wait.UntilAsync(() => !KeyInput.Unclaimed(CtrlAltShift, F22),
                    "the launcher to claim the hotkey from the file");
                Assert.True(KeyInput.Unclaimed(CtrlAltShift, F23));

                File.WriteAllText(path, $"[{name}]\nhotkey = \"Ctrl+Alt+Shift+F23\"\n");
                await Wait.UntilAsync(() => !KeyInput.Unclaimed(CtrlAltShift, F23),
                    "the launcher to claim the changed hotkey");
                Assert.True(KeyInput.Unclaimed(CtrlAltShift, F22));
            }
            finally
            {
                File.Delete(path);
            }

            Assert.True(KeyInput.Unclaimed(CtrlAltShift, F23));
            return true;
        });
    }
}
