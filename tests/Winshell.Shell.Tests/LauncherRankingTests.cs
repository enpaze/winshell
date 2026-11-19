namespace Winshell.ShellTests;

[TestClass]
public class LauncherRankingTests
{
    [TestMethod]
    public void A_command_goes_before_the_first_app_whose_name_matches_less_well()
    {
        var ranked = LauncherRanking.Rank("lock", Items("App", "Lockbox", "Clock", "Block editor"),
            Items("Command", "Lock", "Unlock all"));

        Assert.AreSequenceEqual(["Lock", "Lockbox", "Clock", "Block editor", "Unlock all"], ranked.Select(item => item.Name));
    }

    [TestMethod]
    public void Apps_keep_the_order_search_gave_them_and_unmatched_commands_are_left_out()
    {
        var ranked = LauncherRanking.Rank("note", Items("App", "Notepad", "OneNote"), Items("Command", "Sleep"));

        Assert.AreSequenceEqual(["Notepad", "OneNote"], ranked.Select(item => item.Name));
    }

    [TestMethod]
    public void With_nothing_typed_every_command_follows_the_apps()
    {
        var ranked = LauncherRanking.Rank(" ", Items("App", "Notepad"), Items("Command", "Sleep", "Lock"));

        Assert.AreSequenceEqual(["Notepad", "Sleep", "Lock"], ranked.Select(item => item.Name));
    }

    private static List<LauncherItem> Items(string kind, params string[] names)
    {
        return [.. names.Select(name => new LauncherItem(kind + ":" + name, name, kind, () => { }))];
    }
}
