using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Winshell.Native;
using Winshell.Network;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class WifiProfileTests
{
    private const string Secret = "winshell-profile-key-9f3a";
    private const string Marker = "winshell-profile-marker";

    private static readonly string Saved =
        "<?xml version=\"1.0\"?>\n"
        + "<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\n"
        + "\t<!-- <connectionMode>auto</connectionMode> -->\n"
        + "\t<name>Office</name>\n"
        + "\t<SSIDConfig>\n"
        + "\t\t<SSID>\n"
        + "\t\t\t<hex>43616665</hex>\n"
        + "\t\t\t<name>Cafe</name>\n"
        + "\t\t</SSID>\n"
        + "\t\t<nonBroadcast>true</nonBroadcast>\n"
        + "\t</SSIDConfig>\n"
        + "\t<connectionMode note=\"keep\">auto</connectionMode>\n"
        + "\t<connectionType>ESS</connectionType>\n"
        + "\t<MSM>\n"
        + "\t\t<security>\n"
        + "\t\t\t<authEncryption>\n"
        + "\t\t\t\t<authentication>WPA2PSK</authentication>\n"
        + "\t\t\t\t<encryption>AES</encryption>\n"
        + "\t\t\t\t<useOneX>false</useOneX>\n"
        + "\t\t\t</authEncryption>\n"
        + "\t\t\t<sharedKey>\n"
        + "\t\t\t\t<keyType>passPhrase</keyType>\n"
        + "\t\t\t\t<protected>true</protected>\n"
        + "\t\t\t\t<keyMaterial>winshell-profile-key-9f3a</keyMaterial>\n"
        + "\t\t\t</sharedKey>\n"
        + "\t\t</security>\n"
        + "\t</MSM>\n"
        + "\t<MacRandomization xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v3\">\n"
        + "\t\t<enableRandomization>true</enableRandomization>\n"
        + "\t\t<randomizationSeed>keep-me</randomizationSeed>\n"
        + "\t</MacRandomization>\n"
        + "</WLANProfile>\n";

    private static readonly string Manual = Saved.Replace(
        "<connectionMode note=\"keep\">auto</connectionMode>",
        "<connectionMode note=\"keep\">manual</connectionMode>",
        StringComparison.Ordinal);

    private static readonly Guid AdapterId = Guid.Parse("0b0f2811-87b5-48d6-9d7a-7a26ae12ff33");

    [TestMethod]
    public void Round_trip_changes_only_the_connection_mode()
    {
        Assert.AreNotEqual(Saved, Manual);
        Assert.IsTrue(WifiProfileXml.TryParse(Saved, out var fields, out var error), error);
        Assert.AreEqual("", error);
        Assert.AreEqual("Office", fields.Name);
        Assert.AreEqual("Cafe", fields.Ssid);
        Assert.AreEqual("auto", fields.ConnectionMode);
        Assert.IsTrue(fields.AutoConnect);
        Assert.IsTrue(fields.HasPassword);
        Assert.IsTrue(fields.Hidden);
        Assert.AreEqual(WifiSecurityType.Wpa2Psk, fields.Security);
        Assert.DoesNotContain(Secret, fields.ToString());

        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(Saved, true, out var same, out var changed, out error), error);
        Assert.IsFalse(changed);
        Assert.AreEqual(Saved, same);

        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(Saved, false, out var updated, out changed, out error), error);
        Assert.IsTrue(changed);
        Assert.AreEqual("", error);
        Assert.AreEqual(Manual, updated);
        Assert.Contains("<!-- <connectionMode>auto</connectionMode> -->", updated);
        Assert.Contains(Secret, updated);
        Assert.Contains("randomizationSeed", updated);
        Assert.IsTrue(WifiProfileXml.TryParse(updated, out var after, out error), error);
        Assert.IsFalse(after.AutoConnect);
        Assert.AreEqual("manual", after.ConnectionMode);
        Assert.IsTrue(after.HasPassword);
        Assert.AreEqual(fields.Ssid, after.Ssid);
        Assert.AreEqual(fields.Security, after.Security);
        Assert.AreEqual(fields.Hidden, after.Hidden);
        Assert.DoesNotContain(Secret, after.ToString());
        Assert.DoesNotContain(Secret, error);
    }

    [TestMethod]
    public void Clear_password_removes_only_the_key_element()
    {
        var cleared = Saved.Replace("<keyMaterial>" + Secret + "</keyMaterial>", "", StringComparison.Ordinal);
        Assert.AreNotEqual(Saved, cleared);
        Assert.IsTrue(WifiProfileXml.TryClearPassword(Saved, out var updated, out var changed, out var error), error);
        Assert.IsTrue(changed);
        Assert.AreEqual("", error);
        Assert.AreEqual(cleared, updated);
        Assert.DoesNotContain(Secret, updated);
        Assert.Contains("<protected>true</protected>", updated);
        Assert.Contains("MacRandomization", updated);
        Assert.IsTrue(WifiProfileXml.TryParse(updated, out var after, out error), error);
        Assert.IsFalse(after.HasPassword);
        Assert.AreEqual("auto", after.ConnectionMode);
        Assert.AreEqual("Cafe", after.Ssid);
        Assert.DoesNotContain(Secret, after.ToString());

        const string prefixed = """
                                <WLANProfile><name>Office</name><connectionMode>auto</connectionMode><sharedKey><keyMaterial /><p:keyMaterial xmlns:p="urn:p">winshell-profile-key-9f3a</p:keyMaterial><protected>true</protected></sharedKey><Vendor xmlns="urn:vendor">keep</Vendor></WLANProfile>
                                """;
        Assert.IsTrue(WifiProfileXml.TryParse(prefixed, out var before, out error), error);
        Assert.IsTrue(before.HasPassword);
        Assert.DoesNotContain(Secret, before.ToString());
        Assert.IsTrue(WifiProfileXml.TryClearPassword(prefixed, out updated, out changed, out error), error);
        Assert.IsTrue(changed);
        Assert.AreEqual(
            """
            <WLANProfile><name>Office</name><connectionMode>auto</connectionMode><sharedKey><protected>true</protected></sharedKey><Vendor xmlns="urn:vendor">keep</Vendor></WLANProfile>
            """,
            updated);
        Assert.DoesNotContain(Secret, updated);

        const string comment =
            "<WLANProfile><name>Office</name><!-- <keyMaterial>winshell-profile-key-9f3a</keyMaterial> --><connectionMode>auto</connectionMode></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TryParse(comment, out var commented, out error), error);
        Assert.IsFalse(commented.HasPassword);
        Assert.DoesNotContain(Secret, commented.ToString());
        Assert.IsTrue(WifiProfileXml.TryClearPassword(comment, out updated, out changed, out error), error);
        Assert.IsFalse(changed);
        Assert.AreEqual(comment, updated);

        var nested = "<WLANProfile><name>Office<nested>" + Secret +
                     "</nested></name><connectionMode>auto</connectionMode></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TryParse(nested, out var name, out error), error);
        Assert.AreEqual("Office", name.Name);
        Assert.IsFalse(name.HasPassword);
        Assert.DoesNotContain(Secret, name.ToString());
    }

    [TestMethod]
    public void Hostile_profiles_are_refused_without_a_write()
    {
        AssertNoWrite(new string('a', WifiProfileXml.MaxCharacters + 1), WifiProfileXml.TooLarge);
        AssertNoWrite("<WLANProfile><keyMaterial>" + Secret, WifiProfileXml.Unreadable);
        AssertNoWrite(Deep(40), WifiProfileXml.Unreadable);
        Assert.IsTrue(WifiProfileXml.TryParse(Deep(8), out _, out var shallow), shallow);
        Assert.AreEqual("", shallow);

        var dtd = """
                  <?xml version="1.0"?>
                  <!DOCTYPE WLANProfile [
                  <!ENTITY secret SYSTEM "file:///C:/winshell-profile-marker">
                  ]>
                  <WLANProfile><name>&secret;</name></WLANProfile>
                  """;
        AssertNoWrite(dtd, WifiProfileXml.Unreadable);
        Assert.DoesNotContain(Marker, WifiProfileXml.Unreadable);

        var billion = """
                      <!DOCTYPE lolz [
                      <!ENTITY lol "lol">
                      <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
                      <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
                      <!ENTITY lol4 "&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;">
                      <!ENTITY lol5 "&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;">
                      <!ENTITY lol6 "&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;">
                      <!ENTITY lol7 "&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;">
                      <!ENTITY lol8 "&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;">
                      <!ENTITY lol9 "&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;">
                      ]>
                      <WLANProfile><name>&lol9;</name></WLANProfile>
                      """;
        AssertNoWrite(billion, WifiProfileXml.Unreadable);

        Assert.IsFalse(WifiProfileXml.TryClearPassword(dtd, out var cleared, out var clearedChanged, out var clearError));
        Assert.IsFalse(clearedChanged);
        Assert.AreEqual(dtd, cleared);
        Assert.AreEqual(WifiProfileXml.Unreadable, clearError);
        Assert.DoesNotContain(Marker, clearError);
        Assert.DoesNotContain(Secret, clearError);
    }

    [TestMethod]
    [DataRow("open", "none", WifiSecurityType.Open)]
    [DataRow("open", "WEP", WifiSecurityType.StaticWep)]
    [DataRow("shared", "none", WifiSecurityType.StaticWep)]
    [DataRow("WPA", "TKIP", WifiSecurityType.WpaEap)]
    [DataRow("WPAPSK", "TKIP", WifiSecurityType.WpaPsk)]
    [DataRow("WPA2", "AES", WifiSecurityType.Wpa2Eap)]
    [DataRow("WPA3ENT", "AES", WifiSecurityType.Wpa2Eap)]
    [DataRow("WPA2PSK", "AES", WifiSecurityType.Wpa2Psk)]
    [DataRow("wpa2psk", "aes", WifiSecurityType.Wpa2Psk)]
    [DataRow("WPA3ENT192", "GCMP256", WifiSecurityType.Wpa3SuiteB192)]
    [DataRow("WPA3SAE", "AES", WifiSecurityType.Sae)]
    [DataRow("WPA3", "AES", WifiSecurityType.Sae)]
    [DataRow("OWE", "AES", WifiSecurityType.Owe)]
    [DataRow("other", "AES", WifiSecurityType.Unknown)]
    public void Authentication_maps_like_the_scan(string authentication, string encryption, WifiSecurityType expected)
    {
        var xml =
            "<WLANProfile><name>Office</name><SSIDConfig><SSID><name>Cafe</name></SSID></SSIDConfig><connectionMode>manual</connectionMode><MSM><security><authEncryption><authentication>"
            + authentication + "</authentication><encryption>" + encryption +
            "</encryption></authEncryption></security></MSM></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TryParse(xml, out var fields, out var error), error);
        Assert.AreEqual(expected, fields.Security);
        Assert.AreEqual(WifiFields.Security(authentication, encryption), fields.Security);
        Assert.IsFalse(fields.AutoConnect);
        Assert.IsFalse(fields.HasPassword);
        Assert.DoesNotContain(Secret, fields.ToString());
    }

    [TestMethod]
    public void Ssid_prefers_the_name_and_rejects_a_bad_hex_value()
    {
        Assert.AreEqual("Cafe", Ssid("<hex>43616665</hex><name>Cafe</name>"));
        Assert.AreEqual("Cafe", Ssid("<hex>43616665</hex>"));
        Assert.AreEqual("Caf\u00e9", Ssid("<hex>436166C3A9</hex>"));
        Assert.AreEqual("", Ssid("<hex>zzzz</hex>"));
        Assert.AreEqual("", Ssid("<hex>abc</hex>"));
    }

    [TestMethod]
    public void A_mode_write_fails_closed_when_the_element_is_not_one_plain_value()
    {
        const string spaced =
            "<WLANProfile><name>Office</name><connectionMode> auto </connectionMode><Vendor>keep</Vendor></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(spaced, true, out var same, out var changed, out var error),
            error);
        Assert.IsFalse(changed);
        Assert.AreEqual(spaced, same);
        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(spaced, false, out var manual, out changed, out error), error);
        Assert.IsTrue(changed);
        Assert.AreEqual(
            "<WLANProfile><name>Office</name><connectionMode>manual</connectionMode><Vendor>keep</Vendor></WLANProfile>",
            manual);

        const string capital = "<WLANProfile><name>Office</name><connectionMode>Auto</connectionMode></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(capital, true, out same, out changed, out error), error);
        Assert.IsFalse(changed);
        Assert.AreEqual(capital, same);

        const string foo =
            "<WLANProfile><name>Office</name><connectionMode>foo</connectionMode><Vendor>keep</Vendor></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(foo, false, out manual, out changed, out error), error);
        Assert.IsTrue(changed);
        Assert.AreEqual(
            "<WLANProfile><name>Office</name><connectionMode>manual</connectionMode><Vendor>keep</Vendor></WLANProfile>",
            manual);

        const string nested =
            "<WLANProfile><name>Office</name><connectionMode>auto</connectionMode><Vendor><connectionMode>auto</connectionMode><keep>1</keep></Vendor></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(nested, false, out manual, out changed, out error), error);
        Assert.IsTrue(changed);
        Assert.AreEqual(
            "<WLANProfile><name>Office</name><connectionMode>manual</connectionMode><Vendor><connectionMode>auto</connectionMode><keep>1</keep></Vendor></WLANProfile>",
            manual);

        const string empty = "<WLANProfile><name>Office</name><connectionMode></connectionMode></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TrySetAutoConnect(empty, true, out manual, out _, out error), error);
        Assert.AreEqual("<WLANProfile><name>Office</name><connectionMode>auto</connectionMode></WLANProfile>", manual);

        AssertNoWrite(
            "<WLANProfile><name>Office</name><connectionMode>auto</connectionMode><connectionMode>auto</connectionMode></WLANProfile>",
            WifiProfileXml.Rejected);
        AssertNoWrite("<WLANProfile><name>Office</name></WLANProfile>", WifiProfileXml.Rejected, true);
        AssertNoWrite("<WLANProfile><name>Office</name><connectionMode/></WLANProfile>", WifiProfileXml.Rejected, true);
        AssertNoWrite("<WLANProfile><name>Office</name><connectionMode><![CDATA[auto]]></connectionMode></WLANProfile>",
            WifiProfileXml.Rejected);
    }

    [TestMethod]
    [DataRow(0x6u, 0x2u)]
    [DataRow(0x4u, 0x0u)]
    [DataRow(0x1u, 0x1u)]
    [DataRow(0x3u, 0x3u)]
    [DataRow(0x0u, 0x0u)]
    public void Profile_write_flags_keep_the_user_and_group_bits_and_drop_a_plaintext_bit(uint reported,
        uint written)
    {
        Assert.AreEqual(written, Wlan.ProfileWriteFlags(reported));
    }

    [TestMethod]
    public void Profile_reads_request_no_plaintext_key()
    {
        Assert.AreEqual(0u, Wlan.ProfileReadFlags());
    }

    [TestMethod]
    public void A_loaded_profile_matches_its_ssid_and_a_failed_read_matches_its_name()
    {
        var loaded = WifiProfiles.Describe(AdapterId, "Office", Saved, 0);
        Assert.AreEqual(AdapterId + ":Office", loaded.Id);
        Assert.IsTrue(loaded.Loaded);
        Assert.AreEqual("Cafe", loaded.Ssid);
        Assert.IsTrue(loaded.HasPassword);
        Assert.IsTrue(loaded.Hidden);
        Assert.AreEqual("", loaded.Error);
        Assert.IsTrue(WifiProfiles.Matches(loaded, "Cafe"));
        Assert.IsFalse(WifiProfiles.Matches(loaded, "Office"));
        Assert.DoesNotContain(Secret, loaded.ToString());

        var failed = WifiProfiles.Describe(AdapterId, "Cafe", Saved, 5);
        Assert.IsFalse(failed.Loaded);
        Assert.AreEqual("", failed.Ssid);
        Assert.IsFalse(failed.HasPassword);
        Assert.AreEqual(new Win32Exception(5).Message, failed.Error);
        Assert.DoesNotContain(Secret, failed.Error);
        Assert.DoesNotContain("<", failed.Error);
        Assert.IsTrue(WifiProfiles.Matches(failed, "Cafe"));
        Assert.IsFalse(WifiProfiles.Matches(failed, "Office"));

        var unread = WifiProfiles.Describe(AdapterId, "Cafe", null, 0);
        Assert.AreEqual(WifiProfileXml.Unreadable, unread.Error);
        Assert.IsTrue(WifiProfiles.Matches(unread, "Cafe"));
    }

    [TestMethod]
    public async Task Profile_writes_run_one_at_a_time()
    {
        var queue = new ProfileWriteQueue();
        using var hold = new ManualResetEventSlim();
        var firstInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = new StrongBox<int>(0);
        var max = new StrongBox<int>(0);

        var first = queue.RunAsync(() => Enter(firstInside));
        await firstInside.Task;
        var second = queue.RunAsync(() => Enter(secondInside));
        Assert.AreEqual(1, Volatile.Read(ref inFlight.Value));
        hold.Set();
        Assert.AreEqual(1, await first);
        await secondInside.Task;
        Assert.AreEqual(1, await second);
        Assert.AreEqual(1, max.Value);

        int Enter(TaskCompletionSource inside)
        {
            var now = Interlocked.Increment(ref inFlight.Value);
            int seen;
            do
            {
                seen = Volatile.Read(ref max.Value);
            } while (now > seen && Interlocked.CompareExchange(ref max.Value, now, seen) != seen);

            inside.SetResult();
            hold.Wait(CurrentTest.Cancellation);
            Interlocked.Decrement(ref inFlight.Value);
            return now;
        }
    }

    private static void AssertNoWrite(string xml, string expected, bool auto = false)
    {
        Assert.IsFalse(WifiProfileXml.TrySetAutoConnect(xml, auto, out var updated, out var changed, out var error));
        Assert.IsFalse(changed);
        Assert.AreEqual(expected, error);
        Assert.AreEqual(xml, updated);
        Assert.DoesNotContain(Secret, error);
        Assert.DoesNotContain(Marker, error);
    }

    private static string Deep(int levels)
    {
        var builder = new StringBuilder("<WLANProfile><name>Office</name>");
        for (var i = 0; i < levels; i++)
            builder.Append("<e>");
        builder.Append('x');
        for (var i = 0; i < levels; i++)
            builder.Append("</e>");
        builder.Append("</WLANProfile>");
        return builder.ToString();
    }

    private static string Ssid(string inner)
    {
        var xml = "<WLANProfile><name>Office</name><SSIDConfig><SSID>" + inner +
                  "</SSID></SSIDConfig><connectionMode>auto</connectionMode></WLANProfile>";
        Assert.IsTrue(WifiProfileXml.TryParse(xml, out var fields, out var error), error);
        Assert.IsTrue(fields.Name.Length > 0);
        return fields.Ssid;
    }
}
