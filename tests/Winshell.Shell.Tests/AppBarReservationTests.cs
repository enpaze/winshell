using Winshell.Native;

namespace Winshell.ShellTests;

[TestClass]
public class AppBarReservationTests
{
    private static readonly RECT Bottom = new(0, 1032, 1920, 1080);
    private static readonly RECT AboveOther = new(0, 992, 1920, 1040);

    private sealed class Explorer
    {
        public int Requests { get; private set; }

        public int Other { get; set; }

        public RECT SetPos(RECT asked)
        {
            Requests++;
            return Other == 0 ? asked : new RECT(asked.left, asked.top - Other, asked.right, asked.bottom - Other);
        }
    }

    [TestMethod]
    public void The_first_request_is_asked_for_and_moves_the_work_area()
    {
        var explorer = new Explorer();
        var reservation = new AppBarReservation();
        Assert.IsTrue(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.AreEqual(Bottom, reservation.Granted);
        Assert.AreEqual(1, explorer.Requests);
    }

    [TestMethod]
    public void Placing_the_panel_again_for_the_same_reservation_asks_nothing()
    {
        var explorer = new Explorer();
        var reservation = new AppBarReservation();
        reservation.Reserve(Bottom, explorer.SetPos);
        for (var i = 0; i < 10; i++)
            Assert.IsFalse(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.AreEqual(1, explorer.Requests);
    }

    [TestMethod]
    public void A_bar_that_took_the_space_in_between_and_left_again_is_negotiated_back()
    {
        var explorer = new Explorer { Other = 40 };
        var reservation = new AppBarReservation();

        Assert.IsTrue(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.AreEqual(AboveOther, reservation.Granted);

        Assert.IsFalse(reservation.Reserve(AboveOther, explorer.SetPos));
        Assert.AreEqual(1, explorer.Requests);

        explorer.Other = 0;
        Assert.IsTrue(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.AreEqual(Bottom, reservation.Granted);
        Assert.AreEqual(2, explorer.Requests);
    }

    [TestMethod]
    public void A_grant_that_stays_short_of_the_request_is_not_asked_for_again()
    {
        var explorer = new Explorer { Other = 40 };
        var reservation = new AppBarReservation();
        Assert.IsTrue(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.IsFalse(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.IsFalse(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.AreEqual(AboveOther, reservation.Granted);
        Assert.AreEqual(1, explorer.Requests);
    }

    private sealed class Placement(AppBarReservation reservation, Func<RECT> query)
    {
        private readonly Queue<Action> _callbacks = new();

        public int SetPosCalls { get; private set; }

        public void Place()
        {
            reservation.Reserve(query(), asked =>
            {
                SetPosCalls++;
                _callbacks.Enqueue(Place);
                return Grant(asked);
            });
        }

        public Func<RECT, RECT> Grant { get; set; } = asked => asked;

        public int Settle(int limit = 1000)
        {
            var delivered = 0;
            while (_callbacks.Count > 0 && delivered < limit)
            {
                _callbacks.Dequeue()();
                delivered++;
            }

            Assert.IsEmpty(_callbacks);
            return delivered;
        }
    }

    [TestMethod]
    public void Callbacks_from_a_grant_that_stays_short_settle_instead_of_asking_forever()
    {
        var offer = Bottom;
        var placement = new Placement(new AppBarReservation(), () => offer) { Grant = _ => AboveOther };
        placement.Place();
        Assert.IsTrue(placement.Settle() <= 1);
        Assert.AreEqual(1, placement.SetPosCalls);

        offer = new RECT(0, 1000, 1920, 1048);
        placement.Grant = asked => asked;
        placement.Place();
        placement.Settle();
        Assert.AreEqual(2, placement.SetPosCalls);
    }

    [TestMethod]
    public void Callbacks_settle_while_another_bar_comes_and_goes_and_the_edge_is_won_back()
    {
        var reservation = new AppBarReservation();
        var other = 0;
        var placement = new Placement(reservation, () => other == 0 ? Bottom : AboveOther);
        placement.Place();
        placement.Settle();
        Assert.AreEqual(Bottom, reservation.Granted);

        other = 40;
        placement.Place();
        placement.Settle();
        Assert.AreEqual(AboveOther, reservation.Granted);

        other = 0;
        placement.Place();
        placement.Settle();
        Assert.AreEqual(Bottom, reservation.Granted);
        Assert.AreEqual(3, placement.SetPosCalls);
    }

    [TestMethod]
    public void A_bar_that_leaves_before_the_short_grant_is_observed_is_asked_for_again()
    {
        var other = 40;
        var reservation = new AppBarReservation();
        var placement = new Placement(reservation, () => Bottom)
        {
            Grant = asked => other == 0 ? asked : AboveOther
        };
        placement.Place();
        Assert.AreEqual(AboveOther, reservation.Granted);
        Assert.AreEqual(1, placement.SetPosCalls);

        other = 0;
        reservation.NoteExternalChange();
        placement.Settle();
        Assert.AreEqual(Bottom, reservation.Granted);
        Assert.AreEqual(2, placement.SetPosCalls);
        Assert.AreEqual(0, placement.Settle());
        Assert.AreEqual(2, placement.SetPosCalls);
    }

    [TestMethod]
    public void Each_outside_change_asks_once_more_for_a_grant_that_stays_short_and_then_stands()
    {
        var reservation = new AppBarReservation();
        var placement = new Placement(reservation, () => Bottom) { Grant = _ => AboveOther };
        placement.Place();
        placement.Settle();
        Assert.AreEqual(1, placement.SetPosCalls);

        reservation.NoteExternalChange();
        placement.Place();
        placement.Settle();
        Assert.AreEqual(AboveOther, reservation.Granted);
        Assert.AreEqual(2, placement.SetPosCalls);

        placement.Place();
        Assert.AreEqual(0, placement.Settle());
        Assert.AreEqual(2, placement.SetPosCalls);
    }

    [TestMethod]
    public void Two_notices_before_the_short_grant_is_placed_again_ask_only_once_more()
    {
        var reservation = new AppBarReservation();
        var placement = new Placement(reservation, () => Bottom) { Grant = _ => AboveOther };
        placement.Place();
        reservation.NoteExternalChange();
        reservation.NoteExternalChange();
        placement.Settle();
        Assert.AreEqual(AboveOther, reservation.Granted);
        Assert.AreEqual(2, placement.SetPosCalls);
        Assert.AreEqual(0, placement.Settle());
        Assert.AreEqual(2, placement.SetPosCalls);
    }

    [TestMethod]
    public void After_the_app_bar_is_removed_the_same_rectangle_is_asked_for_again()
    {
        var explorer = new Explorer();
        var reservation = new AppBarReservation();
        reservation.Reserve(Bottom, explorer.SetPos);
        reservation.Clear();
        Assert.IsTrue(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.AreEqual(2, explorer.Requests);
    }
}
