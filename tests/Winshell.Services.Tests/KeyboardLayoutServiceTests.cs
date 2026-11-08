using System.ComponentModel;
using Winshell.Keyboard;

namespace Winshell.Services.Tests;

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

    [Fact]
    public void Layouts_carry_a_short_label_and_the_name_Windows_shows()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, International, Burmese, Uncoded)
            { CurrentId = International.Id };
        using var keyboard = new KeyboardLayoutService(loop, backend);
        keyboard.PropertyChanged += Ignore;

        Assert.Equal(4, keyboard.Layouts.Count);
        AssertLayout(keyboard.Layouts[0], English.Id, "EN", English.Name);
        AssertLayout(keyboard.Layouts[1], International.Id, "EN", International.Name);
        AssertLayout(keyboard.Layouts[2], Burmese.Id, "MY", Burmese.Name);
        AssertLayout(keyboard.Layouts[3], Uncoded.Id, "ENG", Uncoded.Name);
        Assert.Equal(International.Id, keyboard.Current?.Id);
        Assert.Equal("EN", keyboard.Current?.Label);
        Assert.Equal(International.Name, keyboard.Current?.Name);
    }

    [Fact]
    public void The_current_layout_is_reported_when_it_changes()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, German) { CurrentId = English.Id };
        using var keyboard = new KeyboardLayoutService(loop, backend);
        List<string?> heard = [];
        keyboard.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
        Assert.Equal([nameof(KeyboardLayoutService.Layouts), nameof(KeyboardLayoutService.Current)], heard);
        Assert.Equal(English.Id, keyboard.Current?.Id);

        heard.Clear();
        loop.Advance(Interval);
        Assert.Empty(heard);

        backend.CurrentId = German.Id;
        loop.Advance(Interval);
        Assert.Equal([nameof(KeyboardLayoutService.Current)], heard);
        Assert.Equal(German.Id, keyboard.Current?.Id);
        Assert.Equal("DE", keyboard.Current?.Label);

        heard.Clear();
        backend.Layouts.Add(International);
        loop.Advance(Interval);
        Assert.Equal([nameof(KeyboardLayoutService.Layouts)], heard);
        Assert.Equal(International.Name, keyboard.Layouts[^1].Name);
        Assert.Equal("EN", keyboard.Layouts[^1].Label);
    }

    [Fact]
    public void Windows_is_read_only_while_something_listens()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English) { CurrentId = English.Id };
        using var keyboard = new KeyboardLayoutService(loop, backend);
        PropertyChangedEventHandler second = Ignore;

        Assert.Empty(keyboard.Layouts);
        Assert.Null(keyboard.Current);
        loop.Advance(Interval * 5);
        Assert.Equal(0, backend.Reads);
        Assert.Null(loop.NextDue);

        keyboard.PropertyChanged += Ignore;
        keyboard.PropertyChanged += second;
        Assert.Equal(English.Id, keyboard.Current?.Id);
        loop.Advance(Interval * 2);
        Assert.Equal(3, backend.Reads);

        keyboard.PropertyChanged -= second;
        loop.Advance(Interval);
        Assert.Equal(4, backend.Reads);

        keyboard.PropertyChanged -= Ignore;
        loop.Advance(Interval * 5);
        Assert.Equal(4, backend.Reads);
        Assert.Null(loop.NextDue);
        Assert.Null(keyboard.Current);
        Assert.Empty(keyboard.Layouts);

        keyboard.PropertyChanged += Ignore;
        loop.Advance(Interval);
        Assert.Equal(6, backend.Reads);
        Assert.Equal(English.Id, keyboard.Current?.Id);
    }

    [Fact]
    public void Disposing_stops_the_reading()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, German) { CurrentId = English.Id };
        var keyboard = new KeyboardLayoutService(loop, backend);
        keyboard.PropertyChanged += Ignore;
        Assert.Equal(1, backend.Reads);

        keyboard.Dispose();
        keyboard.PropertyChanged += Ignore;
        loop.Advance(Interval * 3);
        Assert.Equal(1, backend.Reads);
        Assert.Null(loop.NextDue);
        Assert.False(keyboard.Next());
        Assert.Empty(backend.Used);
    }

    [Fact]
    public void Switching_asks_for_the_next_layout_or_a_chosen_one()
    {
        var loop = new ManualEventLoop();
        var backend = new FakeKeyboardBackend(English, International, German) { CurrentId = English.Id };
        using var keyboard = new KeyboardLayoutService(loop, backend);

        Assert.True(keyboard.Next());
        Assert.Equal([International.Id], backend.Used);
        Assert.Null(loop.NextDue);

        backend.CurrentId = International.Id;
        Assert.True(keyboard.Next());
        backend.CurrentId = German.Id;
        Assert.True(keyboard.Next());
        Assert.Equal([International.Id, German.Id, English.Id], backend.Used);

        backend.Layouts.Clear();
        backend.Layouts.Add(English);
        backend.CurrentId = English.Id;
        Assert.False(keyboard.Next());
        Assert.Equal(3, backend.Used.Count);

        backend.Layouts.Add(German);
        backend.CurrentId = "missing";
        Assert.True(keyboard.Next());
        Assert.Equal(English.Id, backend.Used[^1]);

        backend.CurrentId = English.Id;
        Assert.False(keyboard.Use(new KeyboardLayout(English.Id, "EN", English.Name)));
        Assert.True(keyboard.Use(new KeyboardLayout(German.Id, "DE", German.Name)));
        Assert.False(keyboard.Use(new KeyboardLayout("00000809", "EN", "United Kingdom")));
        Assert.Equal(German.Id, backend.Used[^1]);

        var asked = backend.Used.Count;
        backend.Accepts = false;
        backend.CurrentId = English.Id;
        Assert.False(keyboard.Next());
        Assert.Equal(asked + 1, backend.Used.Count);
        Assert.Equal(German.Id, backend.Used[^1]);
        Assert.Null(loop.NextDue);
    }

    private static void AssertLayout(KeyboardLayout layout, string id, string label, string name)
    {
        Assert.Equal(id, layout.Id);
        Assert.Equal(label, layout.Label);
        Assert.Equal(name, layout.Name);
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
