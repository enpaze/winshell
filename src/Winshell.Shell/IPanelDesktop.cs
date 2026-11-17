using Winshell.Native;

namespace Winshell;

internal readonly record struct PanelMonitor(string Id, RECT Bounds, RECT Area, double Scale);

internal interface IPanelDesktop
{
    PanelMonitor? Find(string id);

    PanelMonitor? Primary();

    bool FullscreenOn(string screenId);

    bool Register(uint callback);

    RECT Query(AppBarEdge edge, RECT proposed);

    RECT Reserve(AppBarEdge edge, RECT desired);

    void Remove();

    void Move(RECT rect, bool topmost, double scale);

    void WorkAreaMayHaveChanged();

    void Post(Action action);
}
