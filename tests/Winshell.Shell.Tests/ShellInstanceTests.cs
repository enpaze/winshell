namespace Winshell.ShellTests;

public sealed class ShellInstanceTests
{
    private static async Task<InstanceInfo> Own()
    {
        await ShellFixture.Current();
        return ShellLocations.Current.Instances.Running().Single(i => i.Pid == Environment.ProcessId);
    }

    private static async Task<List<IpcResponse>> Ask(IpcRequest request)
    {
        var info = await Own();
        var responses = new List<IpcResponse>();
        await foreach (var reply in IpcClient.Request(info.Pipe, request, TestContext.Current.CancellationToken))
        {
            if (reply is IpcResponse response)
                responses.Add(response);
            break;
        }

        return responses;
    }

    [Fact]
    public async Task A_running_shell_is_listed_under_its_id_and_program()
    {
        var info = await Own();

        Assert.Equal("winshell-tests", info.ShellId);
        Assert.Equal(Environment.ProcessPath, info.Program);
        Assert.StartsWith("winshell-", info.Pipe, StringComparison.Ordinal);
        Assert.Null(info.ExitedAt);
    }

    [Fact]
    public async Task What_the_shell_logs_goes_to_its_instance_log_with_secrets_taken_out()
    {
        var info = await Own();
        var shell = await ShellFixture.Current();

        shell.Log.Warn("test", "sync --password SENTINEL-PASSWORD");

        await using var stream = new FileStream(info.Log, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("WARN test: sync --password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL-PASSWORD", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_folders_follow_the_shell_name_and_exist_once_read()
    {
        var shell = await ShellFixture.Current();
        var paths = shell.Paths;

        Assert.Equal(Path.Combine(ShellLocations.Current.DataRoot, "by-shell", "winshell-tests"), paths.Data);
        Assert.Equal(Path.Combine(ShellLocations.Current.StateRoot, "by-shell", "winshell-tests"), paths.State);
        Assert.Equal(Path.Combine(ShellLocations.Current.CacheRoot, "by-shell", "winshell-tests"), paths.Cache);
        Assert.True(Directory.Exists(paths.Data));
        Assert.True(Directory.Exists(paths.State));
        Assert.True(Directory.Exists(paths.Cache));
    }

    [Fact]
    public async Task The_program_shells_and_logs_paths_are_where_winshell_looks()
    {
        var info = await Own();
        var paths = (await ShellFixture.Current()).Paths;

        Assert.Equal(Path.GetDirectoryName(Environment.ProcessPath), paths.Program);
        Assert.Equal(ShellLocations.Current.ShellsRoot, paths.Shells);
        Assert.Equal(info.Log, paths.Logs);
        Assert.True(File.Exists(paths.Logs));
    }

    [Fact]
    public async Task The_control_pipe_reaches_what_the_shell_registered()
    {
        var shell = await ShellFixture.Current();
        using var handler = await ShellFixture.On(s => s.Ipc.Handle("tests", "twice", (int number) => number * 2));
        using var property = await ShellFixture.On(s => s.Ipc.Property("tests", "name", () => s.ShellId));

        var shown = Assert.Single(await Ask(IpcRequest.ForShow(false)));
        var called = Assert.Single(await Ask(IpcRequest.ForCall("tests", "twice", ["21"])));
        var read = Assert.Single(await Ask(IpcRequest.ForProp("tests", "name")));

        Assert.Contains("function twice(number: int): int", shown.Value, StringComparison.Ordinal);
        Assert.Equal("42", called.Value);
        Assert.Equal(shell.ShellId, read.Value);
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task A_build_error_shows_on_the_running_shell()
    {
        await ShellFixture.Hold(async () =>
        {
            var shell = await ShellFixture.Current();
            var response =
                Assert.Single(await Ask(IpcRequest.ForBuild("Bar.cs(1,1): error CS1002: ; expected",
                    @"C:\mine\Bar.csproj")));

            try
            {
                Assert.True(response.Ok);
                Assert.Equal(NoticeKind.BuildError, shell.Notices.Current!.Kind);
                Assert.Contains("CS1002", shell.Notices.Current.Body, StringComparison.Ordinal);
                Assert.Equal(Environment.ProcessId, (await Own()).Pid);
            }
            finally
            {
                await DismissNotice();
            }
        });
    }

    [Fact]
    [Trait("Category", "Window")]
    public async Task Work_that_fails_on_the_shell_thread_writes_a_report_and_the_shell_goes_on()
    {
        await ShellFixture.Hold(async () =>
        {
            var shell = await ShellFixture.Current();

            shell.Loop.Post(() => throw new InvalidOperationException("SENTINEL posted failure"));

            await Wait.UntilAsync(
                () => Directory.Exists(ShellLocations.Current.CrashRoot)
                      && Directory.GetFiles(ShellLocations.Current.CrashRoot).Any(f =>
                          ReadClosed(f).Contains("SENTINEL posted failure", StringComparison.Ordinal)),
                "a crash report for the posted failure");
            Assert.True(await ShellFixture.On(_ => true));
            await DismissNotice();
        });
    }

    private static Task DismissNotice()
    {
        return ShellFixture.On(shell =>
        {
            shell.Notices.Hide();
            return true;
        });
    }

    private static string ReadClosed(string path)
    {
        using var reader =
            new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete));
        return reader.ReadToEnd();
    }
}
