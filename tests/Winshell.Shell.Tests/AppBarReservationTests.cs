using Winshell.Native;

namespace Winshell.ShellTests;

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

    [Fact]
    public void The_first_request_is_asked_for_and_moves_the_work_area()
    {
        var explorer = new Explorer();
        var reservation = new AppBarReservation();
        Assert.True(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.Equal(Bottom, reservation.Granted);
        Assert.Equal(1, explorer.Requests);
    }

    [Fact]
    public void Placing_the_panel_again_for_the_same_reservation_asks_nothing()
    {
        var explorer = new Explorer();
        var reservation = new AppBarReservation();
        reservation.Reserve(Bottom, explorer.SetPos);
        for (var i = 0; i < 10; i++)
            Assert.False(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.Equal(1, explorer.Requests);
    }

    [Fact]
    public void A_bar_that_took_the_space_in_between_and_left_again_is_negotiated_back()
    {
        var explorer = new Explorer { Other = 40 };
        var reservation = new AppBarReservation();

        Assert.True(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.Equal(AboveOther, reservation.Granted);

        Assert.False(reservation.Reserve(AboveOther, explorer.SetPos));
        Assert.Equal(1, explorer.Requests);

        explorer.Other = 0;
        Assert.True(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.Equal(Bottom, reservation.Granted);
        Assert.Equal(2, explorer.Requests);
    }

    [Fact]
    public void A_grant_that_stays_short_of_the_request_is_not_asked_for_again()
    {
        var explorer = new Explorer { Other = 40 };
        var reservation = new AppBarReservation();
        Assert.True(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.False(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.False(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.Equal(AboveOther, reservation.Granted);
        Assert.Equal(1, explorer.Requests);
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

            Assert.Empty(_callbacks);
            return delivered;
        }
    }

    [Fact]
    public void Callbacks_from_a_grant_that_stays_short_settle_instead_of_asking_forever()
    {
        var offer = Bottom;
        var placement = new Placement(new AppBarReservation(), () => offer) { Grant = _ => AboveOther };
        placement.Place();
        Assert.True(placement.Settle() <= 1);
        Assert.Equal(1, placement.SetPosCalls);

        offer = new RECT(0, 1000, 1920, 1048);
        placement.Grant = asked => asked;
        placement.Place();
        placement.Settle();
        Assert.Equal(2, placement.SetPosCalls);
    }

    [Fact]
    public void Callbacks_settle_while_another_bar_comes_and_goes_and_the_edge_is_won_back()
    {
        var reservation = new AppBarReservation();
        var other = 0;
        var placement = new Placement(reservation, () => other == 0 ? Bottom : AboveOther);
        placement.Place();
        placement.Settle();
        Assert.Equal(Bottom, reservation.Granted);

        other = 40;
        placement.Place();
        placement.Settle();
        Assert.Equal(AboveOther, reservation.Granted);

        other = 0;
        placement.Place();
        placement.Settle();
        Assert.Equal(Bottom, reservation.Granted);
        Assert.Equal(3, placement.SetPosCalls);
    }

    [Fact]
    public void A_bar_that_leaves_before_the_short_grant_is_observed_is_asked_for_again()
    {
        var other = 40;
        var reservation = new AppBarReservation();
        var placement = new Placement(reservation, () => Bottom)
        {
            Grant = asked => other == 0 ? asked : AboveOther
        };
        placement.Place();
        Assert.Equal(AboveOther, reservation.Granted);
        Assert.Equal(1, placement.SetPosCalls);

        other = 0;
        reservation.NoteExternalChange();
        placement.Settle();
        Assert.Equal(Bottom, reservation.Granted);
        Assert.Equal(2, placement.SetPosCalls);
        Assert.Equal(0, placement.Settle());
        Assert.Equal(2, placement.SetPosCalls);
    }

    [Fact]
    public void Each_outside_change_asks_once_more_for_a_grant_that_stays_short_and_then_stands()
    {
        var reservation = new AppBarReservation();
        var placement = new Placement(reservation, () => Bottom) { Grant = _ => AboveOther };
        placement.Place();
        placement.Settle();
        Assert.Equal(1, placement.SetPosCalls);

        reservation.NoteExternalChange();
        placement.Place();
        placement.Settle();
        Assert.Equal(AboveOther, reservation.Granted);
        Assert.Equal(2, placement.SetPosCalls);

        placement.Place();
        Assert.Equal(0, placement.Settle());
        Assert.Equal(2, placement.SetPosCalls);
    }

    [Fact]
    public void Two_notices_before_the_short_grant_is_placed_again_ask_only_once_more()
    {
        var reservation = new AppBarReservation();
        var placement = new Placement(reservation, () => Bottom) { Grant = _ => AboveOther };
        placement.Place();
        reservation.NoteExternalChange();
        reservation.NoteExternalChange();
        placement.Settle();
        Assert.Equal(AboveOther, reservation.Granted);
        Assert.Equal(2, placement.SetPosCalls);
        Assert.Equal(0, placement.Settle());
        Assert.Equal(2, placement.SetPosCalls);
    }

    [Fact]
    public void After_the_app_bar_is_removed_the_same_rectangle_is_asked_for_again()
    {
        var explorer = new Explorer();
        var reservation = new AppBarReservation();
        reservation.Reserve(Bottom, explorer.SetPos);
        reservation.Clear();
        Assert.True(reservation.Reserve(Bottom, explorer.SetPos));
        Assert.Equal(2, explorer.Requests);
    }
}
