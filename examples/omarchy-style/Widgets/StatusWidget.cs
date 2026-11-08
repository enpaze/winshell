using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winshell;

namespace OmarchyStyle;

/// <summary>
/// One status glyph. It uses the <c>PanelButton</c> style, whose font and foreground are theme resources, and opens a
/// Windows Settings page. The glyph is drawn once the button is in the bar, which is when its screen can be read.
/// </summary>
public abstract class StatusWidget : Button, IDisposable
{
    private readonly string _page;
    private Watcher? _watcher;
    private Watcher? _reading;
    private bool _disposed;

    protected StatusWidget(WidgetContext context, string page)
    {
        Shell = context.Shell;
        _page = page;
        Click += (_, _) => Bar.OpenSettings(_page);
        Loaded += (_, _) =>
        {
            if (_disposed)
                return;
            Style = Bar.Resource<Style>(this, "PanelButton");
            _watcher?.Dispose();
            _watcher = new Watcher(Render);
        };
    }

    protected Shell Shell { get; }

    /// <summary>Returns <paramref name="source"/>, and draws this glyph again when it changes.</summary>
    protected T Read<T>(T source) where T : INotifyPropertyChanged => _reading!.Read(source);

    protected abstract void Show();

    public void Dispose()
    {
        _disposed = true;
        _watcher?.Dispose();
        _watcher = null;
        GC.SuppressFinalize(this);
    }

    private void Render(Watcher watch)
    {
        _reading = watch;
        Show();
    }
}
