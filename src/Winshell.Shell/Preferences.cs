using System.ComponentModel;

namespace Winshell;

public abstract class Preferences : ObservableObject, IDisposable
{
    private readonly PreferencesFile _file;
    private bool _disposed;

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

    internal string Section { get; }

    internal TomlKeys Keys { get; }

    internal IReadOnlyDictionary<TomlKey, object?> Defaults { get; }

    internal HashSet<TomlKey> Assigned { get; } = [];

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

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _file.Remove(this);
        GC.SuppressFinalize(this);
    }
}
