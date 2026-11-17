using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell.ShellTests;

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
