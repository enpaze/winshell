using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Windows.Win32;
using Winshell.Native;

namespace Winshell.Tray;

internal sealed record NotifyIconCall
{
    public uint Message { get; init; }

    public nint Window { get; init; }

    public uint Id { get; init; }

    public uint Flags { get; init; }

    public uint CallbackMessage { get; init; }

    public nint Icon { get; init; }

    public string Tip { get; init; } = "";

    public uint State { get; init; }

    public uint StateMask { get; init; }

    public string Info { get; init; } = "";

    public uint TimeoutOrVersion { get; init; }

    public string InfoTitle { get; init; } = "";

    public uint InfoFlags { get; init; }

    public Guid Guid { get; init; }
}

internal enum TrayClick
{
    Left,
    Double,
    Middle,

    Menu
}

internal static class TrayProtocol
{
    public const uint Version = 3;

    public const uint Version4 = 4;

    public const nuint NotifyIconData = 1;

    public const int Signature = 0x34753423;

    public const uint Add = 0, Modify = 1, Delete = 2, SetVersion = 4;

    public const uint NIF_MESSAGE = 0x1,
        NIF_ICON = 0x2,
        NIF_TIP = 0x4,
        NIF_STATE = 0x8,
        NIF_INFO = 0x10,
        NIF_GUID = 0x20;

    public const uint NIS_HIDDEN = 0x1;

    private const int HeaderSize = 8;
    private const int TipOffset = 24, TipChars = 128;
    private const int StateOffset = 280, StateMaskOffset = 284;
    private const int InfoOffset = 288, InfoChars = 256;
    private const int TimeoutOffset = 800;
    private const int InfoTitleOffset = 804, InfoTitleChars = 64;
    private const int InfoFlagsOffset = 932;
    private const int GuidOffset = 936;

    public const int FullSize = 956;

    public static NotifyIconCall? Read(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderSize + TipOffset || BinaryPrimitives.ReadInt32LittleEndian(payload) != Signature)
            return null;
        var message = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]);
        var sent = payload[HeaderSize..];
        Span<byte> nid = stackalloc byte[FullSize];
        nid.Clear();
        sent[..Math.Min(sent.Length, FullSize)].CopyTo(nid);
        var flags = U32(nid, 12);
        return new NotifyIconCall
        {
            Message = message,
            Window = BinaryPrimitives.ReadInt32LittleEndian(nid[4..]),
            Id = U32(nid, 8),
            Flags = flags,
            CallbackMessage = U32(nid, 16),
            Icon = BinaryPrimitives.ReadInt32LittleEndian(nid[20..]),
            Tip = Text(nid, TipOffset, TipChars),
            State = U32(nid, StateOffset),
            StateMask = U32(nid, StateMaskOffset),
            Info = Text(nid, InfoOffset, InfoChars),
            TimeoutOrVersion = U32(nid, TimeoutOffset),
            InfoTitle = Text(nid, InfoTitleOffset, InfoTitleChars),
            InfoFlags = U32(nid, InfoFlagsOffset),
            Guid = (flags & NIF_GUID) != 0 && sent.Length >= GuidOffset + 16
                ? new Guid(nid.Slice(GuidOffset, 16))
                : Guid.Empty
        };
    }

    public static uint[] ClickMessages(TrayClick click, uint version)
    {
        var current = version >= Version;
        return click switch
        {
            TrayClick.Left => current
                ? [PInvoke.WM_LBUTTONDOWN, PInvoke.WM_LBUTTONUP, PInvoke.NIN_SELECT]
                : [PInvoke.WM_LBUTTONDOWN, PInvoke.WM_LBUTTONUP],
            TrayClick.Double => current
                ?
                [
                    PInvoke.WM_LBUTTONDOWN, PInvoke.WM_LBUTTONUP, PInvoke.NIN_SELECT, PInvoke.WM_LBUTTONDBLCLK,
                    PInvoke.WM_LBUTTONUP, PInvoke.NIN_SELECT
                ]
                : [PInvoke.WM_LBUTTONDOWN, PInvoke.WM_LBUTTONUP, PInvoke.WM_LBUTTONDBLCLK, PInvoke.WM_LBUTTONUP],
            TrayClick.Middle => [PInvoke.WM_MBUTTONDOWN, PInvoke.WM_MBUTTONUP],
            _ => current
                ? [PInvoke.WM_RBUTTONDOWN, PInvoke.WM_RBUTTONUP, PInvoke.WM_CONTEXTMENU]
                : [PInvoke.WM_RBUTTONDOWN, PInvoke.WM_RBUTTONUP]
        };
    }

    public static (nint WParam, nint LParam) Callback(uint version, uint iconId, uint message, int x, int y)
    {
        if (version >= Version4)
        {
            var w = (uint)(x & 0xffff) | ((uint)(y & 0xffff) << 16);
            var l = (message & 0xffff) | ((iconId & 0xffff) << 16);
            return ((nint)w, (nint)l);
        }

        return ((nint)iconId, (nint)message);
    }

    private static uint U32(ReadOnlySpan<byte> nid, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(nid[offset..]);
    }

    private static string Text(ReadOnlySpan<byte> nid, int offset, int chars)
    {
        var text = MemoryMarshal.Cast<byte, char>(nid.Slice(offset, chars * 2));
        var end = text.IndexOf('\0');
        return new string(end < 0 ? text : text[..end]);
    }
}
