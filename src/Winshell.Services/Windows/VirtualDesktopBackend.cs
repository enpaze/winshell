using System.Globalization;
using System.Security;
using Microsoft.Win32;
using Winshell.Native;

namespace Winshell.Windows;

internal interface IVirtualDesktopBackend
{
    DesktopContract Contract { get; }

    IDisposable ListenSwitched(Action handler, Action<Exception> failed);

    bool StartWatching();

    string ListStamp { get; }

    bool TryRead(out IReadOnlyList<DesktopRecord> desktops, out Guid current, out string error);

    bool SwitchTo(Guid id, out string problem);

    Guid? Create(out string problem);

    bool Remove(Guid id, Guid fallback, out string problem);
}

internal readonly record struct WindowsVersion(uint Build, uint Update)
{
    public override string ToString()
    {
        return Update == 0 ? Build.ToString(CultureInfo.InvariantCulture) : $"{Build}.{Update}";
    }

    public static WindowsVersion Current => _current ??= Read();

    private static WindowsVersion? _current;

    public static WindowsVersion Read()
    {
        return OsVersion.Build() is { } build ? new WindowsVersion(build, ReadUpdate()) : new WindowsVersion(0, 0);
    }

    private static uint ReadUpdate()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("UBR") switch
            {
                int value when value > 0 => (uint)value,
                uint value => value,
                _ => 0
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return 0;
        }
    }
}

internal sealed class VirtualDesktopBackend : IVirtualDesktopBackend
{
    public static VirtualDesktopBackend Shared { get; } = new();

    private readonly Listeners<Action> _switched = new();
    private WinEventHook? _hook;

    private VirtualDesktopBackend()
    {
    }

    public IDisposable ListenSwitched(Action handler, Action<Exception> failed)
    {
        return _switched.Add(handler, failed);
    }

    public DesktopContract Contract
    {
        get
        {
            var version = WindowsVersion.Current;
            var contract = DesktopContract.ForBuild(version.Build);
            if (contract.Supported)
                return contract;
            return new DesktopContract(false, DesktopContract.Unsupported($"Windows {version}"));
        }
    }

    public bool StartWatching()
    {
        _hook ??= WinEventHook.Start(PInvoke.EVENT_SYSTEM_DESKTOPSWITCH, OnEvent);
        return _hook is not null;
    }

    private void OnEvent(uint eventType, nint hwnd, int idObject, int idChild)
    {
        _switched.Raise(0, static (handler, _) => handler());
    }

    public string ListStamp =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops",
            "VirtualDesktopIDs", null) is byte[] ids
            ? Convert.ToHexString(ids)
            : "";

    public bool TryRead(out IReadOnlyList<DesktopRecord> desktops, out Guid current, out string error)
    {
        desktops = [];
        if (!ImmersiveDesktops.TryRead(out var entries, out current, out error))
            return false;
        desktops = entries.Select(entry => new DesktopRecord(entry.Id, entry.Name)).ToList();
        return true;
    }

    public bool SwitchTo(Guid id, out string problem)
    {
        if (ImmersiveDesktops.SwitchTo(id, out problem))
            return true;
        problem = $"SwitchDesktop {problem}.";
        return false;
    }

    public Guid? Create(out string problem)
    {
        if (ImmersiveDesktops.Create(out problem) is { } id)
            return id;
        problem = $"CreateDesktop {problem}.";
        return null;
    }

    public bool Remove(Guid id, Guid fallback, out string problem)
    {
        if (id == Guid.Empty || fallback == Guid.Empty || id == fallback)
        {
            problem = "RemoveDesktop needs two different desktops.";
            return false;
        }

        if (ImmersiveDesktops.Remove(id, fallback, out problem))
            return true;
        problem = $"RemoveDesktop {problem}.";
        return false;
    }
}
