namespace Winshell.TestSupport;

// Each test project compiles this file, so a use in another project is invisible here.
/// <summary>The repository these tests were built from.</summary>
internal static class Repo
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "winshell.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("The tests run outside the repository.");
    }
}
