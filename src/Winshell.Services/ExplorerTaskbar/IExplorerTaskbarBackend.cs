namespace Winshell.ExplorerTaskbar;

internal interface IExplorerTaskbarBackend : IDisposable
{
    event Action? TaskbarMayHaveChanged;

    void Hide();

    void Restore();
}
