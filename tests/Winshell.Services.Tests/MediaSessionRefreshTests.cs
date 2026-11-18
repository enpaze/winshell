using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Media.Control;
using Winshell.Media;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class MediaSessionRefreshTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void A_hung_discovery_is_cancelled_and_closed_before_exactly_one_retry(bool completesOnCancel)
    {
        var loop = new ManualEventLoop();
        var requests = 0;
        var pending = new Discovery { CompletesOnCancel = completesOnCancel };
        var next = new Discovery();
        _ = new SmtcSessions(loop, new Log(), () =>
        {
            if (++requests == 1)
                return pending;
            Assert.IsTrue(pending.Closed);
            return next;
        });

        loop.Process();
        loop.Advance(TimeSpan.FromMilliseconds(29999));

        Assert.AreEqual(1, requests);
        Assert.AreEqual(0, pending.CancelCount);
        Assert.IsFalse(pending.Closed);

        loop.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(1, requests);
        Assert.AreEqual(1, pending.CancelCount);
        Assert.AreEqual(1, pending.CloseCount);
        Assert.AreEqual(TimeSpan.FromSeconds(32), loop.NextDue);

        loop.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.AreEqual(1, requests);
        loop.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(2, requests);
        Assert.IsFalse(next.Closed);

        pending.NotifyCompletion(AsyncStatus.Completed);
        loop.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual(2, requests);
        Assert.AreEqual(1, pending.CloseCount);
        Assert.AreEqual(0, next.CancelCount);
        Assert.IsFalse(next.Closed);
        Assert.AreEqual(TimeSpan.FromSeconds(92), loop.NextDue);
    }

    [TestMethod]
    public void A_broker_that_stops_answering_is_logged_once_not_at_every_timeout()
    {
        var loop = new ManualEventLoop();
        List<string> warnings = [];
        var log = new Log { Sink = (level, category, message) => warnings.Add($"{level} {category}: {message}") };
        _ = new SmtcSessions(loop, log, () => new Discovery { CompletesOnCancel = false });
        loop.Process();

        loop.Advance(TimeSpan.FromSeconds(29));
        Assert.IsEmpty(warnings);

        loop.Advance(TimeSpan.FromSeconds(1 + 2 + 60 + 2 + 120));

        var warning = Assert.ContainsSingle(warnings);
        Assert.StartsWith("Warn media: ", warning, StringComparison.Ordinal);
        Assert.Contains("not answering", warning, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Each_timeout_doubles_the_next_wait_up_to_an_hour_until_Windows_answers()
    {
        var loop = new ManualEventLoop();
        var starts = new List<TimeSpan>();
        Discovery? last = null;
        _ = new SmtcSessions(loop, new Log(), () =>
        {
            starts.Add(loop.Now);
            return last = new Discovery { CompletesOnCancel = false };
        });
        loop.Process();

        loop.Advance(TimeSpan.FromSeconds(11028));

        Assert.AreSequenceEqual<int>([32, 62, 122, 242, 482, 962, 1922, 3602, 3602],
            [.. starts.Zip(starts.Skip(1), (a, b) => (int)(b - a).TotalSeconds)]);

        var answered = loop.Now;
        last!.Finish(AsyncStatus.Error);
        loop.Advance(TimeSpan.FromSeconds(34));

        Assert.AreSequenceEqual([answered + TimeSpan.FromSeconds(2), answered + TimeSpan.FromSeconds(34)], starts[^2..]);
    }

    [TestMethod]
    [DataRow(AsyncStatus.Completed)]
    [DataRow(AsyncStatus.Error)]
    [DataRow(AsyncStatus.Canceled)]
    public void A_finished_discovery_releases_its_resources_and_retries_after_two_seconds(AsyncStatus status)
    {
        var loop = new ManualEventLoop();
        var requests = 0;
        var first = new Discovery();
        var next = new Discovery();
        _ = new SmtcSessions(loop, new Log(), () => ++requests == 1 ? first : next);
        loop.Process();

        first.Finish(status);
        loop.Process();

        Assert.IsTrue(first.Closed);
        loop.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.AreEqual(1, requests);
        loop.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(2, requests);
        Assert.IsFalse(next.Closed);
        Assert.AreEqual(TimeSpan.FromSeconds(32), loop.NextDue);
    }

    [TestMethod]
    public void A_request_that_cannot_start_is_retried_after_two_seconds()
    {
        var loop = new ManualEventLoop();
        var requests = 0;
        var pending = new Discovery();
        _ = new SmtcSessions(loop, new Log(), () => ++requests == 1
            ? throw new COMException("The media broker is unavailable.")
            : pending);

        loop.Process();
        loop.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.AreEqual(1, requests);
        loop.Advance(TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(2, requests);
        Assert.AreEqual(TimeSpan.FromSeconds(32), loop.NextDue);
    }

    [TestMethod]
    public void A_disposed_list_asks_Windows_no_more()
    {
        var loop = new ManualEventLoop();
        var requests = 0;
        var first = new Discovery();
        var sessions = new SmtcSessions(loop, new Log(), () => ++requests == 1 ? first : new Discovery());
        loop.Process();
        first.Finish(AsyncStatus.Error);
        loop.Process();
        Assert.AreEqual(TimeSpan.FromSeconds(2), loop.NextDue);

        sessions.Dispose();
        loop.Advance(TimeSpan.FromHours(2));

        Assert.AreEqual(1, requests);
        Assert.IsNull(loop.NextDue);
    }

    [TestMethod]
    public void A_disposed_list_cancels_and_closes_the_request_that_is_out()
    {
        var loop = new ManualEventLoop();
        var pending = new Discovery { CompletesOnCancel = false };
        var sessions = new SmtcSessions(loop, new Log(), () => pending);
        loop.Process();

        sessions.Dispose();

        Assert.AreEqual(1, pending.CancelCount);
        Assert.AreEqual(1, pending.CloseCount);
    }

    private sealed class Discovery : IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager>
    {
        public AsyncOperationCompletedHandler<GlobalSystemMediaTransportControlsSessionManager> Completed
        {
            get => field ?? throw new InvalidOperationException("No completion handler.");
            set
            {
                field = value;
                if (Status != AsyncStatus.Started)
                    value(this, Status);
            }
        }

        public Exception ErrorCode { get; } = new COMException("The media broker is unavailable.");

        public uint Id => 1;

        public AsyncStatus Status { get; private set; } = AsyncStatus.Started;

        public bool CompletesOnCancel { get; init; } = true;

        public int CancelCount { get; private set; }

        public int CloseCount { get; private set; }

        public bool Closed => CloseCount > 0;

        public GlobalSystemMediaTransportControlsSessionManager GetResults()
        {
            throw ErrorCode;
        }

        public void Cancel()
        {
            CancelCount++;
            Status = AsyncStatus.Canceled;
            if (CompletesOnCancel)
                NotifyCompletion(AsyncStatus.Canceled);
        }

        public void Close()
        {
            CloseCount++;
        }

        public void Finish(AsyncStatus status)
        {
            Status = status;
            NotifyCompletion(status);
        }

        public void NotifyCompletion(AsyncStatus status)
        {
            Completed(this, status);
        }
    }
}
