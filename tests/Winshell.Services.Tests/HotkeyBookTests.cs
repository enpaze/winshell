using Winshell.Input;

namespace Winshell.Services.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HotkeyBookTests
{
    [TestMethod]
    public void Parser_uses_windows_names_and_canonical_order()
    {
        var chord = Parse("shift+alt+ctrl+win+k").Chords[0];
        Assert.AreEqual("Win+Ctrl+Alt+Shift+K", chord.Text);
        Assert.AreEqual('K', chord.VirtualKey);
        Assert.AreEqual(0x000F, (int)chord.Modifiers);
        Assert.IsFalse(chord.IsWinOnly);

        Assert.AreEqual("Alt+F4", Parse("alt+f4").Text);
        Assert.AreEqual(0x73, Parse("alt+f4").Chords[0].VirtualKey);
        Assert.AreEqual("Ctrl+K, C", Parse("Ctrl+K, C").Text);
        Assert.AreEqual("Ctrl+Comma, C", Parse("ctrl+comma, c").Text);
        Assert.AreEqual(0xBC, Parse("Comma").Chords[0].VirtualKey);
        Assert.AreEqual(0x87, Parse("F24").Chords[0].VirtualKey);
        Assert.AreEqual(0x2E, Parse("Delete").Chords[0].VirtualKey);
        Assert.AreEqual(0xAF, Parse("Volume Up").Chords[0].VirtualKey);

        var winE = Parse("Win+E").Chords[0];
        Assert.IsFalse(winE.IsWinOnly);
        Assert.AreEqual('E', winE.VirtualKey);
        Assert.AreEqual(0x0008, (int)winE.Modifiers);

        var either = Parse("Win").Chords[0];
        Assert.IsTrue(either.IsWinOnly);
        Assert.AreEqual(WinKeySide.Either, either.Side);
        Assert.AreEqual("Win", either.Text);
        var left = Parse("Left Win").Chords[0];
        Assert.IsTrue(left.IsWinOnly);
        Assert.AreEqual(WinKeySide.Left, left.Side);
        Assert.AreEqual("Left Win", left.Text);

        Assert.IsFalse(HotkeySequence.TryParse("   ", out var idle, out var idleError));
        Assert.IsNull(idle);
        Assert.IsNull(idleError);
        Assert.IsFalse(HotkeySequence.TryParse(null, out _, out idleError));
        Assert.IsNull(idleError);
    }

    [TestMethod]
    public void Parser_rejects_linux_names_sided_modifiers_and_bad_shapes()
    {
        AssertRejected("Super+K", "Super", "Win");
        AssertRejected("Left Ctrl+K", "Left Ctrl", "Ctrl");
        AssertRejected("Right Alt+K", "Left Alt", "Alt");
        AssertRejected("LShift+K", "Left Shift", "Shift");
        AssertRejected("Left Win+E", "Left Win", "Win");
        AssertRejected("Ctrl+Shift", "needs a key");
        AssertRejected("Ctrl+", "Comma");
        AssertRejected("A, B, C, D, E, F, G, H, I", "8");
        AssertRejected("Ctrl+Ctrl+K", "repeated");
    }

    [TestMethod]
    public void Two_listeners_share_a_chord_until_the_last_one_releases_it()
    {
        var book = new HotkeyBook();
        var first = new Probe();
        var second = new Probe();
        var key = Parse("Ctrl+K").Chords[0].OsKey;
        book.Add(first, Parse("Ctrl+K"));
        book.Add(second, Parse("Ctrl+K"));
        var fired = book.Trigger(key, 0);
        Assert.AreEqual(2, fired.Pressed.Count);
        Assert.Contains(first, fired.Pressed);
        Assert.Contains(second, fired.Pressed);
        book.Remove(first);
        Assert.IsTrue(book.HasOs(key));
        book.Remove(second);
        Assert.IsFalse(book.HasOs(key));
    }

    [TestMethod]
    public void Windows_key_tap_fires_that_side_and_either_but_not_the_other_side()
    {
        var book = new HotkeyBook();
        var left = new Probe();
        var right = new Probe();
        var either = new Probe();
        book.Add(left, Parse("Left Win"));
        book.Add(right, Parse("Right Win"));
        book.Add(either, Parse("Win"));

        var down = book.Hook(VirtualKeys.LeftWin, false, HotkeyModifiers.None, 0);
        Assert.IsEmpty(down.Pressed);
        Assert.IsFalse(down.Swallow);

        var up = book.Hook(VirtualKeys.LeftWin, true, HotkeyModifiers.Win, 0);
        Assert.Contains(left, up.Pressed);
        Assert.Contains(either, up.Pressed);
        Assert.DoesNotContain(right, up.Pressed);
        Assert.IsTrue(up.Swallow);
    }

    [TestMethod]
    public void Windows_key_used_as_a_modifier_does_not_fire_and_repeats_do_not_reset_that()
    {
        var book = new HotkeyBook();
        var either = new Probe();
        book.Add(either, Parse("Win"));
        book.Hook(VirtualKeys.LeftWin, false, HotkeyModifiers.None, 0);
        book.Hook('E', false, HotkeyModifiers.Win, 0);
        book.Hook(VirtualKeys.LeftWin, false, HotkeyModifiers.Win, 0);
        var up = book.Hook(VirtualKeys.LeftWin, true, HotkeyModifiers.Win, 0);
        Assert.IsEmpty(up.Pressed);
        Assert.IsFalse(up.Swallow);
    }

    [TestMethod]
    public void Sequence_swallows_a_match_and_a_mismatch_or_modifier_does_not_steal_the_key()
    {
        var book = new HotkeyBook();
        var listener = new Probe();
        book.Add(listener, Parse("Ctrl+K, C"));
        book.Trigger(Parse("Ctrl+K").Chords[0].OsKey, 0);
        var shift = book.Hook(0x10, false, HotkeyModifiers.Shift, 0);
        Assert.IsFalse(shift.Swallow);
        Assert.IsTrue(book.NeedsHook);

        var miss = book.Hook('D', false, HotkeyModifiers.None, 0);
        Assert.IsFalse(miss.Swallow);
        Assert.IsEmpty(miss.Pressed);
        Assert.IsFalse(book.NeedsHook);

        book.Trigger(Parse("Ctrl+K").Chords[0].OsKey, 0);
        var hit = book.Hook('C', false, HotkeyModifiers.None, 0);
        Assert.IsTrue(hit.Swallow);
        Assert.Contains(listener, hit.Pressed);
        Assert.IsFalse(book.NeedsHook);
    }

    [TestMethod]
    public void Extending_a_sequence_keeps_a_new_token_and_the_old_disarm_does_nothing()
    {
        var book = new HotkeyBook();
        var listener = new Probe();
        book.Add(listener, Parse("A, B, C"));
        var started = book.Trigger(Parse("A").Chords[0].OsKey, 0);
        var token = Assert.ContainsSingle(started.Armed).Token;
        book.Hook('B', false, HotkeyModifiers.None, 0);
        book.Disarm(listener, token);
        Assert.IsTrue(book.NeedsHook);
        var done = book.Hook('C', false, HotkeyModifiers.None, 0);
        Assert.Contains(listener, done.Pressed);
    }

    [TestMethod]
    public void A_sequence_expires_when_the_deadline_is_reached()
    {
        var book = new HotkeyBook();
        var listener = new Probe();
        book.Add(listener, Parse("A, B"));
        book.Trigger(Parse("A").Chords[0].OsKey, 0);
        var late = book.Hook('B', false, HotkeyModifiers.None, 1000);
        Assert.IsEmpty(late.Pressed);
        Assert.IsFalse(late.Swallow);
        Assert.IsFalse(book.NeedsHook);
    }

    [TestMethod]
    public void Windows_tap_finishes_a_waiting_sequence_and_still_starts_one()
    {
        var book = new HotkeyBook();
        var starter = new Probe();
        var finisher = new Probe();
        book.Add(starter, Parse("Win, K"));
        book.Add(finisher, Parse("Ctrl+K, Win"));
        book.Trigger(Parse("Ctrl+K").Chords[0].OsKey, 0);
        book.Hook(VirtualKeys.LeftWin, false, HotkeyModifiers.None, 0);
        var up = book.Hook(VirtualKeys.LeftWin, true, HotkeyModifiers.Win, 0);
        Assert.Contains(finisher, up.Pressed);
        Assert.Contains(notice => notice.Listener == starter, up.Armed);
        Assert.IsTrue(up.Swallow);
        Assert.IsTrue(book.NeedsHook);
    }

    [TestMethod]
    public void Ctrl_Alt_Delete_is_not_registered()
    {
        var loop = new ManualEventLoop();
        var registrar = new HotkeyRegistrar(loop, Log.Default);
        using var stopRegistrar = registrar.Stopping();
        using var hotkey = new Hotkey(loop, Log.Default, registrar);
        hotkey.Sequence = "Ctrl+Alt+Delete";

        Assert.IsFalse(hotkey.Active);
        Assert.Contains("Ctrl+Alt+Delete", hotkey.Error, StringComparison.Ordinal);
        if (hotkey.Error.Contains("already in use", StringComparison.Ordinal))
            Assert.IsTrue(hotkey.Taken);
        else
            Assert.IsFalse(hotkey.Taken);
    }

    [TestMethod]
    public void A_second_windows_key_hotkey_waits_until_the_hook_is_free()
    {
        var firstLoop = new ManualEventLoop();
        var secondLoop = new ManualEventLoop();
        var firstRegistrar = new HotkeyRegistrar(firstLoop, Log.Default);
        using var stopFirstRegistrar = firstRegistrar.Stopping();
        var secondRegistrar = new HotkeyRegistrar(secondLoop, Log.Default);
        using var stopSecondRegistrar = secondRegistrar.Stopping();
        var first = new Hotkey(firstLoop, Log.Default, firstRegistrar);
        using var second = new Hotkey(secondLoop, Log.Default, secondRegistrar);
        try
        {
            first.Sequence = "Win";
            Assert.IsTrue(first.Active);

            second.Sequence = "Win";
            Assert.IsFalse(second.Active);
            Assert.IsFalse(second.Taken);
            Assert.Contains("keyboard hook", second.Error, StringComparison.Ordinal);
        }
        finally
        {
            first.Dispose();
        }

        second.Enabled = false;
        second.Enabled = true;
        Assert.IsTrue(second.Active);
        Assert.AreEqual("", second.Error);
    }

    private static void AssertRejected(string text, params string[] parts)
    {
        Assert.IsFalse(HotkeySequence.TryParse(text, out _, out var error));
        Assert.IsNotNull(error);
        foreach (var part in parts)
            Assert.Contains(part, error, StringComparison.Ordinal);
    }

    private static HotkeySequence Parse(string text)
    {
        Assert.IsTrue(HotkeySequence.TryParse(text, out var sequence, out var error), error);
        return sequence!;
    }

    private sealed class Probe : IHotkeyListener
    {
        public void OnPressed()
        {
        }

        public void OnReleased()
        {
        }
    }
}
