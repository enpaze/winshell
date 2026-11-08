using Microsoft.UI.Xaml.Controls;
using Winshell.Diagnostics;

namespace Winshell.ShellTests;

/// <summary>A real panel window. Fullscreen stepping and placement after release also have fake-backend tests.</summary>
public sealed class PanelEdgeTests
{
    [Fact]
    public async Task The_doctor_knows_a_panel_for_Winshell_s_own()
    {
        var classes = await ShellFixture.On(_ =>
        {
            using var panel = new PanelWindow();
            panel.Anchors = Edges.Top | Edges.Left;
            panel.Width = 320;
            panel.Height = 60;
            panel.Content = new Grid();
            panel.Visible = true;
            return DoctorProbe.EdgeWindows().Select(window => window.ClassName).ToList();
        });

        Assert.Contains(DoctorScreens.WinshellClass, classes);
    }
}
