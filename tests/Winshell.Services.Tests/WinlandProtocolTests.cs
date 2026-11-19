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
    [DataRow("workspacev2>>3,3,\\\\.\\DISPLAY1", "workspacev2", "3,3,\\\\.\\DISPLAY1")]
    [DataRow("configreloaded>>", "configreloaded", "")]
    [DataRow("activewindow>>Chrome_WidgetWin_1,a >> b", "activewindow", "Chrome_WidgetWin_1,a >> b")]
    [DataRow("noarrows", "noarrows", "")]
    public void Event_lines_split_at_the_first_arrows(string line, string name, string data)
    {
        Assert.AreEqual((name, data), WinlandProtocol.SplitEvent(line));
    }

    [TestMethod]
    public void The_last_argument_keeps_its_commas()
    {
        Assert.AreSequenceEqual(["708ba", "1", "\\\\.\\DISPLAY1", "Chrome_WidgetWin_1", "a, b, c"],
            WinlandProtocol.ParseArgs("708ba,1,\\\\.\\DISPLAY1,Chrome_WidgetWin_1,a, b, c", 5));
    }

    [TestMethod]
    public void Missing_arguments_are_empty()
    {
        Assert.AreSequenceEqual(["a", "", ""], WinlandProtocol.ParseArgs("a", 3));
        Assert.AreSequenceEqual(["", ""], WinlandProtocol.ParseArgs("", 2));
        Assert.AreSequenceEqual(["", "", "DISPLAY1"], WinlandProtocol.ParseArgs(",,DISPLAY1", 3));
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
        Assert.IsExactInstanceOfType<WinlandProtocol.ConfigReloaded>(Event("configreloaded>>"));

        var added = Assert.IsExactInstanceOfType<WinlandProtocol.MonitorAdded>(Event(@"monitoraddedv2>>1,\\.\DISPLAY1,Main, panel"));
        Assert.AreEqual((1, @"\\.\DISPLAY1", "Main, panel"), (added.Id, added.Name, added.Description));
        Assert.AreEqual(@"\\.\DISPLAY2", Assert.IsExactInstanceOfType<WinlandProtocol.MonitorRemoved>(Event(@"monitorremoved>>\\.\DISPLAY2")).Name);

        var created = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceCreated>(Event(@"createworkspacev2>>,notes,\\.\DISPLAY1"));
        Assert.AreEqual((-1, "notes", @"\\.\DISPLAY1"), (created.Id, created.Name, created.Monitor));
        var destroyed = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceDestroyed>(Event(@"destroyworkspacev2>>7,7,\\.\DISPLAY1"));
        Assert.AreEqual((7, "7", @"\\.\DISPLAY1"), (destroyed.Id, destroyed.Name, destroyed.Monitor));

        var focused = Assert.IsExactInstanceOfType<WinlandProtocol.MonitorFocused>(Event(@"focusedmon>>\\.\DISPLAY2,3"));
        Assert.AreEqual((@"\\.\DISPLAY2", "3"), (focused.Monitor, focused.Workspace));
        Assert.IsNull(Assert.IsExactInstanceOfType<WinlandProtocol.MonitorFocused>(Event(@"focusedmon>>\\.\DISPLAY1,?")).Workspace);

        var shown = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceShown>(Event(@"workspacev2>>3,3,\\.\DISPLAY1"));
        Assert.AreEqual((3, "3", @"\\.\DISPLAY1"), (shown.Id, shown.Name, shown.Monitor));
        var moved = Assert.IsExactInstanceOfType<WinlandProtocol.WorkspaceMoved>(Event(@"moveworkspacev2>>,special:special,\\.\DISPLAY2"));
        Assert.AreEqual((-99, "special:special", @"\\.\DISPLAY2"), (moved.Id, moved.Name, moved.Monitor));

        Assert.IsTrue(Assert.IsExactInstanceOfType<WinlandProtocol.FullscreenChanged>(Event("fullscreen>>1")).Fullscreen);
        Assert.IsFalse(Assert.IsExactInstanceOfType<WinlandProtocol.FullscreenChanged>(Event("fullscreen>>0")).Fullscreen);

        var opened = Assert.IsExactInstanceOfType<WinlandProtocol.WindowOpened>(Event(@"openwindow>>30,1,\\.\DISPLAY1,App,Title, with comma"));
        Assert.AreEqual((0x30UL, "1", @"\\.\DISPLAY1", "App", "Title, with comma"),
            (opened.Address, opened.Workspace, opened.Monitor, opened.WindowClass, opened.Title));
        var relocated = Assert.IsExactInstanceOfType<WinlandProtocol.WindowMoved>(Event(@"movewindowv2>>20,3,3,\\.\DISPLAY2"));
        Assert.AreEqual((0x20UL, 3, "3", @"\\.\DISPLAY2"),
            (relocated.Address, relocated.WorkspaceId, relocated.Workspace, relocated.Monitor));
        var titled = Assert.IsExactInstanceOfType<WinlandProtocol.WindowTitleChanged>(Event("windowtitlev2>>10,New, title"));
        Assert.AreEqual((0x10UL, "New, title"), (titled.Address, titled.Title));
        Assert.AreEqual(0x30UL, Assert.IsExactInstanceOfType<WinlandProtocol.WindowClosed>(Event("closewindow>>30")).Address);
        Assert.AreEqual(0x30UL, Assert.IsExactInstanceOfType<WinlandProtocol.WindowActivated>(Event("activewindowv2>>30")).Address);
        Assert.AreEqual(0x20UL, Assert.IsExactInstanceOfType<WinlandProtocol.WindowUrgent>(Event("urgent>>20")).Address);

        Assert.IsNull(Event("activewindow>>30"));
        Assert.IsNull(Event("noarrows"));
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
        var (name, data) = WinlandProtocol.SplitEvent(line);
        return WinlandProtocol.ReadEvent(name, data);
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
