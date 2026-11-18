using Winshell.Apps;
using Winshell.Media;

namespace Winshell.Services.Tests;

[TestClass]
public class MediaAppNameTests
{
    [TestMethod]
    public void An_app_is_named_once_off_the_ui_thread()
    {
        var loop = new ManualEventLoop();
        using var lookupMayEnd = new ManualResetEventSlim();
        var lookups = 0;
        var lookupThread = 0;
        var named = 0;
        var names = new AppNames(loop, new Log(), _ =>
        {
            Interlocked.Increment(ref lookups);
            lookupThread = Environment.CurrentManagedThreadId;
            lookupMayEnd.Wait(TimeSpan.FromSeconds(10), CurrentTest.Cancellation);
            return "Spotify Music";
        }, () => named++);

        Assert.AreEqual("Spotify", names.Get("Spotify.exe"));
        Assert.AreEqual("Spotify", names.Get("Spotify.exe"));
        lookupMayEnd.Set();
        Wait.Until(() => named > 0, "the app name", loop.Process);

        Assert.AreEqual("Spotify Music", names.Get("Spotify.exe"));
        Assert.AreEqual(1, lookups);
        Assert.AreEqual(1, named);
        Assert.AreNotEqual(Environment.CurrentManagedThreadId, lookupThread);
    }

    [TestMethod]
    public void A_failed_lookup_keeps_the_fallback_name()
    {
        var loop = new ManualEventLoop();
        var warnings = new List<string>();
        var log = new Log { Sink = (_, category, message) => warnings.Add($"{category}: {message}") };
        var names = new AppNames(loop, log, _ => throw new InvalidOperationException("no shell"), () => { });

        Assert.AreEqual("Player", names.Get("Player.exe"));
        Wait.Until(() => warnings.Count > 0, "the lookup warning", loop.Process);

        Assert.AreEqual("Player", names.Get("Player.exe"));
        Assert.Contains(w => w.Contains("no shell", StringComparison.Ordinal), warnings);
    }

    [TestMethod]
    [DataRow("Spotify.exe", "Spotify")]
    [DataRow("VLC.EXE", "VLC")]
    [DataRow("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic",
        "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic")]
    public void The_fallback_name_is_the_program_without_its_extension_or_else_the_app_id(string appId, string name)
    {
        Assert.AreEqual(name, AppsService.FallbackName(appId));
    }
}
