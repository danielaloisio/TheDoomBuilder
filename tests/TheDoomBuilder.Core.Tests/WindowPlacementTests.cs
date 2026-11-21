using System.Collections.Generic;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class WindowPlacementTests
{
    private static readonly IList<int[]> Screen = new List<int[]> { new[] { 0, 0, 1920, 1080 } };

    [Fact]
    public void A_window_that_fits_stays_where_it_is()
    {
        var fit = new WindowPlacement(100, 80, 1200, 800, false).FitTo(Screen);

        Assert.Equal((100, 80, 1200, 800), (fit.X, fit.Y, fit.Width, fit.Height));
    }

    [Fact]
    public void A_window_on_a_monitor_that_is_gone_moves_onto_the_remaining_one()
    {
        var fit = new WindowPlacement(3000, 200, 1200, 800, false).FitTo(Screen);

        Assert.Equal(1920 - 1200, fit.X);
        Assert.Equal(200, fit.Y);
    }

    [Fact]
    public void A_window_larger_than_the_screen_is_shrunk()
    {
        var fit = new WindowPlacement(-50, -50, 4000, 3000, true).FitTo(Screen);

        Assert.Equal((0, 0, 1920, 1080), (fit.X, fit.Y, fit.Width, fit.Height));
        Assert.True(fit.Maximized);
    }

    [Fact]
    public void The_screen_with_most_of_the_window_wins()
    {
        var two = new List<int[]> { new[] { 0, 0, 1920, 1080 }, new[] { 1920, 0, 1920, 1080 } };

        var fit = new WindowPlacement(2000, 100, 1200, 800, false).FitTo(two);

        Assert.Equal(2000, fit.X);                       // already on the second screen: untouched
    }

    [Fact]
    public void Without_screens_nothing_changes()
    {
        var p = new WindowPlacement(5, 5, 1000, 700, false);

        Assert.Same(p, p.FitTo(new List<int[]>()));
    }
}

[Collection("General static state")]
public class WindowPlacementSettingsTests
{
    [Fact]
    public void A_saved_placement_is_loaded_back_and_nothing_saved_gives_null()
    {
        General.InitializeHeadless(TestAssets.DefaultSettings);
        try
        {
            Assert.Null(WindowPlacement.Load());

            new WindowPlacement(10, 20, 1111, 777, true).Save();
            var loaded = WindowPlacement.Load();

            Assert.Equal((10, 20, 1111, 777, true), (loaded.X, loaded.Y, loaded.Width, loaded.Height, loaded.Maximized));
        }
        finally { General.ShutdownHeadless(); }
    }
}
