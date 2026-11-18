using System.Buffers.Binary;
using System.Text;
using Winshell.Tray;

namespace Winshell.Services.Tests;

[TestClass]
public class TrayProtocolTests
{
    [TestMethod]
    public void Version_4_callbacks_carry_the_point_and_the_icon_id()
    {
        var (w, l) = TrayProtocol.Callback(TrayProtocol.Version4, 7, 0x0205, 20, 40);
        Assert.AreEqual((nint)(20u | (40u << 16)), w);
        Assert.AreEqual((nint)(0x0205u | (7u << 16)), l);
    }

    [TestMethod]
    public void Older_callbacks_carry_the_icon_id_and_the_message()
    {
        var (w, l) = TrayProtocol.Callback(TrayProtocol.Version, 7, 0x0205, 20, 40);
        Assert.AreEqual(7, w);
        Assert.AreEqual(0x0205, l);
    }

    [TestMethod]
    public void A_point_left_of_the_origin_keeps_its_low_16_bits()
    {
        var (w, _) = TrayProtocol.Callback(TrayProtocol.Version4, 1, 0x007B, -8, 100);
        Assert.AreEqual((nint)(0xfff8u | (100u << 16)), w);
    }

    [TestMethod]
    [DataRow((int)TrayClick.Left, 0u, new uint[] { 0x0201, 0x0202 })]
    [DataRow((int)TrayClick.Left, 4u, new uint[] { 0x0201, 0x0202, 0x0400 })]
    [DataRow((int)TrayClick.Double, 0u, new uint[] { 0x0201, 0x0202, 0x0203, 0x0202 })]
    [DataRow((int)TrayClick.Double, 3u, new uint[] { 0x0201, 0x0202, 0x0400, 0x0203, 0x0202, 0x0400 })]
    [DataRow((int)TrayClick.Middle, 4u, new uint[] { 0x0207, 0x0208 })]
    [DataRow((int)TrayClick.Menu, 0u, new uint[] { 0x0204, 0x0205 })]
    [DataRow((int)TrayClick.Menu, 3u, new uint[] { 0x0204, 0x0205, 0x007B })]
    public void Clicks_send_what_explorer_sends(int click, uint version, uint[] messages)
    {
        Assert.AreSequenceEqual(messages, TrayProtocol.ClickMessages((TrayClick)click, version));
    }

    [TestMethod]
    public void A_full_structure_reads_every_field()
    {
        var guid = Guid.NewGuid();
        var call = TrayProtocol.Read(TrayCall.Build(TrayProtocol.Modify, unchecked((int)0x80001234), 9,
            TrayProtocol.NIF_MESSAGE | TrayProtocol.NIF_TIP | TrayProtocol.NIF_STATE | TrayProtocol.NIF_INFO |
            TrayProtocol.NIF_GUID,
            0x8001, 0x0042, "Sync\nUp to date", 1, 1, "Done", 10000,
            "Backup", 2, guid));
        Assert.IsNotNull(call);
        Assert.AreEqual(TrayProtocol.Modify, call.Message);
        Assert.AreEqual(unchecked((int)0x80001234), call.Window);
        Assert.AreEqual(9u, call.Id);
        Assert.AreEqual(0x8001u, call.CallbackMessage);
        Assert.AreEqual(0x42, call.Icon);
        Assert.AreEqual("Sync\nUp to date", call.Tip);
        Assert.AreEqual(1u, call.State);
        Assert.AreEqual(1u, call.StateMask);
        Assert.AreEqual("Done", call.Info);
        Assert.AreEqual(10000u, call.TimeoutOrVersion);
        Assert.AreEqual("Backup", call.InfoTitle);
        Assert.AreEqual(2u, call.InfoFlags);
        Assert.AreEqual(guid, call.Guid);
    }

    [TestMethod]
    public void A_structure_from_an_older_app_reads_what_it_sent()
    {
        var call = TrayProtocol.Read(TrayCall.Build(TrayProtocol.Add, 0x100, 1,
            TrayProtocol.NIF_TIP | TrayProtocol.NIF_GUID,
            tip: "Old app", guid: Guid.NewGuid(), size: 24 + 64 * 2));
        Assert.IsNotNull(call);
        Assert.AreEqual("Old app", call.Tip);
        Assert.AreEqual(Guid.Empty, call.Guid);
        Assert.AreEqual(0u, call.TimeoutOrVersion);
    }

    [TestMethod]
    public void A_longer_structure_from_a_newer_windows_still_reads()
    {
        var payload = TrayCall.Build(TrayProtocol.Add, 0x100, 1, TrayProtocol.NIF_TIP, tip: "Future",
            size: TrayProtocol.FullSize + 64);
        Assert.AreEqual("Future", TrayProtocol.Read(payload)?.Tip);
    }

    [TestMethod]
    public void Anything_else_is_not_a_call()
    {
        Assert.IsNull(TrayProtocol.Read(new byte[16]));
        var payload = TrayCall.Build(TrayProtocol.Add, 0x100, 1, 0);
        payload[0] ^= 0xff;
        Assert.IsNull(TrayProtocol.Read(payload));
    }
}

internal static class TrayCall
{
    public static byte[] Build(
        uint message,
        int window,
        uint id,
        uint flags,
        uint callback = 0,
        int icon = 0,
        string tip = "",
        uint state = 0,
        uint stateMask = 0,
        string info = "",
        uint timeoutOrVersion = 0,
        string infoTitle = "",
        uint infoFlags = 0,
        Guid? guid = null,
        int size = TrayProtocol.FullSize)
    {
        var nid = new byte[Math.Max(size, TrayProtocol.FullSize)];
        BinaryPrimitives.WriteInt32LittleEndian(nid, size);
        BinaryPrimitives.WriteInt32LittleEndian(nid.AsSpan(4), window);
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(8), id);
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(12), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(16), callback);
        BinaryPrimitives.WriteInt32LittleEndian(nid.AsSpan(20), icon);
        Encoding.Unicode.GetBytes(tip, nid.AsSpan(24));
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(280), state);
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(284), stateMask);
        Encoding.Unicode.GetBytes(info, nid.AsSpan(288));
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(800), timeoutOrVersion);
        Encoding.Unicode.GetBytes(infoTitle, nid.AsSpan(804));
        BinaryPrimitives.WriteUInt32LittleEndian(nid.AsSpan(932), infoFlags);
        (guid ?? Guid.Empty).TryWriteBytes(nid.AsSpan(936));
        var payload = new byte[8 + size];
        BinaryPrimitives.WriteInt32LittleEndian(payload, TrayProtocol.Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), message);
        nid.AsSpan(0, size).CopyTo(payload.AsSpan(8));
        return payload;
    }
}
