using System.ComponentModel;
using Winshell.Keyboard;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class KeyboardLayoutServiceTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private static readonly InstalledLayout English = new("00000409", "en", "US");
    private static readonly InstalledLayout International = new("00020409", "en", "United States-International");
    private static readonly InstalledLayout German = new("00000407", "de", "German");
    private static readonly InstalledLayout Burmese = new("00000455", "my-zwg", "Burmese (Zawgyi)");
    private static readonly InstalledLayout Uncoded = new("00000411", "", "English (United States)");

    private static void Ignore(object? sender, PropertyChangedEventArgs e)
    {
    }

    [TestMethod]
    public void Layouts_carry_a_short_label_and_the_name_Windows_shows()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, International, Burmese, Uncoded)
        { CurrentId = International.Id };
        var keyboard = new KeyboardLayoutService(loop, backend);
        using var stopKeyboard = keyboard.Stopping();
        keyboard.PropertyChanged += Ignore;

        Assert.AreEqual(4, keyboard.Layouts.Count);
        AssertLayout(keyboard.Layouts[0], English.Id, "EN", English.Name);
        AssertLayout(keyboard.Layouts[1], International.Id, "EN", International.Name);
        AssertLayout(keyboard.Layouts[2], Burmese.Id, "MY", Burmese.Name);
        AssertLayout(keyboard.Layouts[3], Uncoded.Id, "ENG", Uncoded.Name);
        Assert.AreEqual(International.Id, keyboard.Current?.Id);
        Assert.AreEqual("EN", keyboard.Current?.Label);
        Assert.AreEqual(International.Name, keyboard.Current?.Name);
    }

    [TestMethod]
    public void The_current_layout_is_reported_when_it_changes()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, German) { CurrentId = English.Id };
        var keyboard = new KeyboardLayoutService(loop, backend);
        using var stopKeyboard = keyboard.Stopping();
        List<string?> heard = [];
        keyboard.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        Assert.AreSequenceEqual([nameof(KeyboardLayoutService.Layouts), nameof(KeyboardLayoutService.Current)], heard);
        Assert.AreEqual(English.Id, keyboard.Current?.Id);

        heard.Clear();
        loop.Advance(Interval);
        Assert.IsEmpty(heard);

        backend.CurrentId = German.Id;
        loop.Advance(Interval);
        Assert.AreSequenceEqual([nameof(KeyboardLayoutService.Current)], heard);
        Assert.AreEqual(German.Id, keyboard.Current?.Id);
        Assert.AreEqual("DE", keyboard.Current?.Label);

        heard.Clear();
        backend.Layouts.Add(International);
        loop.Advance(Interval);
        Assert.AreSequenceEqual([nameof(KeyboardLayoutService.Layouts)], heard);
        Assert.AreEqual(International.Name, keyboard.Layouts[^1].Name);
        Assert.AreEqual("EN", keyboard.Layouts[^1].Label);
    }

    [TestMethod]
    public void Windows_is_read_only_while_something_listens()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English) { CurrentId = English.Id };
        var keyboard = new KeyboardLayoutService(loop, backend);
        using var stopKeyboard = keyboard.Stopping();
        PropertyChangedEventHandler second = Ignore;

        Assert.IsEmpty(keyboard.Layouts);
        Assert.IsNull(keyboard.Current);
        loop.Advance(Interval * 5);
        Assert.AreEqual(0, backend.Reads);
        Assert.IsNull(loop.NextDue);

        keyboard.PropertyChanged += Ignore;
        keyboard.PropertyChanged += second;
        Assert.AreEqual(English.Id, keyboard.Current?.Id);
        loop.Advance(Interval * 2);
        Assert.AreEqual(3, backend.Reads);

        keyboard.PropertyChanged -= second;
        loop.Advance(Interval);
        Assert.AreEqual(4, backend.Reads);

        keyboard.PropertyChanged -= Ignore;
        loop.Advance(Interval * 5);
        Assert.AreEqual(4, backend.Reads);
        Assert.IsNull(loop.NextDue);
        Assert.IsNull(keyboard.Current);
        Assert.IsEmpty(keyboard.Layouts);

        keyboard.PropertyChanged += Ignore;
        loop.Advance(Interval);
        Assert.AreEqual(6, backend.Reads);
        Assert.AreEqual(English.Id, keyboard.Current?.Id);
    }

    [TestMethod]
    public void Disposing_stops_the_reading()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, German) { CurrentId = English.Id };
        var keyboard = new KeyboardLayoutService(loop, backend);
        keyboard.PropertyChanged += Ignore;
        Assert.AreEqual(1, backend.Reads);

        keyboard.Stop();
        keyboard.PropertyChanged += Ignore;
        loop.Advance(Interval * 3);
        Assert.AreEqual(1, backend.Reads);
        Assert.IsNull(loop.NextDue);
        Assert.IsFalse(keyboard.Next());
        Assert.IsEmpty(backend.Used);
    }

    [TestMethod]
    public void Switching_asks_for_the_next_layout_or_a_chosen_one()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, International, German) { CurrentId = English.Id };
        var keyboard = new KeyboardLayoutService(loop, backend);
        using var stopKeyboard = keyboard.Stopping();

        Assert.IsTrue(keyboard.Next());
        Assert.AreSequenceEqual([International.Id], backend.Used);
        Assert.IsNull(loop.NextDue);

        backend.CurrentId = International.Id;
        Assert.IsTrue(keyboard.Next());
        backend.CurrentId = German.Id;
        Assert.IsTrue(keyboard.Next());
        Assert.AreSequenceEqual([International.Id, German.Id, English.Id], backend.Used);

        backend.Layouts.Clear();
        backend.Layouts.Add(English);
        backend.CurrentId = English.Id;
        Assert.IsFalse(keyboard.Next());
        Assert.AreEqual(3, backend.Used.Count);

        backend.Layouts.Add(German);
        backend.CurrentId = "missing";
        Assert.IsTrue(keyboard.Next());
        Assert.AreEqual(English.Id, backend.Used[^1]);

        backend.CurrentId = English.Id;
        Assert.IsFalse(keyboard.Use(new KeyboardLayout(English.Id, "EN", English.Name)));
        Assert.IsTrue(keyboard.Use(new KeyboardLayout(German.Id, "DE", German.Name)));
        Assert.IsFalse(keyboard.Use(new KeyboardLayout("00000809", "EN", "United Kingdom")));
        Assert.AreEqual(German.Id, backend.Used[^1]);

        var asked = backend.Used.Count;
        backend.Accepts = false;
        backend.CurrentId = English.Id;
        Assert.IsFalse(keyboard.Next());
        Assert.AreEqual(asked + 1, backend.Used.Count);
        Assert.AreEqual(German.Id, backend.Used[^1]);
        Assert.IsNull(loop.NextDue);
    }

    private static void AssertLayout(KeyboardLayout layout, string id, string label, string name)
    {
        Assert.AreEqual(id, layout.Id);
        Assert.AreEqual(label, layout.Label);
        Assert.AreEqual(name, layout.Name);
    }

    private sealed class FakeKeyboardBackend(params InstalledLayout[] layouts) : IKeyboardLayoutBackend
    {
        public List<InstalledLayout> Layouts { get; } = [.. layouts];

        public string? CurrentId { get; set; }

        public int Reads { get; private set; }

        public List<string> Used { get; } = [];

        public bool Accepts { get; set; } = true;

        public KeyboardSnapshot Read()
        {
            Reads++;
            return new KeyboardSnapshot([.. Layouts], CurrentId);
        }

        public bool Use(string id)
        {
            Used.Add(id);
            return Accepts;
        }
    }
}
