using System;
using DoomBuilder.App.Input;
using Xunit;

namespace DoomBuilder.App.Tests;

public class RelativePointerTrackerTests
{
    [Fact]
    public void Movement_is_the_distance_from_the_center_and_asks_for_a_warp()
    {
        var tracker = new RelativePointerTracker();
        tracker.SetCenter(100, 50);

        Assert.True(tracker.Feed(110, 47));      // moved right 10, up 3: warp back to the center
        var delta = tracker.Poll();

        Assert.Equal(10, delta.x);
        Assert.Equal(-3, delta.y);
    }

    [Fact]
    public void The_event_caused_by_the_warp_itself_is_ignored()
    {
        var tracker = new RelativePointerTracker();
        tracker.SetCenter(100, 50);
        tracker.Feed(110, 50);

        Assert.False(tracker.Feed(100, 50));     // pointer is back on the center: no extra movement, no new warp
        Assert.Equal(10, tracker.Poll().x);
    }

    [Fact]
    public void Several_movements_between_polls_add_up_and_poll_resets()
    {
        var tracker = new RelativePointerTracker();
        tracker.SetCenter(0, 0);

        tracker.Feed(3, 1);
        tracker.Feed(-1, 4);
        var first = tracker.Poll();
        var second = tracker.Poll();

        Assert.Equal(2, first.x);
        Assert.Equal(5, first.y);
        Assert.Equal(0, second.x);
        Assert.Equal(0, second.y);
    }

    [Fact]
    public void Moving_the_center_keeps_later_deltas_correct()
    {
        var tracker = new RelativePointerTracker();
        tracker.SetCenter(0, 0);
        tracker.SetCenter(200, 100);             // the view was resized

        tracker.Feed(205, 100);
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
