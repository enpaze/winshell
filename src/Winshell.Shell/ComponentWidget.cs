using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>One placed component widget. A new placement renders this instance again instead of making another.</summary>
internal abstract class ComponentWidgetHost : UserControl
{
    internal abstract void Show(WidgetContext context, string area);
}

internal sealed class ComponentWidgetHost<TWidget> : ComponentWidgetHost, IDisposable
    where TWidget : Component<WidgetContext>, new()
{
    private readonly ReactorHostControl _host = new();
    private readonly WidgetHost _root;
    private readonly string _widget;
    private string _area;
    private string? _lastLoggedError;
    private bool _disposed;

    public ComponentWidgetHost(WidgetContext context, string widget, string area)
    {
        _widget = widget;
        _area = area;
        _root = new WidgetHost(context);
        _host.RenderErrorHandler = ShowError;
        _host.Mount(_root);
        Content = _host;
    }

    internal override void Show(WidgetContext context, string area)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _area = area;
        _lastLoggedError = null;
        _root.Show(context);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _host.Dispose();
    }

    private Element? ShowError(RenderError error)
    {
        if (error.Source == RenderErrorSource.Cleanup)
            return null;
        var text = error.Exception.ToString();
        if (_lastLoggedError != text)
        {
            _lastLoggedError = text;
            Shell.Current.Log.Error("widgets", $"Could not render {_widget} in area '{_area}': {error.Exception}");
        }

        return TextBlock($"{_widget}: {error.Exception.Message}");
    }

    /// <summary>
    /// The host's root. It renders the author's component with the current props, and renders again when
    /// <see cref="Show"/> changes them. The author's instance stays.
    /// </summary>
    private sealed class WidgetHost : Component
    {
        private readonly Revision _revision = new();
        private WidgetContext _context;

        public WidgetHost(WidgetContext context) => _context = context;

        public void Show(WidgetContext context)
        {
            _context = context;
            _revision.Bump();
        }

        public override Element Render()
        {
            _ = UseObservable(_revision);
            return Component<TWidget, WidgetContext>(_context);
        }
    }

    /// <summary>A change of this object asks the host component to render again.</summary>
    private sealed class Revision : ObservableObject
    {
        private int _version;

        public void Bump() => Set(ref _version, _version + 1);
    }
}
