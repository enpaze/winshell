using System.ComponentModel;
using Winshell.Native;

namespace Winshell.Network;

/// <summary>A profile write. <see cref="Wrote"/> is false when nothing was sent to Windows.</summary>
internal readonly record struct ProfileChange(string? Error, bool Wrote);

/// <summary>Gets a profile, changes one element, and writes it back with the flag Windows reported.</summary>
internal static class WifiProfileEdit
{
    public static ProfileChange SetAutoConnect(Guid adapter, string profileName, bool auto)
    {
        if (!TryRead(adapter, profileName, out var xml, out var flags, out var error))
            return new ProfileChange(error, false);
        if (!WifiProfileXml.TrySetAutoConnect(xml, auto, out var updated, out var changed, out error))
            return new ProfileChange(error, false);
        return changed ? Write(adapter, updated, flags) : new ProfileChange(null, false);
    }

    public static ProfileChange ClearPassword(Guid adapter, string profileName)
    {
        if (!TryRead(adapter, profileName, out var xml, out var flags, out var error))
            return new ProfileChange(error, false);
        if (!WifiProfileXml.TryClearPassword(xml, out var updated, out var changed, out error))
            return new ProfileChange(error, false);
        return changed ? Write(adapter, updated, flags) : new ProfileChange(null, false);
    }

    public static string WindowsMessage(uint code)
    {
        return new Win32Exception((int)code).Message;
    }

    private static bool TryRead(Guid adapter, string profileName, out string xml, out uint flags, out string? error)
    {
        xml = "";
        var read = Wlan.GetProfile(adapter, profileName, out var text, out flags);
        if (read != 0 || string.IsNullOrEmpty(text))
        {
            error = read == 0 ? WifiProfileXml.Unreadable : WindowsMessage(read);
            return false;
        }

        xml = text;
        error = null;
        return true;
    }

    private static ProfileChange Write(Guid adapter, string xml, uint flags)
    {
        var error = Wlan.SetProfile(adapter, xml, flags, out _);
        return error == 0 ? new ProfileChange(null, true) : new ProfileChange(WindowsMessage(error), true);
    }
}

/// <summary>Turns profile XML into the fields a configuration sees. The XML itself is not kept.</summary>
internal static class WifiProfiles
{
    public static WifiProfileInfo Describe(Guid adapter, string profileName, string? xml, uint readError)
    {
        var id = WifiFields.Id(adapter, profileName);
        if (readError != 0 || string.IsNullOrEmpty(xml))
        {
            var message = readError == 0 ? WifiProfileXml.Unreadable : WifiProfileEdit.WindowsMessage(readError);
            return new WifiProfileInfo(id, profileName, "", false, WifiSecurityType.Unknown, false, false, false,
                message);
        }

        if (!WifiProfileXml.TryParse(xml, out var fields, out var error))
            return new WifiProfileInfo(id, profileName, "", false, WifiSecurityType.Unknown, false, false, false,
                error);
        return new WifiProfileInfo(
            id, profileName, fields.Ssid, fields.AutoConnect, fields.Security, fields.HasPassword, true, fields.Hidden,
            "");
    }

    /// <summary>A readable profile matches its SSID. One that could not be read matches its profile name.</summary>
    public static bool Matches(WifiProfileInfo profile, string networkName)
    {
        return profile.Loaded
            ? string.Equals(profile.Ssid, networkName, StringComparison.Ordinal)
            : string.Equals(profile.Name, networkName, StringComparison.Ordinal);
    }
}

/// <summary>Runs profile XML writes one at a time, off the thread that asked.</summary>
internal sealed class ProfileWriteQueue
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> RunAsync<T>(Func<T> work)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(work).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
