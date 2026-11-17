using System.Globalization;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

internal readonly record struct LayoutFacts(string Id, string Language, string Name);

internal readonly record struct LayoutCensus(IReadOnlyList<LayoutFacts> Layouts, string? CurrentId);

internal sealed unsafe class KeyboardLayouts
{
    private const uint InputLanguageRequest = 0x0050;

    private const uint Iso639LanguageName = 0x00000059;

    private const uint Iso639LanguageName2 = 0x00000067;

    private const int LocaleNameCapacity = 85;

    private const string LayoutsKey = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, string> _languages = new();
    private Dictionary<ushort, string>? _klidByLayoutId;

    internal LayoutCensus Read()
    {
        var handles = Handles();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var layouts = new List<LayoutFacts>(handles.Length);
        foreach (var handle in handles)
        {
            var facts = Describe(handle);
            if (seen.Add(facts.Id))
                layouts.Add(facts);
        }

        var current = Foreground();
        return new LayoutCensus(layouts, current == 0 ? null : IdOf(current));
    }

    internal bool Use(string id)
    {
        if (id.Length == 0)
            return false;
        foreach (var handle in Handles())
        {
            if (!string.Equals(IdOf(handle), id, StringComparison.Ordinal))
                continue;
            var window = Focused();
            return window != 0 && PInvoke.PostMessage((HWND)window, InputLanguageRequest, default, handle);
        }

        return false;
    }

    private static nint Foreground()
    {
        var window = PInvoke.GetForegroundWindow();
        if ((nint)window == 0)
            return 0;
        var thread = PInvoke.GetWindowThreadProcessId(window);
        return thread == 0 ? 0 : (nint)PInvoke.GetKeyboardLayout(thread).Value;
    }

    private static nint Focused()
    {
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        if (PInvoke.GetGUIThreadInfo(0, &info))
        {
            if ((nint)info.hwndFocus != 0)
                return info.hwndFocus;
            if ((nint)info.hwndActive != 0)
                return info.hwndActive;
        }

        return PInvoke.GetForegroundWindow();
    }

    private LayoutFacts Describe(nint handle)
    {
        var id = IdOf(handle);
        return new LayoutFacts(id, LanguageOf(id), NameOf(id));
    }

    private string LanguageOf(string id)
    {
        var tail = id.Length > 4 ? id[^4..] : id;
        return ushort.TryParse(tail, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var langId)
            ? Language(langId)
            : "";
    }

    private string IdOf(nint handle)
    {
        var bits = Bits(handle);
        var low = (ushort)bits;
        var high = (ushort)(bits >> 16);
        if ((high & 0xF000) == 0xF000)
            return KlidForLayoutId((ushort)(high & 0x0FFF)) ?? low.ToString("X8", CultureInfo.InvariantCulture);
        var language = high != 0 ? high : low;
        return language.ToString("X8", CultureInfo.InvariantCulture);
    }

    private string? KlidForLayoutId(ushort layoutId)
    {
        _klidByLayoutId ??= LoadLayoutIds();
        return _klidByLayoutId.GetValueOrDefault(layoutId);
    }

    private static Dictionary<ushort, string> LoadLayoutIds()
    {
        var map = new Dictionary<ushort, string>();
        using var root = Registry.LocalMachine.OpenSubKey(LayoutsKey);
        if (root is null)
            return map;
        foreach (var name in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(name);
            if (key?.GetValue("Layout Id") is not string text ||
                !ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
                continue;
            map.TryAdd(id, name.ToUpperInvariant());
        }

        return map;
    }

    private string NameOf(string id)
    {
        if (_names.TryGetValue(id, out var name))
            return name;
        name = ReadName(id);
        _names.Add(id, name);
        return name;
    }

    private static string ReadName(string id)
    {
        using var key = Registry.LocalMachine.OpenSubKey(LayoutsKey + "\\" + id);
        if (key is null)
            return "";
        var text = key.GetValue("Layout Text") as string ?? "";
        if (key.GetValue("Layout Display Name") is not string display || display.Length == 0)
            return text;
        var resolved = Indirect(display);
        return resolved.Length > 0 ? resolved : text;
    }

    private static string Indirect(string text)
    {
        if (text[0] != '@')
            return text;
        var buffer = new char[512];
        return PInvoke.SHLoadIndirectString(text, buffer).Value == 0 ? new string(buffer).TrimEnd('\0') : "";
    }

    private string Language(ushort langId)
    {
        if (langId == 0)
            return "";
        if (_languages.TryGetValue(langId, out var language))
            return language;
        language = ReadLanguage(langId);
        _languages.Add(langId, language);
        return language;
    }

    private static string ReadLanguage(ushort langId)
    {
        var locale = LocaleName(langId);
        if (locale.Length == 0)
            return "";
        var two = LocaleInfo(locale, Iso639LanguageName);
        return two.Length > 0 ? two : LocaleInfo(locale, Iso639LanguageName2);
    }

    private static string LocaleName(uint localeId)
    {
        var buffer = new char[LocaleNameCapacity];
        fixed (char* text = buffer)
        {
            var written = PInvoke.LCIDToLocaleName(localeId, text, buffer.Length, 0);
            return written > 1 ? new string(buffer, 0, written - 1) : "";
        }
    }

    private static string LocaleInfo(string locale, uint kind)
    {
        var buffer = new char[16];
        fixed (char* name = locale)
        fixed (char* text = buffer)
        {
            var written = PInvoke.GetLocaleInfoEx(name, kind, text, buffer.Length);
            return written > 1 ? new string(buffer, 0, written - 1) : "";
        }
    }

    private static nint[] Handles()
    {
        var count = PInvoke.GetKeyboardLayoutList(0, null);
        if (count <= 0)
            return [];
        var buffer = new HKL[count];
        int written;
        fixed (HKL* pointer = buffer)
        {
            written = PInvoke.GetKeyboardLayoutList(buffer.Length, pointer);
        }

        if (written <= 0)
            return [];
        if (written > buffer.Length)
            written = buffer.Length;
        var handles = new nint[written];
        for (var i = 0; i < written; i++)
            handles[i] = (nint)buffer[i].Value;
        return handles;
    }

    private static uint Bits(nint handle)
    {
        return unchecked((uint)handle);
    }
}
