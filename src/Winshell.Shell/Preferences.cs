using System.ComponentModel;

namespace Winshell;

/// <summary>
/// One section of the shell's preferences file, <c>preferences\&lt;shell id&gt;.toml</c> in the config folder, as an
/// object. A component's section is named after the component, as in <c>[bar]</c>. A type made with no name holds the
/// configuration's own preferences, the keys before the first section. A type derives from this and has one public
/// read-write property per key, as <see cref="TomlFile"/> describes, and a default belongs in the property's
/// initializer.
/// <code>
/// public sealed class ClockPreferences(string name) : Preferences(name)
/// {
///     [TomlComment("Show 13:00 rather than 1:00 PM.")]
///     public bool Clock24 { get => Read(field); set => Set(ref field, value); }
/// }
/// </code>
/// The section is read in the constructor, and after that the object follows the file: a save changes only the
/// properties whose keys changed. A key the file does not have holds its default. Assigning a property writes it into
/// the section, on the shell's thread, and leaves the rest of the file as it is. When the file is missing, it is
/// written with each section's keys at their defaults, commented out, under their
/// <see cref="TomlCommentAttribute"/>. Several objects may read one section. Make one on the shell's thread and
/// dispose it when its component goes.
/// </summary>
public abstract class Preferences : ObservableObject, IDisposable
{
    private readonly PreferencesFile _file;
    private bool _disposed;

    /// <param name="name">The section: the component's name, or <c>""</c> for the configuration's own keys.</param>
    /// <param name="check">
    /// Called with a key and the value read for it. It returns why the value is refused, or <c>""</c>. A refused value
    /// is reported and left in the file, and the one in use stays.
    /// </param>
    protected Preferences(string name = "", Func<string, object, string>? check = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Section = name;
        _file = Shell.Current.PreferencesFile;
        Keys = new TomlKeys(this, check, _file.Warn);
        Defaults = Keys.All.ToDictionary(key => key, key => key.Value(this));
        PropertyChanged += OnPropertyChanged;
        _file.Add(this);
    }

    /// <summary>The section's name, or <c>""</c> for the keys before the first section.</summary>
    internal string Section { get; }

    internal TomlKeys Keys { get; }

    /// <summary>What each key held when this was made, which it goes back to when the file does not have it.</summary>
    internal IReadOnlyDictionary<TomlKey, object?> Defaults { get; }

    /// <summary>Keys assigned since the file was last read and the write they are waiting for.</summary>
    internal HashSet<TomlKey> Assigned { get; } = [];

    /// <summary>The value a missing file is written with for <paramref name="key"/>, commented out, or null for none.</summary>
    internal virtual object? Commented(TomlKey key)
    {
        return Defaults[key];
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Keys.Loading || _disposed || Keys.Of(e.PropertyName) is not { } key)
            return;
        Assigned.Add(key);
        _file.PostWrite();
    }

    /// <summary>Stops following the file. A write of this section that was on its way runs now.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _file.Remove(this);
        GC.SuppressFinalize(this);
    }
}
