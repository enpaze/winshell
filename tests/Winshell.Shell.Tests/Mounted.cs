using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

/// <summary>The props a mounted component is rendering, and the bump that asks it to render them again.</summary>
internal sealed class PropsBox<T>
{
    public T Value = default!;

    public Action<Func<int, int>>? Bump { get; set; }

    public void Set(T value)
    {
        Value = value;
        Bump?.Invoke(pulse => pulse + 1);
    }
}

/// <summary>
/// A host showing <typeparamref name="TComponent"/> with props a test can replace. Mounting starts the tree at once,
/// so a test that never opens a window still renders, and one that does can put <see cref="Host"/> in the window.
/// </summary>
internal sealed class Mounted<TComponent, TProps> : IDisposable
    where TComponent : Component<TProps>, new()
{
    private readonly PropsBox<TProps> _box = new();
    private bool _disposed;

    public Mounted(TProps props)
    {
        _box.Value = props;
        Props = props;
        Host = new ReactorHostControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            IsTabStop = false
        };
        Host.Mount(ctx =>
        {
            var (_, bump) = ctx.UseReducer(0);
            _box.Bump = bump;
            return Component<TComponent, TProps>(_box.Value);
        });
    }

    public ReactorHostControl Host { get; }

    public TProps Props { get; private set; }

    public void Set(TProps props)
    {
        Props = props;
        _box.Set(props);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Host.Dispose();
    }
}
