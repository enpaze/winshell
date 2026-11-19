using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls;

namespace Winshell.ShellTests;

public sealed class StyleHostTests
{
    [Fact]
    public async Task A_new_style_replaces_the_one_an_element_shows_after_its_wrapper_is_collected()
    {
        var sizes = await ShellFixture.On(_ =>
        {
            var panel = new StackPanel();
            var first = Styled(panel, new ShellStyle { FontSize = 12 });
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(first.IsAlive, "The element's first wrapper outlived a collection, so this test proves nothing.");

            var again = (Border)panel.Children[0];
            StyleHost.ShowAlert(again, new ShellStyle { FontSize = 20 });
            return again.Resources.MergedDictionaries.Select(dictionary => (double)dictionary["ShellFontSize"]).ToArray();
        });

        Assert.Equal([20d], sizes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Styled(StackPanel panel, ShellStyle style)
    {
        var border = new Border();
        panel.Children.Add(border);
        StyleHost.ShowAlert(border, style);
        return new WeakReference(border);
    }
}
