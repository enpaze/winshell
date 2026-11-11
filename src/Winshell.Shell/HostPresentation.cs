using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.Win32.Graphics.Dwm;
using MARGINS = Windows.Win32.UI.Controls.MARGINS;
using WinRT;
using Winshell.Native;

namespace Winshell;

/// <summary>
/// What a native window shows, and the keyboard that reaches it: a XAML island whose background stays transparent, over
/// a material, both following the shell's theme. The content's root exists before the native window; the island goes in
/// once every part of the window exists.
/// </summary>
internal sealed partial class HostPresentation(HostWindow host, ShellPlatform platform) : IDisposable
{
    private readonly WindowKindDescription _kind = WindowKindDescription.Of(host.Kind);
    private readonly Grid _root = new() { Background = null };
    private DesktopWindowXamlSource? _xaml;
    private SystemBackdropConfiguration? _backdropConfig;
    private ISystemBackdropControllerWithTargets? _backdropController;
    private WindowCorners _corners;
    private bool _fullscreen;

    private nint Handle => host.Handle;

    /// <summary>What the window shows. Its background stays transparent, so the material shows through.</summary>
    public UIElement? Content
    {
        get => _root.Children.Count > 0 ? _root.Children[0] : null;
        set
        {
            _root.Children.Clear();
            if (value is not null)
                _root.Children.Add(value);
        }
    }

    public XamlRoot? XamlRoot => _root.XamlRoot;

    /// <summary>
    /// Readies the native window, which exists now, for the material: its frame, and for a panel or popup no fade, peek,
    /// rounding or border. Then puts the content in it and follows the theme from then on.
    /// </summary>
    public void Attach()
    {
        // The material shows wherever the content is transparent.
        var margins = new MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        PInvoke.DwmExtendFrameIntoClientArea((HWND)Handle, margins);
        if (_kind.ReplacesSystemFrame)
        {
            // Panels and popups appear without the Windows fade, as layer surfaces do.
            SetDwm(DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, 1);
            SetDwm(DWMWINDOWATTRIBUTE.DWMWA_EXCLUDED_FROM_PEEK, 1);
        }

        SetCornersAndBorder(WindowBackdrop.None, WindowCorners.Default);

        _root.RequestedTheme = platform.RequestedTheme;
        ApplyCaption();
        _root.KeyDown += OnRootKeyDown;
        _root.Tag = host.Client;
        _xaml = new DesktopWindowXamlSource();
        _xaml.Initialize(Win32Interop.GetWindowIdFromWindow(Handle));
        _xaml.Content = _root;
        _xaml.SiteBridge.Show();
        platform.ThemeChanged += OnThemeChanged;
    }

    /// <summary>Sizes the island to the window's client area, in physical pixels.</summary>
    internal void FitIsland(int width, int height)
    {
        _xaml?.SiteBridge.MoveAndResize(new RectInt32(0, 0, width, height));
    }

    private void OnThemeChanged()
    {
        _root.RequestedTheme = platform.RequestedTheme;
        ApplyCaption();
        if (_backdropConfig is { } config)
        {
            config.Theme = platform.BackdropTheme;
            config.IsHighContrast = platform.IsHighContrast;
        }
    }

    /// <summary>The caption follows the shell's dark or light choice, including a window that has a title bar.</summary>
    private void ApplyCaption()
    {
        if (Handle == 0)
            return;
        SetDwm(DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, platform.IsDark ? 1 : 0);
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && host.Focus.DismissesOnEscape)
        {
            host.Client.OnDismissed();
            e.Handled = true;
        }
    }

    /// <summary>Fades the content. The material behind it does not fade, and has no opacity of its own.</summary>
    public void SetOpacity(double opacity)
    {
        _root.Opacity = opacity;
    }

    /// <summary>Clips the content to a rectangle in logical window coordinates. It does not change <see cref="HostPlacement.SetInputRegion"/>.</summary>
    public void SetVisibleMask(Rect? bounds)
    {
        _root.Clip = bounds is { } b
            ? new RectangleGeometry { Rect = new Rect(b.X, b.Y, b.Width, b.Height) }
            : null;
    }

    /// <summary>
    /// Puts a material behind the content. A controller told the window is active keeps a live material on a window
    /// that never activates, which the DWM backdrop attribute does not.
    /// </summary>
    public void SetBackdrop(WindowBackdrop backdrop, WindowCorners corners)
    {
        _backdropController?.Dispose();
        _backdropController = null;
        _backdropConfig = null;
        if (backdrop != WindowBackdrop.None && _xaml is not null)
        {
            _backdropConfig = new SystemBackdropConfiguration
            { IsInputActive = true, Theme = platform.BackdropTheme, IsHighContrast = platform.IsHighContrast };
            ISystemBackdropControllerWithTargets controller = backdrop switch
            {
                WindowBackdrop.Acrylic => new DesktopAcrylicController(),
                WindowBackdrop.MicaAlt => new MicaController { Kind = MicaKind.BaseAlt },
                _ => new MicaController()
            };
            controller.AddSystemBackdropTarget(_xaml.As<ICompositionSupportsSystemBackdrop>());
            controller.SetSystemBackdropConfiguration(_backdropConfig);
            _backdropController = controller;
        }

        SetCornersAndBorder(backdrop, corners);
    }

    /// <summary>A fullscreen window's corners are square, so it meets the monitor's corners instead of rounding inside them.</summary>
    internal void SetFullscreen(bool fullscreen)
    {
        if (_fullscreen == fullscreen)
            return;
        _fullscreen = fullscreen;
        ApplyCorners();
    }

    /// <summary>The window's corners, and for a panel or popup its border, which follow its material.</summary>
    private void SetCornersAndBorder(WindowBackdrop backdrop, WindowCorners corners)
    {
        // A bar along an edge stays square; a popup with a material is a rounded flyout.
        _corners = corners != WindowCorners.Default ? corners : _kind.OwnCorners(backdrop);
        ApplyCorners();
        if (_kind.ReplacesSystemFrame)
        {
            // A popup window with a material gets Windows' hairline border; a plain panel or popup window gets none.
            SetDwm(DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, _kind.HairlineBorder(backdrop)
                ? PInvoke.DWMWA_COLOR_DEFAULT
                : PInvoke.DWMWA_COLOR_NONE);
        }
    }

    private void ApplyCorners()
    {
        // DWM_WINDOW_CORNER_PREFERENCE: DWMWCP_DEFAULT, DWMWCP_DONOTROUND, DWMWCP_ROUND, DWMWCP_ROUNDSMALL.
        var preference = (_fullscreen ? WindowCorners.Square : _corners) switch
        {
            WindowCorners.Square => 1,
            WindowCorners.Round => 2,
            WindowCorners.RoundSmall => 3,
            _ => 0
        };
        SetDwm(DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, preference);
    }

    private void SetDwm<T>(DWMWINDOWATTRIBUTE attribute, T value)
        where T : unmanaged
    {
        PInvoke.DwmSetWindowAttribute((HWND)Handle, attribute, MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value)));
    }

    /// <summary>Hands the keyboard to the island, unless it already has it.</summary>
    internal void FocusContent()
    {
        if (_xaml is { HasFocus: false } xaml)
            xaml.NavigateFocus(new XamlSourceFocusNavigationRequest(XamlSourceFocusNavigationReason.Programmatic));
    }

    public void Dispose()
    {
        platform.ThemeChanged -= OnThemeChanged;
        _backdropController?.Dispose();
        _backdropController = null;
        _root.KeyDown -= OnRootKeyDown;
        _root.Children.Clear();
        _xaml?.Dispose();
        _xaml = null;
    }
}
