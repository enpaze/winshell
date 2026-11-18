using Winshell.Windows;

namespace FeatureMap;

internal sealed class Launched : ObservableObject
{
    private const string App = "Notepad";

    private readonly Shell _shell;
    private readonly Launcher _launcher;
    private HashSet<AppWindow>? _before;
    private string _opened = "nothing";

    public Launched(Shell shell, Launcher launcher)
    {
        _shell = shell;
        _launcher = launcher;
        launcher.PropertyChanged += (_, _) =>
        {
            if (launcher.Visible)
                _before = [.. shell.WindowList.Windows];
            Changed(nameof(Text));
        };
        shell.WindowList.Windows.CollectionChanged += (_, _) => Look();
    }

    public string Text => $"launcher: hotkey {_launcher.Hotkey}; visible: {_launcher.Visible}; opened {Read(_opened)}";

    private void Look()
    {
        if (_before is null || _shell.Apps.Find(App) is not { } app)
            return;
        foreach (var window in _shell.WindowList.Windows.ToList())
        {
            if (_before.Contains(window) || !Shows(window, app.AppId, app.Target))
                continue;
            _before.Add(window);
            window.Close();
            _opened = $"{App}, whose window it closed";
            Changed(nameof(Text));
        }
    }

    private static bool Shows(AppWindow window, string appId, string target)
    {
        return (appId.Length > 0 && window.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase)) ||
               (target.Length > 0 && Path.GetFileName(window.Executable)
                   .Equals(Path.GetFileName(target), StringComparison.OrdinalIgnoreCase));
    }
}
