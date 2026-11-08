using System.Globalization;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winshell.Native;

/// <summary>One installed keyboard layout: the id Windows stores, its ISO language code, and the name Windows shows.</summary>
internal readonly record struct LayoutFacts(string Id, string Language, string Name);

/// <summary>The installed layouts, in the order Windows cycles them, and the foreground window's layout id.</summary>
internal readonly record struct LayoutCensus(IReadOnlyList<LayoutFacts> Layouts, string? CurrentId);

/// <summary>
/// The keyboard layouts Windows has loaded, and a switch that reaches the foreground window. A layout change is
/// announced only to the focused window, so a reading asks the foreground thread which layout it has. Switching posts
/// <c>WM_INPUTLANGCHANGEREQUEST</c> to that window: its <c>DefWindowProc</c> activates the layout on its thread.
/// <c>ActivateKeyboardLayout</c> would change this thread, and <c>LoadKeyboardLayout</c> does nothing unless this
/// process owns the focus, and it can load a layout that is not installed.
/// </summary>
internal sealed unsafe class KeyboardLayouts
{
    /// <summary><c>WM_INPUTLANGCHANGEREQUEST</c>.</summary>
    private const uint InputLanguageRequest = 0x0050;

    /// <summary><c>LOCALE_SISO639LANGNAME</c>, the two-letter language code, such as en.</summary>
    private const uint Iso639LanguageName = 0x00000059;

    /// <summary><c>LOCALE_SISO639LANGNAME2</c>, the three-letter language code, such as eng.</summary>
    private const uint Iso639LanguageName2 = 0x00000067;

    /// <summary><c>LOCALE_NAME_MAX_LENGTH</c>.</summary>
    private const int LocaleNameCapacity = 85;

    private const string LayoutsKey = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, string> _languages = new();
    private Dictionary<ushort, string>? _klidByLayoutId;

    /// <summary>The loaded layouts and the foreground thread's layout. Does not change either.</summary>
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

    /// <summary>Posts a request that the focused window use <paramref name="id"/>. Returns whether it was posted.</summary>
    internal bool Use(string id)
    {
        if (id.Length == 0)
            return false;
        foreach (var handle in Handles())
        {
            if (!string.Equals(IdOf(handle), id, StringComparison.Ordinal))
                continue;
            var window = Focused();
            return window != 0 && Win32.PostMessageW(window, InputLanguageRequest, 0, handle);
        }

        return false;
    }

    /// <summary>
    /// The layout handle of the foreground thread. <c>GetKeyboardLayout(0)</c> is this thread, so a shell window that
    /// is not the foreground would become the answer.
    /// </summary>
    private static nint Foreground()
    {
        var window = Win32.GetForegroundWindow();
        if (window == 0)
            return 0;
        var thread = Win32.GetWindowThreadProcessId(window, out _);
        return thread == 0 ? 0 : (nint)PInvoke.GetKeyboardLayout(thread).Value;
    }

    /// <summary>The window with the keyboard focus on the foreground thread, or the foreground window when it has none.</summary>
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

        return Win32.GetForegroundWindow();
    }

    private LayoutFacts Describe(nint handle)
    {
        var id = IdOf(handle);
        // The label follows the layout's own language, the low word of its KLID. The low word of the HKL is the input
        // language, which can be English while the keyboard is Swedish.
        return new LayoutFacts(id, LanguageOf(id), NameOf(id));
    }

    private string LanguageOf(string id)
    {
        var tail = id.Length > 4 ? id[^4..] : id;
        return ushort.TryParse(tail, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var langId)
            ? Language(langId)
            : "";
    }

    /// <summary>
    /// The KLID, such as 00000409. <c>GetKeyboardLayoutName</c> reports it for the calling thread only, which would
    /// mean activating each layout here. When the high word's top bits are <c>0xF</c>, its low 12 bits are the
    /// <c>Layout Id</c> to find under the keyboard layouts key. Otherwise the KLID is the high word when that word is
    /// set, and the low word when it is not.
    /// </summary>
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

    /// <summary>Resolves a <c>Layout Display Name</c> such as <c>@%SystemRoot%\system32\input.dll,-5000</c>.</summary>
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
