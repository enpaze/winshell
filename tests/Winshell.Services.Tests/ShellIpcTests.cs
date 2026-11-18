using System.Runtime.CompilerServices;
using Windows.UI;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class ShellIpcTests
{
    private readonly List<string> _warnings = [];

    private ShellIpc Ipc()
    {
        return new ShellIpc(new Log { Sink = (_, _, message) => _warnings.Add(message) });
    }

    [TestMethod]
    public void A_handler_gets_its_arguments_as_the_types_it_declares()
    {
        var ipc = Ipc();
        (string, int, bool, double, float, Color)? seen = null;
        ipc.Handle("t", "all",
            (string text, int number, bool flag, double wide, float narrow, Color color) =>
            {
                seen = (text, number, flag, wide, narrow, color);
            });

        ipc.Call("t", "all", ["a b", "-7", "1", "2.5", "0.5", "#ff102030"]);

        Assert.AreEqual(("a b", -7, true, 2.5, 0.5f, Color.FromArgb(255, 0x10, 0x20, 0x30)), seen);
    }

    [TestMethod]
    [DataRow("f00", 255, 255, 0, 0)]
    [DataRow("#8f00", 136, 255, 0, 0)]
    [DataRow("#102030", 255, 0x10, 0x20, 0x30)]
    [DataRow("80102030", 0x80, 0x10, 0x20, 0x30)]
    public void A_color_is_hex_with_or_without_its_hash(string text, int a, int r, int g, int b)
    {
        var ipc = Ipc();
        Color? seen = null;
        ipc.Handle("t", "paint", (Color color) => { seen = color; });

        ipc.Call("t", "paint", [text]);

        Assert.AreEqual(Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b), seen);
    }

    [TestMethod]
    public void A_result_comes_back_as_text()
    {
        var ipc = Ipc();
        ipc.Handle("t", "number", () => 1234);
        ipc.Handle("t", "real", () => 1.5);
        ipc.Handle("t", "flag", () => true);
        ipc.Handle("t", "color", () => Color.FromArgb(1, 2, 3, 4));
        ipc.Handle("t", "text", () => (string)null!);
        ipc.Handle("t", "nothing", () => { });

        Assert.AreEqual("1234", ipc.Call("t", "number", []));
        Assert.AreEqual("1.5", ipc.Call("t", "real", []));
        Assert.AreEqual("true", ipc.Call("t", "flag", []));
        Assert.AreEqual("#01020304", ipc.Call("t", "color", []));
        Assert.AreEqual("", ipc.Call("t", "text", []));
        Assert.AreEqual("", ipc.Call("t", "nothing", []));
    }

    [TestMethod]
    [DataRow("0", "1")]
    [DataRow("true", "false")]
    public void A_bool_is_a_word_or_a_number(string on, string other)
    {
        var ipc = Ipc();
        var flags = new List<bool>();
        ipc.Handle("t", "set", flags.Add);

        ipc.Call("t", "set", [on]);
        ipc.Call("t", "set", [other]);

        Assert.AreNotEqual(flags[0], flags[1]);
    }

    [TestMethod]
    public void The_wrong_number_of_arguments_is_refused_and_nothing_runs()
    {
        var ipc = Ipc();
        var ran = false;
        ipc.Handle("t", "one", (string _) => ran = true);

        Assert.AreEqual("Function t.one takes 1 argument, 0 given.",
            Assert.ThrowsExactly<IpcException>(() => ipc.Call("t", "one", [])).Message);
        Assert.AreEqual("Function t.one takes 1 argument, 2 given.",
            Assert.ThrowsExactly<IpcException>(() => ipc.Call("t", "one", ["a", "b"])).Message);
        Assert.IsFalse(ran);
    }

    [TestMethod]
    [DataRow("many", "Argument n is not an int: many.")]
    [DataRow("1.5", "Argument n is not an int: 1.5.")]
    public void Text_that_is_not_the_declared_type_is_refused(string text, string message)
    {
        var ipc = Ipc();
        ipc.Handle("t", "count", (int n) => n);

        Assert.AreEqual(message, Assert.ThrowsExactly<IpcException>(() => ipc.Call("t", "count", [text])).Message);
    }

    [TestMethod]
    public void A_color_that_is_not_hex_is_refused()
    {
        var ipc = Ipc();
        ipc.Handle("t", "paint", (Color color) => { });

        Assert.AreEqual("Argument color is not a color: 12.",
            Assert.ThrowsExactly<IpcException>(() => ipc.Call("t", "paint", ["12"])).Message);
    }

    [TestMethod]
    public void A_handler_with_a_type_ipc_cant_pass_is_refused_when_it_registers()
    {
        Delegate[] unpassable =
        [
            (object _) => { },
            () => new object(),
            (string[] _) => { },
            () => Task.CompletedTask,
            (long _) => { }
        ];

        foreach (var handler in unpassable)
        {
            var error = Assert.ThrowsExactly<ArgumentException>(() => Ipc().Handle("t", "bad", handler));
            Assert.Contains("Function t.bad has a type IPC can't pass.", error.Message, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public void More_than_ten_parameters_is_refused()
    {
        var eleven = (int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k) => { };

        Assert.ThrowsExactly<ArgumentException>(() => Ipc().Handle("t", "bad", eleven));
    }

    [TestMethod]
    public void What_is_missing_is_named_as_an_IpcHandler_names_it()
    {
        var ipc = Ipc();
        ipc.Handle("t", "known", () => { });

        Assert.AreEqual("Target not found: other.",
            Assert.ThrowsExactly<IpcException>(() => ipc.Call("other", "known", [])).Message);
        Assert.AreEqual("Function not found: t.other.",
            Assert.ThrowsExactly<IpcException>(() => ipc.Call("t", "other", [])).Message);
        Assert.AreEqual("Property not found: t.other.",
            Assert.ThrowsExactly<IpcException>(() => ipc.GetProperty("t", "other")).Message);
        Assert.AreEqual("Signal not found: t.other.",
            Assert.ThrowsExactly<IpcException>(() => ipc.Listen("t", "other", _ => { })).Message);
    }

    [TestMethod]
    public void Show_lists_targets_in_order_with_their_declared_types()
    {
        var ipc = Ipc();
        ipc.Handle("settings", "open", (string page, int tab) => true);
        ipc.Property("settings", "page", () => "home");
        ipc.Signal("settings", "changed");
        ipc.Handle("audio", "mute", () => { });

        Assert.AreEqual(
            "target audio\n  function mute(): void\n"
            + "target settings\n  function open(page: string, tab: int): bool\n  property page: string\n  signal changed()\n",
            ipc.Show());
    }

    [TestMethod]
    public void A_property_reads_its_value_each_time()
    {
        var ipc = Ipc();
        var value = new StrongBox<int>(1);
        ipc.Property("t", "value", () => value.Value);

        Assert.AreEqual("1", ipc.GetProperty("t", "value"));
        value.Value = 2;
        Assert.AreEqual("2", ipc.GetProperty("t", "value"));
    }

    [TestMethod]
    public void A_property_of_a_type_ipc_cant_pass_is_refused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Ipc().Property("t", "value", () => new object()));
    }

    [TestMethod]
    public void A_signal_reaches_its_listeners_until_they_leave()
    {
        var ipc = Ipc();
        var signal = ipc.Signal("t", "changed");
        var seen = new List<string>();
        var listening = ipc.Listen("t", "changed", seen.Add);

        signal.Emit("one");
        listening.Dispose();
        signal.Emit("two");

        Assert.AreSequenceEqual(["one"], seen);
    }

    [TestMethod]
    public void A_later_handler_wins_and_says_so_and_the_earlier_ones_removal_leaves_it()
    {
        var ipc = Ipc();
        var earlier = ipc.Handle("t", "name", () => "earlier");
        ipc.Handle("t", "name", () => "later");

        earlier.Dispose();

        Assert.AreEqual("later", ipc.Call("t", "name", []));
        Assert.Contains("Duplicate IPC name \"t.name\"; the later one wins.", _warnings);
    }

    [TestMethod]
    public void A_target_with_nothing_left_is_gone()
    {
        var ipc = Ipc();
        var handler = ipc.Handle("t", "name", () => "x");

        handler.Dispose();

        Assert.IsEmpty(ipc.Describe());
        Assert.ThrowsExactly<IpcException>(() => ipc.Call("t", "name", []));
    }

    [TestMethod]
    public void A_handler_that_throws_fails_with_its_own_exception()
    {
        var ipc = Ipc();
        ipc.Handle("t", "fail", new Action(() => throw new InvalidOperationException("no")));

        Assert.AreEqual("no", Assert.ThrowsExactly<InvalidOperationException>(() => ipc.Call("t", "fail", [])).Message);
    }
}
