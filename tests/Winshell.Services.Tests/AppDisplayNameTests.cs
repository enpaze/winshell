using Winshell.Apps;

namespace Winshell.Services.Tests;

/// <summary>
/// Audio sessions, media sessions and privacy uses ask the Apps service for one program's display name.
/// </summary>
public sealed class AppDisplayNameTests
{
    [Fact]
    public void A_program_s_display_name_is_its_file_description()
    {
        var path = typeof(AppsService).Assembly.Location;
        // The project sets no description, so the file description is the assembly name.
        Assert.Equal("Winshell.Services", AppsService.ProgramDescription(path));
    }

    [Fact]
    public void A_missing_or_unreadable_program_has_no_display_name()
    {
        using var folder = new TempFolder();
        var missing = Path.Combine(folder.Path, "gone.exe");
        var blank = Path.Combine(folder.Path, "Player.exe");
        File.WriteAllText(blank, "not a program");

        Assert.Null(AppsService.ProgramDescription(missing));
        Assert.Null(AppsService.ProgramDescription(blank));
        Assert.Null(AppsService.ProgramDescription(""));
    }

    [Fact]
    public void A_program_with_no_description_is_shown_by_its_file_name()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "Player.exe");
        File.WriteAllText(path, "not a program");
        // No running program and no Start entry uses this name, so the lookup falls through to the file name.
        var appId = "winshell-name-" + Guid.NewGuid().ToString("N") + ".exe";

        Assert.Null(AppsService.ProgramDescription(path));
        Assert.Equal("Player", AppsService.FallbackName("Player.exe"));
        Assert.Equal(appId[..^4], AppsService.DisplayName(appId));
    }
}
