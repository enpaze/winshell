using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Winshell.Capture;
using Winshell.Windows;

namespace FeatureMap;

public sealed class Capture : Component
{
    public override Element Render()
    {
        var shell = Shell.Current;
        var windows = UseObservableTree(shell.WindowList);
        var own = windows.Windows.FirstOrDefault(Own);
        var session = UseMemo(() => new CaptureSession(shell.Log));
        UseObservable(session);
        UseEffect(() => session.Dispose, session);
        UseEffect(() =>
        {
            if (own is null || session.Running)
                return;
            session.Target = CaptureTarget.ForWindow(own);
            session.Start();
        }, own);
        var capture = session.HasFrame
            ? $"capture view: running, frame {session.PixelSize.Width}x{session.PixelSize.Height}"
            : $"capture view: {(session.Running ? "running" : "stopped")}, no frame{(session.Error.Length > 0 ? ", " + session.Error : "")}";
        var thumbnail = own is null ? "window thumbnail: no window" : "window thumbnail: previews this shell's window";
        return HStack(8,
            XamlHost(() => new CaptureView { Session = session, Width = 64, Height = 48 },
                view => Label(view, "block.capture-view", capture), "capture"),
            XamlHost(() => new WindowThumbnail { Width = 64, Height = 48 },
                view =>
                {
                    ((WindowThumbnail)view).Window = own;
                    Label(view, "block.window-thumbnail", thumbnail);
                }, "thumbnail"));
    }

    private static bool Own(AppWindow window)
    {
        return string.Equals(Path.GetFileName(window.Executable), Path.GetFileName(Environment.ProcessPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void Label(FrameworkElement view, string id, string text)
    {
        AutomationProperties.SetAutomationId(view, id);
        AutomationProperties.SetName(view, text);
    }
}
