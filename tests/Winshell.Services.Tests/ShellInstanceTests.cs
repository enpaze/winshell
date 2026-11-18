using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Winshell.Services.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ShellInstanceTests
{
    private const string Program = @"C:\shells\mine.exe";

    private static List<(InstanceInfo Info, bool Alive)> Rows(ShellInstance instance)
    {
        return ShellLocations.Current.Instances.All().Where(i => i.Info.Id == instance.Id).ToList();
    }

    private static async Task<IpcResponse> Ask(string pipe, IpcRequest request)
    {
        await foreach (var reply in IpcClient.Request(pipe, request, CurrentTest.Cancellation))
        {
            return reply switch
            {
                IpcFailure failure => throw new InvalidOperationException(failure.Reason),
                IpcResponse response => response,
            };
        }

        throw new InvalidOperationException("The server closed the pipe without answering.");
    }

    private static async Task<string?> AskRaw(string pipe, string line)
    {
        await using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(3000, CurrentTest.Cancellation);
        using var reader = new StreamReader(client, Encoding.UTF8);
        await using var writer = new StreamWriter(client, new UTF8Encoding(false));
        writer.AutoFlush = true;
        await writer.WriteLineAsync(line);
        return await reader.ReadLineAsync(CurrentTest.Cancellation);
    }

    private static (string Pipe, EventQueue Loop, IpcServer Server) Serve(IInstanceControl control)
    {
        var pipe = "winshell-test-" + Guid.NewGuid().ToString("N");
        var loop = new EventQueue();
        return (pipe, loop, new IpcServer(IpcServer.OpenFirst(pipe), pipe, loop, Log.Default, control));
    }

    private static async Task<T> Pumping<T>(EventQueue loop, Func<Task<T>> ask)
    {
        var answer = ask();
        await Wait.UntilAsync(() => answer.IsCompleted, "the answer", () => loop.RunDue());
        return await answer;
    }

    [TestMethod]
    public void A_published_instance_is_listed_and_a_clean_close_marks_it_exited()
    {
        var instance = ShellInstance.Open("mine", Program, new Log());
        Assert.IsNull(instance.OpenPipe());
        instance.Publish();

        var row = Assert.ContainsSingle(Rows(instance));
        Assert.IsTrue(row.Alive);
        Assert.AreEqual(("mine", Program, instance.Pipe), (row.Info.ShellId, row.Info.Program, row.Info.Pipe));
        Assert.AreEqual(Environment.ProcessId, row.Info.Pid);

        instance.Close();

        row = Assert.ContainsSingle(Rows(instance));
        Assert.IsFalse(row.Alive);
        Assert.IsNotNull(row.Info.ExitedAt);
        IpcServer.OpenFirst(instance.Pipe).Dispose();
    }

    [TestMethod]
    public void A_listed_instance_has_its_log_on_disk()
    {
        var log = new Log();
        var instance = ShellInstance.Open("mine", Program, log);
        Assert.IsNull(instance.OpenPipe());
        log.Info("shell", "Shell ID: \"mine\"");

        instance.Publish();

        using var reader = new StreamReader(new FileStream(instance.LogPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete));
        Assert.Contains("Shell ID: \"mine\"", reader.ReadToEnd(), StringComparison.Ordinal);
        instance.Close();
    }

    [TestMethod]
    public void A_start_that_is_abandoned_leaves_nothing_behind()
    {
        var instance = ShellInstance.Open("mine", Program, new Log());
        Assert.IsNull(instance.OpenPipe());

        instance.Abandon();

        Assert.IsFalse(Directory.Exists(ShellLocations.Current.Instances.DirOf(instance.Id)));
        Assert.IsEmpty(Rows(instance));
        IpcServer.OpenFirst(instance.Pipe).Dispose();
    }

    [TestMethod]
    public void A_start_whose_name_is_taken_listens_on_another()
    {
        var instance = ShellInstance.Open("mine", Program, new Log());
        using var held = IpcServer.OpenFirst(instance.Pipe);
        var taken = instance.Pipe;

        Assert.IsNull(instance.OpenPipe());

        Assert.AreNotEqual(taken, instance.Pipe);
        instance.Close();
    }

    [TestMethod]
    public void A_secret_in_a_log_line_is_taken_out_of_the_file_and_of_the_crash_reports_that_copy_it()
    {
        var log = new Log();
        var instance = ShellInstance.Open("mine", Program, log);
        log.Warn("shell", "sync --password SENTINEL-PASSWORD");
        log.Write(LogLevel.Warn, "", "password = SENTINEL-NO-CATEGORY");
        log.Error("network",
            "fetch https://example.com/feed?apikey=SENTINEL-QUERY failed\n    continued: token=SENTINEL-NEXT-LINE");
        string text;
        using (var stream = new FileStream(instance.LogPath, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            text = reader.ReadToEnd();
        }

        var report = File.ReadAllText(instance.Crashes.Write("a window", new InvalidOperationException("x"))!.Path);
        instance.Abandon();

        Assert.Contains("sync --password [redacted]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL", text, StringComparison.Ordinal);
        Assert.Contains(" WARN shell: sync --password [redacted]", report, StringComparison.Ordinal);
        Assert.Contains("ERROR network: fetch https://example.com/feed?[redacted] failed", report,
            StringComparison.Ordinal);
        Assert.Contains("    continued: token=[redacted]", report, StringComparison.Ordinal);
        Assert.Contains(" WARN: password = [redacted]", report, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL", report, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_crash_writes_one_fatal_report_that_names_the_shell_and_keeps_the_next_start_from_calling_it_clean()
    {
        var instance = ShellInstance.Open("mine", Program, new Log());
        var error = new InvalidOperationException("SENTINEL crash");
        var before = File.Exists(ShellInstance.ExitState) ? File.ReadAllText(ShellInstance.ExitState) : null;

        instance.ReportFatal(error);
        instance.ReportFatal(error);
        instance.MarkCleanExit();
        instance.Close();

        var reports = Directory.GetFiles(ShellLocations.Current.CrashRoot).Select(File.ReadAllText)
            .Where(r => r.Contains("SENTINEL crash", StringComparison.Ordinal)).ToList();
        var report = Assert.ContainsSingle(reports);
        Assert.Contains("Winshell closed: yes", report, StringComparison.Ordinal);
        Assert.Contains("Shell id: mine" + Environment.NewLine, report, StringComparison.Ordinal);
        Assert.Contains($"Config: {Program}" + Environment.NewLine, report, StringComparison.Ordinal);
        Assert.AreEqual(before, File.Exists(ShellInstance.ExitState) ? File.ReadAllText(ShellInstance.ExitState) : null);
    }

    [TestMethod]
    public async Task A_kill_reaches_a_shell_whatever_its_program_is_called()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var response = await Pumping(loop, () => Ask(pipe, IpcRequest.ForKill()));

        Assert.IsTrue(response.Ok);
        await Wait.UntilAsync(() => control.Quits == 1, "the quit", () => loop.RunDue());
    }

    [TestMethod]
    public async Task A_yield_reaches_a_shell_and_then_quits_it()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var response = await Pumping(loop, () => Ask(pipe, IpcRequest.ForYield()));

        Assert.IsTrue(response.Ok);
        Assert.AreEqual(1, control.Yields);
        await Wait.UntilAsync(() => control.Quits == 1, "the quit after the yield", () => loop.RunDue());
    }

    [TestMethod]
    public async Task A_reload_that_worked_quits_the_shell_once_the_client_has_its_answer()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var response = await Pumping(loop, () => Ask(pipe, IpcRequest.ForReload()));

        Assert.IsTrue(response.Ok);
        await Wait.UntilAsync(() => control.Quits == 1, "the quit after the reload", () => loop.RunDue());
    }

    [TestMethod]
    public async Task A_reload_that_failed_says_why_and_the_shell_stays()
    {
        var control = new FakeInstanceControl { ReloadError = "It did not start." };
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var response = await Pumping(loop, () => Ask(pipe, IpcRequest.ForReload()));

        Assert.AreEqual((false, "It did not start."), (response.Ok, response.Error));
        loop.RunDue();
        Assert.AreEqual(0, control.Quits);
    }

    [TestMethod]
    public async Task A_build_error_is_handed_to_the_shell_with_its_project()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var response = await Pumping(loop,
            () => Ask(pipe, IpcRequest.ForBuild("Bar.cs(1,1): error CS1002: ; expected", @"C:\mine\Bar.csproj")));
        var refused = await Pumping(loop, () => Ask(pipe, new IpcRequest(IpcOps.Build, Args: ["only one"])));

        Assert.IsTrue(response.Ok);
        Assert.AreEqual(("Bar.cs(1,1): error CS1002: ; expected", @"C:\mine\Bar.csproj"),
            (control.BuildError, control.BuildProject));
        Assert.AreEqual((false, "build takes the error and the project file."), (refused.Ok, refused.Error));
        Assert.AreEqual(0, control.Quits);
    }

    [TestMethod]
    public async Task What_throws_on_the_shell_thread_is_a_failed_response_whichever_request_it_was()
    {
        var control = new FakeInstanceControl { InspectFailure = new InvalidOperationException("no screen") };
        control.Ipc.Handle("tests", "fail", new Action(() => throw new InvalidOperationException("no")));
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var called = await Pumping(loop, () => Ask(pipe, IpcRequest.ForCall("tests", "fail", [])));
        var inspected = await Pumping(loop, () => Ask(pipe, IpcRequest.ForInspect("tree", [])));
        var listened = await Pumping(loop, () => Ask(pipe, IpcRequest.ForListen("tests", "gone")));

        Assert.AreEqual((false, "ipc: InvalidOperationException: no"), (called.Ok, called.Error));
        Assert.AreEqual((false, "ipc: InvalidOperationException: no screen"), (inspected.Ok, inspected.Error));
        Assert.AreEqual((false, "Signal not found: tests.gone."), (listened.Ok, listened.Error));
    }

    [TestMethod]
    public async Task A_client_with_no_protocol_number_is_served_and_reads_past_the_shells()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var line = await Pumping(loop, () => AskRaw(pipe, """{"op":"show"}"""));

        var response = JsonSerializer.Deserialize(line!, IpcJson.Default.IpcResponse)!;
        Assert.IsTrue(response.Ok);
        Assert.AreEqual(IpcProtocol.Current, response.Protocol);
    }

    [TestMethod]
    public async Task A_client_from_a_newer_protocol_is_refused_in_a_plain_failed_response()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;

        var line = await AskRaw(pipe, """{"op":"kill","protocol":99}""");

        var response = JsonSerializer.Deserialize(line!, IpcJson.Default.IpcResponse)!;
        Assert.IsFalse(response.Ok);
        Assert.AreEqual(IpcProtocol.ClientRefusal(99), response.Error);
        Assert.Contains("this winshell speaks 99", response.Error!, StringComparison.Ordinal);
        loop.RunDue();
        Assert.AreEqual(0, control.Quits);
    }

    [TestMethod]
    public async Task A_shell_with_no_protocol_number_still_answers_the_client()
    {
        var pipe = "winshell-test-" + Guid.NewGuid().ToString("N");
        await using var shell = IpcServer.OpenFirst(pipe);
        var answering = AnswerOnce(shell, """{"ok":true,"value":"old","error":null}""");

        var response = await Ask(pipe, IpcRequest.ForShow(false));

        Assert.AreEqual((true, "old"), (response.Ok, response.Value));
        var request = JsonSerializer.Deserialize(await answering, IpcJson.Default.IpcRequest)!;
        Assert.AreEqual(IpcProtocol.Current, request.Protocol);
        Assert.AreEqual(ProductVersion.Current.Text, request.Winshell);
    }

    [TestMethod]
    public async Task A_client_of_another_release_is_refused_before_the_shell_acts()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";

        var line = await AskRaw(pipe, $$"""{"op":"reload","winshell":"{{other}}"}""");

        var response = JsonSerializer.Deserialize(line!, IpcJson.Default.IpcResponse)!;
        Assert.IsFalse(response.Ok);
        Assert.AreEqual(IpcProtocol.ReleaseRefusal(ProductVersion.Current.Text, other), response.Error);
        Assert.Contains("Use the winshell that matches", response.Error!, StringComparison.Ordinal);
        loop.RunDue();
        Assert.AreEqual(0, control.Reloads);
    }

    [TestMethod]
    public async Task A_client_of_another_release_can_still_stop_the_shell()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";

        var line = await Pumping(loop, () => AskRaw(pipe, $$"""{"op":"kill","winshell":"{{other}}"}"""));

        Assert.IsTrue(JsonSerializer.Deserialize(line!, IpcJson.Default.IpcResponse)!.Ok);
        loop.RunDue();
        Assert.AreEqual(1, control.Quits);
    }

    [TestMethod]
    public async Task A_client_of_another_release_can_still_yield_the_shell()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";

        var line = await Pumping(loop, () => AskRaw(pipe, $$"""{"op":"yield","winshell":"{{other}}"}"""));

        Assert.IsTrue(JsonSerializer.Deserialize(line!, IpcJson.Default.IpcResponse)!.Ok);
        Assert.AreEqual(1, control.Yields);
        await Wait.UntilAsync(() => control.Quits == 1, "the quit after the yield", () => loop.RunDue());
    }

    [TestMethod]
    public async Task A_shell_of_another_release_can_still_be_stopped_from_here()
    {
        var pipe = "winshell-test-" + Guid.NewGuid().ToString("N");
        await using var shell = IpcServer.OpenFirst(pipe);
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";
        var answering = AnswerOnce(shell, $$"""{"ok":true,"winshell":"{{other}}"}""");

        await Ask(pipe, IpcRequest.ForKill());

        var version = JsonSerializer.Serialize(ProductVersion.Current.Text);
        Assert.AreEqual(
            $$"""{"op":"kill","target":null,"name":null,"args":null,"protocol":{{IpcProtocol.Current}},"winshell":{{version}}}""",
            await answering);
    }

    [TestMethod]
    public async Task A_client_of_this_release_is_served_and_one_with_no_release_still_is()
    {
        var control = new FakeInstanceControl();
        var (pipe, loop, server) = Serve(control);
        using var _ = server;
        var version = JsonSerializer.Serialize(ProductVersion.Current.Text);

        var same = await Pumping(loop, () => AskRaw(pipe, $$"""{"op":"show","winshell":{{version}}}"""));
        var named = JsonSerializer.Deserialize(same!, IpcJson.Default.IpcResponse)!;
        Assert.IsTrue(named.Ok);
        Assert.AreEqual(ProductVersion.Current.Text, named.Winshell);

        var old = await Pumping(loop, () => AskRaw(pipe, """{"op":"show"}"""));
        Assert.IsTrue(JsonSerializer.Deserialize(old!, IpcJson.Default.IpcResponse)!.Ok);
    }

    [TestMethod]
    public async Task A_shell_of_another_release_is_refused_with_a_plain_message()
    {
        var pipe = "winshell-test-" + Guid.NewGuid().ToString("N");
        await using var shell = IpcServer.OpenFirst(pipe);
        var other = ProductVersion.Current.Major == 9 ? "1.0.0" : "9.9.9";
        var answering = AnswerOnce(shell, $$"""{"ok":true,"winshell":"{{other}}"}""");

        IpcReply? reply = null;
        await foreach (var next in IpcClient.Request(pipe, IpcRequest.ForShow(false),
                           CurrentTest.Cancellation))
        {
            reply = next;
            break;
        }

        var failure = Assert.IsExactInstanceOfType<IpcFailure>(reply?.Value);
        Assert.AreEqual(IpcProtocol.ReleaseRefusal(other, ProductVersion.Current.Text), failure.Reason);
        Assert.IsTrue(failure.Rejected);
        await answering;
    }

    private static async Task<string> AnswerOnce(NamedPipeServerStream shell, string answer)
    {
        await shell.WaitForConnectionAsync(CurrentTest.Cancellation);
        var reader = new StreamReader(shell, Encoding.UTF8, leaveOpen: true);
        var writer = new StreamWriter(shell, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var line = await reader.ReadLineAsync(CurrentTest.Cancellation);
        await writer.WriteLineAsync(answer);
        return line!;
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(1, null)]
    [DataRow(2, "This shell speaks control protocol 1 and this winshell speaks 2.")]
    [DataRow(-1, "This shell speaks control protocol 1 and this winshell speaks -1.")]
    public void A_shell_refuses_a_client_outside_what_it_speaks(int client, string? refusal)
    {
        AssertStarts(refusal, IpcProtocol.ClientRefusal(client));
    }

    [TestMethod]
    [DataRow(0, null)]
    [DataRow(5, null)]
    [DataRow(-1, "This shell speaks control protocol -1 and this winshell speaks 1.")]
    public void A_client_refuses_only_a_shell_older_than_it_speaks(int shell, string? refusal)
    {
        AssertStarts(refusal, IpcProtocol.ShellRefusal(shell));
    }

    [TestMethod]
    [DataRow(null, "1.2.3", null)]
    [DataRow("1.2.3", "", null)]
    [DataRow("1.2.3-dev", "1.2.3+abc", null)]
    [DataRow("1.2.3", "1.2.4", "This shell is Winshell 1.2.3 and this winshell is 1.2.4.")]
    [DataRow("2.0.0", "1.9.9", "This shell is Winshell 2.0.0 and this winshell is 1.9.9.")]
    public void A_different_release_is_refused_and_a_missing_one_is_not(string? shell, string? client, string? refusal)
    {
        AssertStarts(refusal, IpcProtocol.ReleaseRefusal(shell, client));
    }

    private static void AssertStarts(string? expected, string? actual)
    {
        if (expected is null)
            Assert.IsNull(actual);
        else
            Assert.StartsWith(expected, actual, StringComparison.Ordinal);
    }
}
