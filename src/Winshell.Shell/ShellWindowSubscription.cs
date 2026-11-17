using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace Winshell;

internal sealed class ShellWindowSubscription(PropertyChangedEventHandler changed)
{
    public ShellWindow? Window { get; private set; }

    public void Attach(UIElement element)
    {
        Detach();
        Window = ShellWindow.Of(element);
        if (Window is not null)
            Window.PropertyChanged += changed;
    }

    public void Detach()
    {
        if (Window is null)
            return;
        Window.PropertyChanged -= changed;
        Window = null;
    }
}
