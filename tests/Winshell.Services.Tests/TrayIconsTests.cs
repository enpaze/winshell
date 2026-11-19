using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class TrayIconsTests
{
    private const string Program = @"C:\Apps\sync.exe";
    private readonly TrayIcons _icons = new(_ => (null, null), _ => (42, Program));

    private static NotifyIconCall Call(uint message, uint flags = 0, nint window = 0x100, uint id = 1,
        Guid guid = default)
    {
        return new NotifyIconCall { Message = message, Flags = flags, Window = window, Id = id, Guid = guid };
    }

    [TestMethod]
    public void An_icon_added_by_guid_is_the_same_icon_when_an_app_names_it_by_its_window()
    {
        var added = new List<TrayIcon>();
        _icons.Added += added.Add;
        var guid = Guid.NewGuid();

        _icons.Apply(Call(TrayProtocol.Add, TrayProtocol.NIF_GUID, guid: guid));
        _icons.Apply(Call(TrayProtocol.SetVersion) with { TimeoutOrVersion = TrayProtocol.Version4 });
        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_TIP) with { Tip = "Synced" });

        var icon = Assert.ContainsSingle(_icons.Icons);
        Assert.AreSame(icon, Assert.ContainsSingle(added));
        Assert.AreEqual(TrayProtocol.Version4, icon.Version);
        Assert.AreEqual("Synced", icon.Tip);
    }

    [TestMethod]
    public void A_deleted_icon_is_gone_and_a_change_to_it_adds_nothing()
    {
        var removed = new List<TrayIcon>();
        _icons.Removed += removed.Add;
        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_TIP) with { Tip = "Never added" });
        Assert.IsEmpty(_icons.Icons);

        _icons.Apply(Call(TrayProtocol.Add));
        var icon = Assert.ContainsSingle(_icons.Icons);
        _icons.Apply(Call(TrayProtocol.Delete));
        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_TIP) with { Tip = "After delete" });

        Assert.IsEmpty(_icons.Icons);
        Assert.AreSame(icon, Assert.ContainsSingle(removed));
    }

    [TestMethod]
    public void Hidden_changes_only_when_the_state_mask_names_it()
    {
        _icons.Apply(Call(TrayProtocol.Add, TrayProtocol.NIF_STATE) with
        {
            State = TrayProtocol.NIS_HIDDEN,
            StateMask = TrayProtocol.NIS_HIDDEN
        });
        var icon = Assert.ContainsSingle(_icons.Icons);
        Assert.IsTrue(icon.Hidden);

        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_STATE) with { State = 0, StateMask = 0 });
        Assert.IsTrue(icon.Hidden);

        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_STATE) with
        {
            State = 0,
            StateMask = TrayProtocol.NIS_HIDDEN
        });
        Assert.IsFalse(icon.Hidden);
    }

    [TestMethod]
    public void A_balloon_needs_its_text()
    {
        var balloons = new List<(string Title, string Text, uint Flags)>();
        _icons.Balloon += (_, title, text, flags) => balloons.Add((title, text, flags));
        _icons.Apply(Call(TrayProtocol.Add));

        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_INFO) with
        {
            InfoTitle = "Backup",
            Info = "Done",
            InfoFlags = 1
        });
        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_INFO) with { InfoTitle = "Backup", Info = "" });

        Assert.AreEqual(("Backup", "Done", 1u), Assert.ContainsSingle(balloons));
    }

    [TestMethod]
    public void Windows_settings_move_icons_and_only_a_moved_icon_changes()
    {
        _icons.Apply(Call(TrayProtocol.Add, id: 1));
        _icons.Apply(Call(TrayProtocol.Add, id: 2));
        var changed = new List<uint>();
        _icons.Changed += icon => changed.Add(icon.Id);
        foreach (var icon in _icons.Icons)
            Assert.IsTrue(icon.Overflow);

        List<TrayIconSetting> promoted = [new(Guid.Empty, Program, 1, true)];
        _icons.UseSettings(promoted);
        _icons.UseSettings(promoted);

        Assert.AreSequenceEqual([1u], changed);
        Assert.IsFalse(_icons.Icons.Single(icon => icon.Id == 1).Overflow);
        Assert.IsTrue(_icons.Icons.Single(icon => icon.Id == 2).Overflow);
    }

    [TestMethod]
    public void Only_nim_setversion_sets_the_version()
    {
        _icons.Apply(Call(TrayProtocol.Add, TrayProtocol.NIF_INFO) with
        {
            Info = "Hi",
            TimeoutOrVersion = TrayProtocol.Version4
        });
        var icon = Assert.ContainsSingle(_icons.Icons);
        Assert.AreEqual(0u, icon.Version);

        _icons.Apply(Call(TrayProtocol.SetVersion) with { TimeoutOrVersion = TrayProtocol.Version4 });
        Assert.AreEqual(TrayProtocol.Version4, icon.Version);

        _icons.Apply(Call(TrayProtocol.Modify, TrayProtocol.NIF_INFO) with
        {
            Info = "Again",
            TimeoutOrVersion = TrayProtocol.Version
        });
        Assert.AreEqual(TrayProtocol.Version4, icon.Version);
    }
}
