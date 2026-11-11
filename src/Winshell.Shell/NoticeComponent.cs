using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;

namespace Winshell;

/// <summary>What the notice shows, and the two things a person can do with it.</summary>
internal sealed record NoticeProps(Notice Notice, Action Act, Action Dismiss);

/// <summary>The words, the dismiss button and the button that opens a file.</summary>
internal sealed class NoticeComponent : Component<NoticeProps>
{
    /// <summary>How wide the notice is, as a Windows 11 notification is.</summary>
    internal const double Width = 364;

    public override Element Render()
    {
        var notice = Props.Notice;
        var announcement = string.Join(". ",
            new[] { notice.Title, notice.Detail, notice.Body, notice.Note }.Where(text => text.Length > 0));
        var lines = new List<Element>
        {
            Grid(
                [GridSize.Star()],
                [GridSize.Auto],
                CaptionLine("Winshell").Grid(0, 0),
                Button(Icon(FontIcon("\uE711", fontSize: 10)), () => Props.Dismiss())
                    .Width(32)
                    .Height(24)
                    .Padding(0)
                    .HAlign(HorizontalAlignment.Right)
                    .AutomationName("Dismiss")
                    .Grid(0, 0))
            .Margin(0, 0, 0, 8),
            TitleLine(notice.Title)
        };
        if (notice.Detail.Length > 0)
            lines.Add(CaptionLine(notice.Detail));
        lines.Add(BodyLine(notice.Body));
        lines.Add(CaptionLine(notice.Note));
        lines.Add(Button(notice.ActionText, () => Props.Act())
            .AccentButton()
            .HAlign(HorizontalAlignment.Stretch)
            .Margin(0, 8, 0, 0));
        return Grid(
                [GridSize.Star()],
                [GridSize.Auto],
                VStack(4, lines.ToArray()).Grid(0, 0))
            .Padding(16)
            .AutomationName(announcement)
            .LiveRegion(AutomationLiveSetting.Assertive)
            .OnKeyDown((_, e) =>
            {
                if (e.Key != VirtualKey.Escape)
                    return;
                e.Handled = true;
                Props.Dismiss();
            })
            .OnMount(element =>
            {
                // The peer can announce once the notice is in the tree.
                void Announce() => FrameworkElementAutomationPeer.CreatePeerForElement(element)?.RaiseNotificationEvent(
                    AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantAll, announcement,
                    "WinshellNotice");

                if (element.IsLoaded)
                    Announce();
                else
                    element.Loaded += (_, _) => Announce();
            });
    }

    private static Element CaptionLine(string text)
    {
        return Caption(text).Opacity(0.8).TextWrapping(TextWrapping.WrapWholeWords);
    }

    private static Element TitleLine(string text)
    {
        return BodyStrong(text).TextWrapping(TextWrapping.WrapWholeWords);
    }

    private static Element BodyLine(string text)
    {
        return Body(text).TextWrapping(TextWrapping.WrapWholeWords);
    }
}
