using System.Collections.Immutable;

namespace Winshell;

internal sealed class Listeners<THandler>
{
    private ImmutableArray<Listener> _all = [];

    public IDisposable Add(THandler handler, Action<Exception> failed)
    {
        var listener = new Listener(this, handler, failed);
        ImmutableInterlocked.Update(ref _all, static (all, added) => all.Add(added), listener);
        return listener;
    }

    public void Raise<TArg>(TArg arg, Action<THandler, TArg> call)
    {
        foreach (var listener in _all)
            try
            {
                call(listener.Handler, arg);
            }
            catch (Exception e)
            {
                listener.Failed(e);
            }
    }

    private sealed class Listener(Listeners<THandler> owner, THandler handler, Action<Exception> failed) : IDisposable
    {
        public THandler Handler => handler;

        public Action<Exception> Failed => failed;

        public void Dispose()
        {
            ImmutableInterlocked.Update(ref owner._all, static (all, removed) => all.Remove(removed), this);
        }
    }
}
