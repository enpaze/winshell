namespace Winshell.ShellTests;

[TestClass]
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
        await foreach (var reply in IpcClient.Request(info.Pipe, request, CurrentTest.Cancellation))
        {
            if (reply is IpcResponse response)
                responses.Add(response);
            break;
        }

        return responses;
    }

    [TestMethod]
    public async Task A_running_shell_is_listed_under_its_id_and_program()
    {
        var info = await Own();

        Assert.AreEqual("winshell-tests", info.ShellId);
        Assert.AreEqual(Environment.ProcessPath, info.Program);
        Assert.StartsWith("winshell-", info.Pipe, StringComparison.Ordinal);
        Assert.IsNull(info.ExitedAt);
    }

    [TestMethod]
    public async Task What_the_shell_logs_goes_to_its_instance_log_with_secrets_taken_out()
    {
        var info = await Own();
        var shell = await ShellFixture.Current();

        shell.Log.Warn("test", "sync --password SENTINEL-PASSWORD");

        await using var stream = new FileStream(info.Log, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(CurrentTest.Cancellation);
        Assert.Contains("WARN test: sync --password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL-PASSWORD", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task The_folders_follow_the_shell_id_and_exist_once_read()
    {
        var shell = await ShellFixture.Current();

        Assert.AreEqual(Path.Combine(ShellLocations.Current.DataRoot, "by-shell", "winshell-tests", "a.txt"),
            shell.DataPath("a.txt"));
        Assert.AreEqual(Path.Combine(ShellLocations.Current.StateRoot, "by-shell", "winshell-tests", "a.txt"),
            shell.StatePath("a.txt"));
        Assert.AreEqual(Path.Combine(ShellLocations.Current.CacheRoot, "by-shell", "winshell-tests", "a.txt"),
            shell.CachePath("a.txt"));
        Assert.AreEqual(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "a.txt"), shell.ConfigPath("a.txt"));
        Assert.IsTrue(Directory.Exists(shell.DataDir));
    }

    [TestMethod]
    public async Task The_control_pipe_reaches_what_the_shell_registered()
    {
        var shell = await ShellFixture.Current();
        using var handler = await ShellFixture.On(s => s.Ipc.Handle("tests", "twice", (int number) => number * 2));
        using var property = await ShellFixture.On(s => s.Ipc.Property("tests", "name", () => s.ShellId));

        var shown = Assert.ContainsSingle(await Ask(IpcRequest.ForShow(false)));
        var called = Assert.ContainsSingle(await Ask(IpcRequest.ForCall("tests", "twice", ["21"])));
        var read = Assert.ContainsSingle(await Ask(IpcRequest.ForProp("tests", "name")));

        Assert.Contains("function twice(number: int): int", shown.Value!, StringComparison.Ordinal);
        Assert.AreEqual("42", called.Value);
        Assert.AreEqual(shell.ShellId, read.Value);
    }

    [TestMethod]
    [TestCategory("Window")]
    public async Task A_build_error_shows_on_the_running_shell()
    {
        await ShellFixture.Hold(async () =>
        {
            var shell = await ShellFixture.Current();
            var response =
                Assert.ContainsSingle(await Ask(IpcRequest.ForBuild("Bar.cs(1,1): error CS1002: ; expected",
                    @"C:\mine\Bar.csproj")));

            try
            {
                Assert.IsTrue(response.Ok);
                Assert.AreEqual(NoticeKind.BuildError, shell.Notices.Current!.Kind);
                Assert.Contains("CS1002", shell.Notices.Current.Body, StringComparison.Ordinal);
                Assert.AreEqual(Environment.ProcessId, (await Own()).Pid);
            }
            finally
            {
                await DismissNotice();
            }
        });
    }

    [TestMethod]
    [TestCategory("Window")]
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
            Assert.IsTrue(await ShellFixture.On(_ => true));
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
