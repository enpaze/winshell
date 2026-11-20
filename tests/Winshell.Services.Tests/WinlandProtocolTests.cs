using System.Text;
using System.Text.Json;
using Winshell.Winland;

namespace Winshell.Services.Tests;

[TestClass]
public class WinlandProtocolTests
{
    [TestMethod]
    public void Pipe_names_follow_the_session_unless_overridden()
    {
        Assert.AreSequenceEqual(["winland-1", "winland"], WinlandProtocol.PipeNames(null, 1));
        Assert.AreSequenceEqual(["custom"], WinlandProtocol.PipeNames(" custom ", 1));
    }

    [TestMethod]
    public void Requests_use_Winland_s_get_verbs_and_ask_for_json()
    {
        Assert.AreSequenceEqual(
            ["j/events", "j/get monitors", "j/get workspaces", "j/get windows", "j/get window", "j/get files"],
            [
                WinlandProtocol.EventsRequest, WinlandProtocol.MonitorsRequest, WinlandProtocol.WorkspacesRequest,
                WinlandProtocol.WindowsRequest, WinlandProtocol.ActiveWindowRequest, WinlandProtocol.FilesRequest,
            ]);
    }

    [TestMethod]
    [DataRow("""{"event":"configreloaded"}""", "configreloaded")]
    [DataRow("""{"event":"urgent","address":"0x20"}""", "urgent")]
    public void An_event_line_is_its_name_and_json(string line, string name)
    {
        var parsed = WinlandProtocol.ParseEvent(line)!;
        Assert.AreEqual(name, parsed.Name);
        Assert.AreEqual(name, parsed.Data.GetProperty("event").GetString());
    }

    [TestMethod]
    [DataRow("workspace>>3,3,\\\\.\\DISPLAY1")]
    [DataRow("""{"address":"0x20"}""")]
    [DataRow("""["urgent"]""")]
    [DataRow("")]
    public void A_line_that_is_not_a_json_event_is_none(string line)
    {
        Assert.IsNull(WinlandProtocol.ParseEvent(line));
    }

    [TestMethod]
    [DataRow("0x708ba", 0x708baUL)]
    [DataRow("708ba", 0x708baUL)]
    [DataRow("", 0UL)]
    [DataRow("nope", 0UL)]
    public void Addresses_read_with_or_without_0x(string text, ulong expected)
    {
        Assert.AreEqual(expected, WinlandProtocol.ParseAddress(text));
    }

    [TestMethod]
    public void Unnumbered_workspaces_get_the_ids_Winland_gives_them_in_events()
    {
        var ids = WinlandProtocol.UnnumberedIds(
            ["1", "special:special", "special:b", "special:a", "zeta", "alpha", "2"]);
        Assert.AreEqual(-99, ids["special:special"]);
        Assert.AreEqual(-98, ids["special:a"]);
        Assert.AreEqual(-97, ids["special:b"]);
        Assert.AreEqual(-1, ids["alpha"]);
        Assert.AreEqual(-2, ids["zeta"]);
        Assert.IsFalse(ids.ContainsKey("1"));
    }

    [TestMethod]
    [DataRow("4", 4)]
    [DataRow("special:special", -99)]
    [DataRow("special:scratch", -1)]
    public void A_workspace_known_by_name_has_its_number_as_id(string name, int id)
    {
        Assert.AreEqual(id, WinlandProtocol.IdFromName(name));
    }

    [TestMethod]
    public async Task A_short_reply_is_one_line()
    {
        var reader = new WinlandReader(Stream("ok\n"));
        Assert.AreEqual("ok", await reader.ReadReplyAsync(CurrentTest.Cancellation));
    }

    [TestMethod]
    public async Task A_reply_with_newlines_is_framed_by_its_length()
    {
        var body = "1\n\twindows: 2\nü";
        var reader = new WinlandReader(Stream($"#{Encoding.UTF8.GetByteCount(body)}\n{body}"));
        Assert.AreEqual(body, await reader.ReadReplyAsync(CurrentTest.Cancellation));
    }

    [TestMethod]
    public async Task The_event_stream_reads_line_by_line()
    {
        var reader = new WinlandReader(Stream("openwindow>>a,1,M,c,t\r\nclosewindow>>a\nlast"));
        Assert.AreEqual("openwindow>>a,1,M,c,t", await reader.ReadLineAsync(CurrentTest.Cancellation));
        Assert.AreEqual("closewindow>>a", await reader.ReadLineAsync(CurrentTest.Cancellation));
        Assert.AreEqual("last", await reader.ReadLineAsync(CurrentTest.Cancellation));
        Assert.IsNull(await reader.ReadLineAsync(CurrentTest.Cancellation));
    }

    [TestMethod]
    public void An_event_line_is_one_typed_event()
    {
        Assert.IsExactInstanceOfType<WinlandProtocol.ConfigReloaded>(Event("""{"event":"configreloaded"}"""));

        var added = Assert.IsExactInstanceOfType<WinlandProtocol.MonitorAdded>(
            Event("""{"event":"monitoradded","id":1,"name":"\\\\.\\DISPLAY1","description":"Main, panel"}"""));
        Assert.AreEqual((1, @"\\.\DISPLAY1", "Main, panel"), (added.Id, added.Name, added.Description));
        Assert.AreEqual(@"\\.\DISPLAY2", Assert.IsExactInstanceOfType<WinlandProtocol.MonitorRemoved>(
            Event("""{"event":"monitorremoved","id":2,"name":"\\\\.\\DISPLAY2","description":""}""")).Name);

        var created = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceCreated>(
            Event("""{"event":"createworkspace","workspace":{"name":"notes"},"monitor":"\\\\.\\DISPLAY1"}"""));
        Assert.AreEqual((-1, "notes", @"\\.\DISPLAY1"), (created.Id, created.Name, created.Monitor));
        var destroyed = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceDestroyed>(
            Event("""{"event":"destroyworkspace","workspace":{"id":7,"name":"7"},"monitor":"\\\\.\\DISPLAY1"}"""));
        Assert.AreEqual((7, "7", @"\\.\DISPLAY1"), (destroyed.Id, destroyed.Name, destroyed.Monitor));

        var focused = Assert.IsExactInstanceOfType<WinlandProtocol.Focused>(Event(
            """{"event":"focus","monitor":"\\\\.\\DISPLAY2","workspace":{"id":3,"name":"3"},"window":{"address":"0x7e089e","title":"Epic, Games","class":"UnrealWindow","exe":"a.exe"}}"""));
        Assert.AreEqual((@"\\.\DISPLAY2", "3", 3, 0x7e089eUL), (focused.Monitor, focused.Workspace, focused.WorkspaceId, focused.Window));
        var bare = Assert.IsExactInstanceOfType<WinlandProtocol.Focused>(
            Event("""{"event":"focus","monitor":"\\\\.\\DISPLAY1","workspace":null,"window":null}"""));
        Assert.AreEqual((@"\\.\DISPLAY1", (string?)null, 0UL), (bare.Monitor, bare.Workspace, bare.Window));

        var shown = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceShown>(
            Event("""{"event":"workspace","workspace":{"id":3,"name":"3"},"monitor":"\\\\.\\DISPLAY1"}"""));
        Assert.AreEqual((3, "3", @"\\.\DISPLAY1"), (shown.Id, shown.Name, shown.Monitor));
        var moved = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceMoved>(
            Event("""{"event":"moveworkspace","workspace":{"name":"special:special"},"monitor":"\\\\.\\DISPLAY2"}"""));
        Assert.AreEqual((-99, "special:special", @"\\.\DISPLAY2"), (moved.Id, moved.Name, moved.Monitor));

        Assert.IsTrue(Assert.IsExactInstanceOfType<WinlandProtocol.FullscreenChanged>(Event("""{"event":"fullscreen","fullscreen":true}""")).Fullscreen);
        Assert.IsFalse(Assert.IsExactInstanceOfType<WinlandProtocol.FullscreenChanged>(Event("""{"event":"fullscreen","fullscreen":false}""")).Fullscreen);

        var opened = Assert.IsExactInstanceOfType<WinlandProtocol.WindowOpened>(Event(
            """{"event":"openwindow","address":"0x30","workspace":{"id":1,"name":"1"},"monitor":"\\\\.\\DISPLAY1","class":"App, Inc","title":"Title, with comma"}"""));
        Assert.AreEqual((0x30UL, "1", @"\\.\DISPLAY1", "App, Inc", "Title, with comma"),
            (opened.Address, opened.Workspace, opened.Monitor, opened.WindowClass, opened.Title));
        var relocated = Assert.IsExactInstanceOfType<WinlandProtocol.WindowMoved>(
            Event("""{"event":"movewindow","address":"0x20","workspace":{"id":3,"name":"3"},"monitor":"\\\\.\\DISPLAY2"}"""));
        Assert.AreEqual((0x20UL, 3, "3", @"\\.\DISPLAY2"),
            (relocated.Address, relocated.WorkspaceId, relocated.Workspace, relocated.Monitor));
        var titled = Assert.IsExactInstanceOfType<WinlandProtocol.WindowTitleChanged>(
            Event("""{"event":"windowtitle","address":"0x10","title":"New, title"}"""));
        Assert.AreEqual((0x10UL, "New, title"), (titled.Address, titled.Title));
        Assert.AreEqual(0x30UL, Assert.IsExactInstanceOfType<WinlandProtocol.WindowClosed>(Event("""{"event":"closewindow","address":"0x30"}""")).Address);
        Assert.AreEqual(0x20UL, Assert.IsExactInstanceOfType<WinlandProtocol.WindowUrgent>(Event("""{"event":"urgent","address":"0x20"}""")).Address);

        Assert.IsNull(Event("""{"event":"workspacev2","workspace":{"id":3,"name":"3"}}"""));
        Assert.IsNull(Event("""{"event":"focusedmon","monitor":"\\\\.\\DISPLAY1"}"""));
    }

    [TestMethod]
    public void A_reply_that_is_not_json_is_none()
    {
        Assert.IsNull(WinlandProtocol.ParseJson(null));
        Assert.IsNull(WinlandProtocol.ParseJson("  "));
        Assert.IsNull(WinlandProtocol.ParseJson("ok"));
        var parsed = WinlandProtocol.ParseJson("""{"id":1}""");
        Assert.IsTrue(parsed.HasValue);
        Assert.AreEqual(1, parsed.GetValueOrDefault().GetProperty("id").GetInt32());
    }

    private static WinlandProtocol.Event? Event(string line)
    {
        return WinlandProtocol.ReadEvent(WinlandProtocol.ParseEvent(line)!);
    }

    [TestMethod]
    public void A_missing_number_reads_as_null()
    {
        using var doc = JsonDocument.Parse("""{"address":"","type":"","name":"","monitor":""}""");
        Assert.IsNull(WinlandProtocol.Int(doc.RootElement, "id"));
    }

    private static MemoryStream Stream(string text)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(text));
    }
}
