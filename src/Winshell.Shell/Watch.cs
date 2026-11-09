using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Winshell;

/// <summary>Keeps a control current with what the services hold.</summary>
public static class WatchExtensions
{
    /// <summary>
    /// Runs <paramref name="block"/> now, and again whenever a service property or list it read changes, while
    /// <paramref name="control"/> is loaded. Several changes in one turn run it once, on the control's thread, and a
    /// change the block makes itself does not run it again. The watch pauses while the control is unloaded, runs again
    /// when the control loads, and stops for good when the control's shell window is disposed. A block that throws is
    /// logged under <c>watch</c> and runs again on the next change to what it read before the throw. Nothing needs
    /// disposing.
    /// </summary>
    /// <param name="control">The control the block keeps current.</param>
    /// <param name="block">Reads the services and sets the control from them.</param>
    public static void Watch(this FrameworkElement control, Action block)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(block);
        _ = new ControlWatch(control, block);
    }
}

/// <summary>
/// One <see cref="WatchExtensions.Watch"/>. It follows what its block read only while its control is loaded, so a
/// control that is never shown again holds nothing, and it ends with its control's window.
/// </summary>
internal sealed class ControlWatch
{
    private readonly FrameworkElement _control;
    private readonly Action _block;
    private readonly DispatcherQueue _queue;
    private Reads _following = Reads.None;
    private ShellWindow? _window;
    private bool _loaded;
    private bool _running;
    private bool _stopped;
    private int _queued;

    public ControlWatch(FrameworkElement control, Action block)
    {
        _control = control;
        _block = block;
        _queue = control.DispatcherQueue;
        control.Loaded += OnLoaded;
        control.Unloaded += OnUnloaded;
        if (control.IsLoaded)
            OnLoaded(control, null);
        else
            Run();
    }

    private void OnLoaded(object sender, RoutedEventArgs? e)
    {
        if (_stopped)
            return;
        _loaded = true;
        _window?.Disposing -= Stop;
        _window = ShellWindow.Of(_control);
        _window?.Disposing += Stop;
        Run();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // WinUI can raise a moved control's Loaded in its new place before the Unloaded from its old one.
        if (!_control.IsLoaded)
            Pause();
    }

    /// <summary>Stops following until the control loads again.</summary>
    private void Pause()
    {
        _loaded = false;
        _window?.Disposing -= Stop;
        _window = null;
        Follow(Reads.None);
    }

    /// <summary>
    /// Stops for good: the control's window is disposed. Its content may unload only later, so this does not wait for
    /// that, and a later load does not start the watch again.
    /// </summary>
    private void Stop()
    {
        _stopped = true;
        _control.Loaded -= OnLoaded;
        _control.Unloaded -= OnUnloaded;
        Pause();
    }

    private void Run()
    {
        var reads = new Reads();
        _running = true;
        try
        {
            reads.Record(_block);
        }
#pragma warning disable CA1031 // One broken block must not end the shell.
        catch (Exception e)
#pragma warning restore CA1031
        {
            Shell.Current.Log.Error("watch", e.ToString());
        }
        finally
        {
            _running = false;
        }

        if (_loaded)
            Follow(reads);
    }

    /// <summary>Follows what <paramref name="reads"/> holds, and stops following anything else.</summary>
    private void Follow(Reads reads)
    {
        var before = _following;
        // What is followed is current before any handler is added, since adding one can raise a change.
        _following = reads;
        foreach (var source in reads.Properties.Keys)
        {
            if (!before.Properties.ContainsKey(source))
                source.PropertyChanged += OnPropertyChanged;
        }

        foreach (var list in reads.Lists)
        {
            if (!before.Lists.Contains(list))
                list.CollectionChanged += OnCollectionChanged;
        }

        // A source read again keeps its handler, so a service that reads Windows only while something listens goes on.
        foreach (var source in before.Properties.Keys)
        {
            if (!reads.Properties.ContainsKey(source))
                source.PropertyChanged -= OnPropertyChanged;
        }

        foreach (var list in before.Lists)
        {
            if (!reads.Lists.Contains(list))
                list.CollectionChanged -= OnCollectionChanged;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is INotifyPropertyChanged source && _following.Properties.TryGetValue(source, out var names) &&
            (string.IsNullOrEmpty(e.PropertyName) || names.Contains(e.PropertyName)))
            Changed();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Changed();
    }

    private void Changed()
    {
        if (_running && _queue.HasThreadAccess)
            return;
        if (Interlocked.Exchange(ref _queued, 1) == 0)
            _queue.TryEnqueue(RunQueued);
    }

    private void RunQueued()
    {
        _queued = 0;
        if (_loaded)
            Run();
    }
}
