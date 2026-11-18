using Winshell.Windows;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class VirtualDesktopsServiceTests
{
    private readonly ManualEventLoop _loop = new();

    private VirtualDesktopsService Start(FakeDesktops source)
    {
        return new VirtualDesktopsService(_loop, new Log(), source);
    }

    [TestMethod]
    public void The_list_names_desktops_and_marks_the_current_one()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var source = FakeDesktops.Holding(second, first);
        source.Items.Add(new DesktopRecord(second, "Work"));
        using var desktops = Start(source);

        Assert.IsTrue(desktops.Supported);
        Assert.AreEqual("", desktops.UnsupportedReason);
        Assert.AreSequenceEqual(["Desktop 1", "Work"], desktops.Desktops.Select(desktop => desktop.Name));
        Assert.AreSequenceEqual([0, 1], desktops.Desktops.Select(desktop => desktop.Index));
        Assert.AreSequenceEqual([false, true], desktops.Desktops.Select(desktop => desktop.Current));
        Assert.AreSame(desktops.Desktops[1], desktops.Current);
        Assert.AreSame(desktops.Desktops[1], desktops.Find(second.ToString("D")));
    }

    [TestMethod]
    public void Switch_create_and_remove_change_the_list_and_the_last_desktop_stays()
    {
        var first = Guid.NewGuid();
        var source = FakeDesktops.Holding(first, first);
        using var desktops = Start(source);

        desktops.Remove(desktops.Current);
        Assert.DoesNotContain(call => call.StartsWith("remove:", StringComparison.Ordinal), source.Calls);
        Assert.ContainsSingle(desktops.Desktops);

        var created = desktops.Create();
        Assert.IsNotNull(created);
        Assert.AreEqual(2, desktops.Desktops.Count);
        Assert.AreEqual("Desktop 2", created.Name);

        desktops.SwitchTo(created);
        Assert.AreSame(created, desktops.Current);
        Assert.IsTrue(created.Current);
        Assert.IsFalse(desktops.Desktops[0].Current);

        desktops.Remove(created);
        Assert.ContainsSingle(desktops.Desktops);
        Assert.AreEqual(first, source.Current);
        Assert.Contains($"remove:{created.Id}:{first:D}", source.Calls);
    }

    [TestMethod]
    public void A_desktop_that_stays_is_the_same_object_and_moves_with_the_list()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var source = FakeDesktops.Holding(first, first, second);
        using var desktops = Start(source);
        var kept = desktops.Desktops[1];

        source.Items.RemoveAt(0);
        source.Current = second;
        _loop.Advance(VirtualDesktopsService.RereadInterval);

        Assert.AreSame(kept, Assert.ContainsSingle(desktops.Desktops));
        Assert.AreEqual(0, kept.Index);
        Assert.AreEqual("Desktop 1", kept.Name);
        Assert.AreSame(kept, desktops.Current);
    }

    [TestMethod]
    public void A_desktop_added_or_removed_elsewhere_shows_up_and_nothing_else_reads_again()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var source = FakeDesktops.Holding(first, first);
        using var desktops = Start(source);
        var reads = source.Calls.Count;

        _loop.Advance(VirtualDesktopsService.RereadInterval);
        Assert.AreEqual(reads, source.Calls.Count);

        source.Items.Add(new DesktopRecord(second, "Extra"));
        _loop.Advance(VirtualDesktopsService.RereadInterval);
        Assert.AreEqual("Extra", desktops.Desktops[1].Name);

        source.Items.RemoveAll(item => item.Id == second);
        _loop.Advance(VirtualDesktopsService.RereadInterval);
        Assert.ContainsSingle(desktops.Desktops);
        Assert.DoesNotContain(call => call is not "read", source.Calls);
    }

    [TestMethod]
    public void A_switch_Windows_reports_reads_the_list_once_on_the_next_turn()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var source = FakeDesktops.Holding(first, first, second);
        using var desktops = Start(source);
        var reads = source.Calls.Count;

        source.Current = second;
        source.Switch();
        source.Switch();
        Assert.AreSame(desktops.Desktops[0], desktops.Current);

        _loop.Process();

        Assert.AreSame(desktops.Desktops[1], desktops.Current);
        Assert.IsTrue(desktops.Desktops[1].Current);
        Assert.AreEqual(reads + 1, source.Calls.Count);

        source.Current = first;
        source.Switch();
        _loop.Process();

        Assert.AreSame(desktops.Desktops[0], desktops.Current);
        Assert.AreEqual(reads + 2, source.Calls.Count);
    }

    [TestMethod]
    public void An_unknown_build_does_not_read_or_change_desktops_or_poll()
    {
        var source = FakeDesktops.Holding(Guid.NewGuid());
        source.Contract = DesktopContract.ForBuild(22631);
        using var desktops = Start(source);

        Assert.IsFalse(desktops.Supported);
        Assert.Contains("22631", desktops.UnsupportedReason, StringComparison.Ordinal);
        Assert.IsEmpty(desktops.Desktops);
        Assert.IsNull(desktops.Current);
        Assert.IsNull(_loop.NextDue);

        Assert.IsNull(desktops.Create());
        desktops.SwitchTo(new VirtualDesktop("00112233-4455-6677-8899-aabbccddeeff"));
        desktops.Remove(new VirtualDesktop("00112233-4455-6677-8899-aabbccddeeff"));
        Assert.IsEmpty(source.Calls);
    }

    [TestMethod]
    public void A_failed_read_blocks_changes_until_a_read_succeeds()
    {
        var id = Guid.NewGuid();
        var source = FakeDesktops.Holding(id, id);
        source.FailRead = true;
        using var desktops = Start(source);

        Assert.IsFalse(desktops.Supported);
        Assert.StartsWith("failed:", desktops.UnsupportedReason, StringComparison.Ordinal);
        Assert.IsNull(desktops.Create());
        Assert.DoesNotContain(call => call.StartsWith("create:", StringComparison.Ordinal), source.Calls);

        source.FailRead = false;
        desktops.Create();

        Assert.IsTrue(desktops.Supported);
        Assert.AreEqual("", desktops.UnsupportedReason);
        Assert.AreEqual(2, desktops.Desktops.Count);
    }

    [TestMethod]
    public void A_failed_read_after_a_good_one_keeps_the_list()
    {
        var id = Guid.NewGuid();
        var source = FakeDesktops.Holding(id, id);
        using var desktops = Start(source);

        source.FailRead = true;
        desktops.SwitchTo(desktops.Current);

        Assert.IsTrue(desktops.Supported);
        Assert.ContainsSingle(desktops.Desktops);
    }

    [TestMethod]
    public void A_disposed_service_stops_reading()
    {
        var id = Guid.NewGuid();
        var source = FakeDesktops.Holding(id, id);
        var desktops = Start(source);
        var reads = source.Calls.Count;

        desktops.Dispose();
        source.Items.Clear();
        source.Switch();
        _loop.Advance(VirtualDesktopsService.RereadInterval);

        Assert.AreEqual(reads, source.Calls.Count);
        Assert.IsNull(_loop.NextDue);
    }
}
