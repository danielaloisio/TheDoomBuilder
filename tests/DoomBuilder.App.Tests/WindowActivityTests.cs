using DoomBuilder.App.Input;
using Xunit;

namespace DoomBuilder.App.Tests;

public class WindowActivityTests
{
    [Fact]
    public void The_platform_answer_counts()
    {
        var activity = new WindowActivity();
        Assert.False(activity.IsActive(false));
        Assert.True(activity.IsActive(true));
    }

    [Fact]
    public void Activation_and_deactivation_are_remembered_like_the_flag_UDB_keeps()
    {
        var activity = new WindowActivity();
        activity.Activated();
        Assert.True(activity.IsActive(false));
        activity.Deactivated();
        Assert.False(activity.IsActive(false));
    }

    [Fact]
    public void Input_in_the_window_shows_that_it_is_in_use_even_when_the_platform_never_reports_the_activation_back()
    {
        var activity = new WindowActivity();
        activity.Activated();
        activity.Deactivated();                  // a dialog took over and closed again, but no activation came back
        Assert.False(activity.IsActive(false));

        activity.InputReceived();                // the user clicks in the window
        Assert.True(activity.IsActive(false));
    }
}
