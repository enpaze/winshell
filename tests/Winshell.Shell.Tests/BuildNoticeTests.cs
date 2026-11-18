namespace Winshell.ShellTests;

public sealed partial class BuildNoticeTests : IDisposable
{
    private const string Mine = @"C:\mine\Mine.csproj";

    private readonly string _dir = TempFolder.NewPath();
    private readonly ShellNotices _notices;

    public BuildNoticeTests()
    {
        var crashes = new CrashReports(_dir);
        _notices = new ShellNotices(new ManualEventLoop(), new Log(), crashes, (_, _, _) => new Window(), (_, _) => { },
            () => false);
    }

    public void Dispose()
    {
        _notices.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void A_build_that_works_closes_the_build_error_of_its_project()
    {
        _notices.ShowBuildError("Bar.cs(1,1): error CS1002: ; expected", Mine);

        _notices.ShowBuildError("", Mine);

        Assert.Null(_notices.Current);
    }

    [Fact]
    public void A_build_that_works_leaves_the_build_error_of_another_project()
    {
        _notices.ShowBuildError("Bar.cs(1,1): error CS1002: ; expected", @"C:\other\Other.csproj");

        _notices.ShowBuildError("", Mine);

        Assert.Equal(NoticeKind.BuildError, _notices.Current?.Kind);
    }

    [Fact]
    public void A_build_that_works_leaves_a_file_error_on_screen()
    {
        _notices.ShowFileError(@"C:\mine\preferences.toml", "preferences.toml[3:1]: expected a value");

        _notices.ShowBuildError("", Mine);

        Assert.Equal(NoticeKind.FileError, _notices.Current?.Kind);
    }

    private sealed partial class Window : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
