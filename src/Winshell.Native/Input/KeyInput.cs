using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

/// <summary>Injects keys with <c>SendInput</c>.</summary>
internal static class KeyInput
{
    /// <summary>Unassigned virtual key. The same value AutoHotkey uses for its menu mask.</summary>
    private const VIRTUAL_KEY Unassigned = (VIRTUAL_KEY)0xE8;

    /// <summary>
    /// The hotkey id this process owns for asking whether a chord is free. It sits at the top of the range
    /// <c>RegisterHotKey</c> gives an application, and the hotkey registrar does not hand it to a real chord.
    /// </summary>
    internal const int ProbeId = 0xBFFE;

    /// <summary>Tag on <c>dwExtraInfo</c>, so <see cref="LowLevelKeyboard"/> passes the injection by.</summary>
    internal const nuint Marker = 0x57154E4B;

    /// <summary>
    /// One down and up of an unassigned key, sent as one injection that nothing else can split. A focus takeover uses
    /// it to become the program that last sent input. Sent while the Windows key is down, it uses that press so Start
    /// does not open, without leaving the Windows key logically held.
    /// </summary>
    internal static void Tap()
    {
        var inputs = new INPUT[2];
        for (var i = 0; i < inputs.Length; i++)
        {
            inputs[i].type = INPUT_TYPE.INPUT_KEYBOARD;
            inputs[i].Anonymous.ki.wVk = Unassigned;
            inputs[i].Anonymous.ki.dwFlags = i == 1 ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0;
            inputs[i].Anonymous.ki.dwExtraInfo = Marker;
        }

        SentInput.Send(inputs, 1);
    }

    /// <summary>
    /// A chord's keys down in order, then up in reverse, as one injection that nothing else can split, so no key is left
    /// held. Windows gives them to the window in front, so the caller checks which that is first.
    /// </summary>
    internal static bool Chord(IReadOnlyList<int> keys)
    {
        var inputs = new INPUT[keys.Count * 2];
        for (var i = 0; i < keys.Count; i++)
        {
            Key(ref inputs[i], keys[i], false);
            Key(ref inputs[^(i + 1)], keys[i], true);
        }

        return SentInput.Send(inputs, keys.Count);
    }

    /// <summary>
    /// Whether no program has the chord as a hotkey, this one included: Windows gives a registered hotkey to the program
    /// that registered it, wherever the keyboard is.
    /// </summary>
    internal static bool Unclaimed(uint modifiers, int key)
    {
        if (!PInvoke.RegisterHotKey(default, ProbeId, (HOT_KEY_MODIFIERS)modifiers, (uint)key))
            return false;
        PInvoke.UnregisterHotKey(default, ProbeId);
        return true;
    }

    private static void Key(ref INPUT input, int key, bool up)
    {
        input.type = INPUT_TYPE.INPUT_KEYBOARD;
        input.Anonymous.ki.wVk = (VIRTUAL_KEY)key;
        input.Anonymous.ki.wScan = (ushort)ScanCode(key);
        input.Anonymous.ki.dwFlags = (up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0) |
                                     (IsExtended(key) ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0);
    }

    internal static uint ScanCode(int key)
    {
        return PInvoke.MapVirtualKey((uint)key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
    }

    /// <summary>The keys of the navigation block and the numpad's divide, which share scan codes with the numpad.</summary>
    internal static bool IsExtended(int key)
    {
        return key is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x6F;
    }
}
