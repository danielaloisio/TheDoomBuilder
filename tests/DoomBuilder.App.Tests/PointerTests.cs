using System;
using DoomBuilder.App.Input;
using Xunit;

namespace DoomBuilder.App.Tests;

public class RelativePointerTrackerTests
{
    private static RelativePointerTracker Tracker(Func<long> clock = null)
    {
        var tracker = new RelativePointerTracker(clock ?? (() => 0));
        tracker.SetCenter(400, 300);
        tracker.Feed(400, 300);                    // the capture starts with the pointer on the center
        return tracker;
    }

    [Fact]
    public void Movement_is_the_difference_between_positions_and_a_small_move_needs_no_warp()
    {
        var tracker = Tracker();

        Assert.False(tracker.Feed(410, 297));      // near the center: nothing to bring back
        var delta = tracker.Poll();

        Assert.Equal(10, delta.x);
        Assert.Equal(-3, delta.y);
    }

    [Fact]
    public void Several_movements_between_polls_add_up_and_poll_resets()
    {
        var tracker = Tracker();

        tracker.Feed(403, 301);
        tracker.Feed(402, 305);
        var first = tracker.Poll();
        var second = tracker.Poll();

        Assert.Equal(2, first.x);
        Assert.Equal(5, first.y);
        Assert.Equal(0, second.x);
        Assert.Equal(0, second.y);
    }

    [Fact]
    public void A_pointer_that_wandered_far_is_brought_back_and_the_jump_is_not_a_movement()
    {
        long now = 0;
        var tracker = Tracker(() => now);

        // walking away from the center in steps of 20 px until it is far: that asks for a warp
        bool warp = false;
        double x = 400;
        while (!warp) { x += 20; warp = tracker.Feed(x, 300); }
        double walked = x - 400;
        Assert.Equal(walked, tracker.Poll().x, 3);

        now += 3;
        Assert.False(tracker.Feed(401, 301));      // the pointer arrives at the center (a little off): the warp's own jump
        Assert.Equal(0, tracker.Poll().x, 3);

        Assert.False(tracker.Feed(405, 301));      // and it goes on from there
        Assert.Equal(4, tracker.Poll().x, 3);
    }

    [Fact]
    public void The_jump_may_land_anywhere_near_the_center_and_still_is_skipped_without_looping()
    {
        long now = 0;
        var tracker = Tracker(() => now);
        double turned = 0;

        // a thousand times: move away until a warp is asked for, and the warp lands 9 px off the center (display scaling, rounding)
        for (int i = 0; i < 1000; i++)
        {
            double x = 409, y = 309;
            bool warp = false;
            while (!warp) { x += 25; now += 2; warp = tracker.Feed(x, y); }
            turned += tracker.Poll().x;
            now += 2;
            Assert.False(tracker.Feed(409, 309));  // the warp's jump, off the center
            Assert.Equal(0, tracker.Poll().x, 3);
        }

        Assert.True(tracker.UsesWarp);
        Assert.InRange(turned, 1000 * 100, 1000 * 400);     // only real movement: a few hundred px each time, not growing without a mouse
    }

    [Fact]
    public void Where_the_warp_does_nothing_the_tracker_gives_up_warping_and_the_view_does_not_spin()
    {
        // XWayland ignores the warp: the pointer stays where the user left it, far from the center
        long now = 0;
        var tracker = new RelativePointerTracker(() => now);
        tracker.SetCenter(400, 300);
        bool gaveup = false;
        tracker.WarpGivenUp += () => gaveup = true;
        tracker.Feed(900, 100);                    // first position: far, not a movement

        double total = 0;
        for (int i = 0; i < 300; i++)
        {
            now += 100;                            // the warps (if asked) are never answered
            tracker.Feed(900 + i, 100);            // slow movement of 1 px per event
            total += tracker.Poll().x;
        }

        Assert.True(gaveup);
        Assert.False(tracker.UsesWarp);
        Assert.Equal(299, total, 3);               // exactly what the pointer travelled
    }

    [Fact]
    public void A_pointer_that_cannot_be_warped_at_all_moves_by_differences_and_never_asks()
    {
        var tracker = new RelativePointerTracker { UsesWarp = false };
        tracker.SetCenter(400, 300);

        Assert.False(tracker.Feed(900, 100));
        Assert.False(tracker.Feed(905, 98));
        Assert.False(tracker.Feed(905, 98));       // resting: nothing
        var delta = tracker.Poll();

        Assert.Equal(5, delta.x, 3);
        Assert.Equal(-2, delta.y, 3);
    }

    [Fact]
    public void A_fast_flick_toward_the_center_is_not_mistaken_for_a_warp_when_none_was_asked()
    {
        var tracker = Tracker();

        tracker.Feed(420, 300);
        Assert.Equal(20, tracker.Poll().x, 3);
        tracker.Feed(380, 300);                    // 40 px back, across the center, without any warp pending
        Assert.Equal(-40, tracker.Poll().x, 3);
    }

    [Fact]
    public void Moving_the_center_keeps_later_deltas_correct()
    {
        var tracker = Tracker();
        tracker.SetCenter(200, 100);               // the view was resized

        tracker.Feed(405, 300);
        Assert.Equal(5, tracker.Poll().x);
    }
}

public class PointerWarpTests
{
    [Fact]
    public void The_factory_always_returns_something_usable()
    {
        IPointerWarp warp = PointerWarp.Create();
        Assert.NotNull(warp);
        warp.MoveTo(0, 0);                       // must not throw, even where warping is unsupported
    }

    [Fact]
    public void X11_warp_moves_the_pointer_where_the_server_reports_it()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "X11 only");
        using var warp = X11PointerWarp.TryCreate();
        Assert.SkipWhen(warp == null, "No X server (DISPLAY) to talk to");

        warp.MoveTo(321, 123);
        var (x, y) = warp.QueryPosition();

        Assert.Equal((321, 123), (x, y));
    }
}