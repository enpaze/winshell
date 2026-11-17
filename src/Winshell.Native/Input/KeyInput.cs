using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Winshell.Native;

internal static class KeyInput
{
    private const VIRTUAL_KEY Unassigned = (VIRTUAL_KEY)0xE8;

    internal const int ProbeId = 0xBFFE;

    internal const nuint Marker = 0x57154E4B;

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

    internal static bool IsExtended(int key)
    {
        return key is >= 0x21 and <= 0x28 or 0x2D or 0x2E or 0x6F;
    }
}
