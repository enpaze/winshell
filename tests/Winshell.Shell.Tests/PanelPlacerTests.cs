using Microsoft.UI.Xaml.Controls;
using Winshell.Diagnostics;

namespace Winshell.ShellTests;

[Trait("Category", "Window")]
public sealed class PanelPlacerTests
{
    [Fact]
    public async Task The_doctor_knows_a_panel_for_Winshell_s_own()
    {
        var classes = await ShellFixture.On(shell =>
        {
            using var panel = new PanelWindow();
            panel.Anchors = Edges.Top | Edges.Left;
            panel.Width = 320;
            panel.Height = 60;
            panel.Content = new Grid();
            shell.Add(panel);
            return DoctorProbe.EdgeWindows().Select(window => window.ClassName).ToList();
        });

        Assert.Contains(DoctorScreens.WinshellClass, classes);
    }
}
