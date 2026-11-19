using Winshell.Apps;
using Winshell.Audio;
using Winshell.Bluetooth;
using Winshell.Cpu;
using Winshell.ExplorerTaskbar;
using Winshell.Input;
using Winshell.Keyboard;
using Winshell.Media;
using Winshell.Network;
using Winshell.Notifications;
using Winshell.Power;
using Winshell.Privacy;
using Winshell.Session;
using Winshell.Tray;
using Winshell.Wallpaper;
using Winshell.Windows;
using Winshell.Winland;

namespace Winshell;

public sealed partial class Shell
{
    private AppsService? _apps;
    private AudioService? _audio;
    private BluetoothService? _bluetooth;
    private BrightnessService? _brightness;
    private CpuService? _cpu;
    private ExplorerTaskbarService? _explorerTaskbar;
    private HotkeyRegistrar? _hotkeys;
    private KeyboardLayoutService? _keyboardLayouts;
    private MediaService? _media;
    private NetworkingService? _networking;
    private NotificationListener? _notifications;
    private PowerService? _power;
    private PowerModeService? _powerMode;
    private PrivacyService? _privacy;
    private SystemTray? _systemTray;
    private VirtualDesktopsService? _virtualDesktops;
    private WallpaperService? _wallpaper;

    private WindowListService? _windows;
    private WinlandService? _winland;
    private IShellService? _winlandToWindowList;

    private IEnumerable<IDisposable?> ServicesInStopOrder =>
        ((IShellService?[])
        [
            _winland, _winlandToWindowList, _windows, _virtualDesktops, _apps, _audio, _bluetooth, _brightness, _cpu,
            _explorerTaskbar, _media, _networking, _notifications, _power, _powerMode, _privacy, _systemTray,
            _hotkeys, _keyboardLayouts, _wallpaper
        ]).Select(service => service is null ? null : new DisposeAction(service.Stop));

    public AppsService Apps => _apps ??= new AppsService(Loop, Log);

    public AudioService Audio => _audio ??= new AudioService(Loop, Log);

    public BluetoothService Bluetooth => _bluetooth ??= new BluetoothService(Loop, Log);

    public BrightnessService Brightness => _brightness ??= new BrightnessService(Loop, Log, Platform.Screens);

    public CpuService Cpu => _cpu ??= new CpuService(Loop);

    public ExplorerTaskbarService ExplorerTaskbar => _explorerTaskbar ??= new ExplorerTaskbarService(Loop, Log);

    public JumpListService JumpLists => field ??= new JumpListService();

    public KeyboardLayoutService KeyboardLayouts => _keyboardLayouts ??= new KeyboardLayoutService(Loop);

    public MediaService Media => _media ??= new MediaService(Loop, Log);

    public NetworkingService Networking => _networking ??= new NetworkingService(Loop, Log);

    public NotificationListener Notifications => _notifications ??= new NotificationListener(Loop, Log);

    public PowerService Power => _power ??= new PowerService(Loop, Log);

    public PowerModeService PowerMode => _powerMode ??= new PowerModeService(Loop, Log);

    public PrivacyService Privacy => _privacy ??= new PrivacyService(Loop, Log);

    public RecentDocuments RecentDocuments =>
        field ??= new RecentDocuments(Environment.GetFolderPath(Environment.SpecialFolder.Recent));

    public SessionService Session => field ??= new SessionService(Log);

    public StartupService Startup => field ??= StartupService.ForThisProcess(Log);

    public SystemTray SystemTray => _systemTray ??= new SystemTray(Loop, Log);

    public VirtualDesktopsService VirtualDesktops => _virtualDesktops ??= new VirtualDesktopsService(Loop, Log);

    public WallpaperService Wallpaper => _wallpaper ??= new WallpaperService(Loop, Log);

    public WindowListService WindowList => _windows ??= new WindowListService(Loop, Log, VirtualDesktops);

    public WinlandService Winland => _winland ?? Linked(WindowList);

    public Hotkey Hotkey(string sequence, Action? pressed = null)
    {
        return new Hotkey(Loop, Log, _hotkeys ??= new HotkeyRegistrar(Loop, Log), sequence, pressed);
    }

    public IdleMonitor NewIdleMonitor()
    {
        return new IdleMonitor(Loop, Log, () => KeepAwake.AnyActive);
    }

    private WinlandService Linked(WindowListService windows)
    {
        var winland = _winland = new WinlandService(Loop, Log);
        winland.Attach(windows);
        _winlandToWindowList = new Unlink(winland);
        return winland;
    }

    private sealed class Unlink(WinlandService winland) : IShellService
    {
        public void Stop()
        {
            winland.Attach(null);
        }
    }
}
