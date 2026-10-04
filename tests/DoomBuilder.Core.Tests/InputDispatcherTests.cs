using System;
using System.IO;
using System.Windows.Forms;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>
/// Keyboard/mouse dispatch end to end: events go through the InputDispatcher into the real ActionManager and into the active
/// edit mode of a real (headless) editor with a map open.
/// </summary>
[Collection("General static state")]
public class InputDispatcherTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-input-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();
    private readonly ActionProbe actions = new ActionProbe();
    private readonly FakeInputHost host = new FakeInputHost();
    private readonly InputDispatcher input;
    private ProbeMode mode => ProbeMode.Current;

    public InputDispatcherTests()
    {
        Directory.CreateDirectory(dir);
        string wad = MapFiles.WriteSquareRoomWad(dir);

        General.BuiltInPluginAssemblies.Add(typeof(ProbePlug).Assembly);
        Assert.True(General.Startup(new[] { wad, "-map", "MAP01", "-cfg", "Doom_DoomDoom.cfg", "-nosettings" },
                                    () => new HeadlessMainWindow(), appdir, dir));

        // Defaults are applied while the plugins register their actions; ours are bound afterwards
        General.Actions.BindMethods(actions);
        General.MainWindow.PerformAutoMapLoading();

        input = new InputDispatcher(host);
        Assert.IsType<ProbeMode>(General.Editing.Mode);
    }

    public void Dispose()
    {
        General.Actions?.UnbindMethods(actions);
        General.ShutdownHeadless();
        General.BuiltInPluginAssemblies.Clear();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        if (Directory.Exists(appdir)) Directory.Delete(appdir, true);
    }

    [Fact]
    public void A_bound_key_begins_on_press_and_ends_on_release()
    {
        Assert.True(input.KeyDown(Keys.F24));
        Assert.Equal(new[] { "testkey begin" }, actions.Log);

        input.KeyUp(Keys.F24);
        Assert.Equal(new[] { "testkey begin", "testkey end" }, actions.Log);
    }

    [Fact]
    public void An_unbound_key_is_not_handled_but_still_reaches_the_edit_mode()
    {
        Assert.False(input.KeyDown(Keys.F23));
        Assert.Empty(actions.Log);
        Assert.Contains("keydown F23", mode.Events);
    }

    [Fact]
    public void Shortcuts_with_modifiers_only_fire_with_those_modifiers()
    {
        input.KeyDown(Keys.K);                          // plain K: nothing bound
        input.KeyUp(Keys.K);
        Assert.Empty(actions.Log);

        input.KeyDown(Keys.K | Keys.Control);
        Assert.Equal(new[] { "testctrlkey begin" }, actions.Log);
        Assert.True(input.CtrlState);
        Assert.False(input.ShiftState);

        input.KeyUp(Keys.K | Keys.Control);
        Assert.Equal("testctrlkey end", actions.Log[^1]);
    }

    [Fact]
    public void Releasing_the_modifier_first_ends_the_action()
    {
        input.KeyDown(Keys.K | Keys.Control);
        input.KeyUp(Keys.ControlKey);                   // Ctrl released while K is still down
        Assert.Contains("testctrlkey end", actions.Log);
        Assert.False(input.CtrlState);
    }

    [Fact]
    public void Mouse_buttons_are_actions_too_and_reach_the_mode()
    {
        input.MouseDown(new MouseEventArgs(MouseButtons.Middle, 1, 30, 40, 0));
        Assert.Equal(MouseButtons.Middle, input.MouseButtons);
        Assert.Equal("testmiddle begin", actions.Log[^1]);
        Assert.Contains("down Middle 30,40", mode.Events);

        input.MouseUp(new MouseEventArgs(MouseButtons.Middle, 1, 30, 40, 0));
        Assert.Equal(MouseButtons.None, input.MouseButtons);
        Assert.Equal("testmiddle end", actions.Log[^1]);
        Assert.Contains("up Middle 30,40", mode.Events);
    }

    [Fact]
    public void Wheel_becomes_a_scroll_key_pressed_and_released_at_once()
    {
        input.KeyDown(Keys.Menu | Keys.Control | Keys.Alt);     // hold Ctrl+Alt: plain wheel is the zoom action
        input.Wheel(120);
        Assert.Equal(new[] { "testscrollup begin", "testscrollup end" }, actions.Log);
    }

    [Fact]
    public void Wheel_is_ignored_while_a_modal_dialog_is_open()
    {
        host.CanProcess = false;
        input.KeyDown(Keys.Menu | Keys.Control | Keys.Alt);
        input.Wheel(120);
        Assert.Empty(actions.Log);
    }

    [Fact]
    public void Mouse_move_enter_leave_click_reach_the_mode_with_their_arguments()
    {
        input.MouseEnter(EventArgs.Empty);
        Assert.True(input.MouseInDisplay);
        input.MouseMove(new MouseEventArgs(MouseButtons.None, 0, 5, 6, 0));
        input.MouseClick(new MouseEventArgs(MouseButtons.Left, 1, 5, 6, 0));
        input.MouseDoubleClick(new MouseEventArgs(MouseButtons.Left, 2, 5, 6, 0));
        input.MouseLeave(EventArgs.Empty);
        Assert.False(input.MouseInDisplay);

        Assert.Equal(new[] { "enter", "move 5,6", "click Left", "doubleclick Left", "leave" }, mode.Events);
    }

    [Fact]
    public void ReleaseAllKeys_ends_active_actions_and_clears_the_state()
    {
        input.KeyDown(Keys.F24 | Keys.Shift);
        input.MouseDown(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));

        input.ReleaseAllKeys();

        Assert.False(input.ShiftState);
        Assert.Equal(MouseButtons.None, input.MouseButtons);
    }

    [Fact]
    public void Exclusive_mouse_captures_once_and_reports_scaled_relative_movement_to_the_mode()
    {
        input.StartExclusiveMouseInput();
        input.StartExclusiveMouseInput();               // already exclusive: nothing happens
        Assert.True(input.MouseExclusive);
        Assert.Equal(1, host.CapturesStarted);

        // while captured, absolute pointer events are not forwarded to the mode
        mode.Events.Clear();
        input.MouseMove(new MouseEventArgs(MouseButtons.None, 0, 1, 1, 0));
        Assert.Empty(mode.Events);

        host.NextPoll = new Vector2D(10, -4);
        input.Tick();

        double sens = General.Settings.MouseSpeed * 0.01;
        Assert.Single(mode.MouseInputs);
        Assert.Equal(10 * General.Settings.VisualMouseSensX * sens, mode.MouseInputs[0].x, 4);
        Assert.Equal(-4 * General.Settings.VisualMouseSensY * sens, mode.MouseInputs[0].y, 4);

        input.StopExclusiveMouseInput();
        Assert.False(input.MouseExclusive);
        Assert.Equal(1, host.CapturesReleased);
    }

    [Fact]
    public void Breaking_exclusive_mode_releases_the_mouse_until_every_break_is_resumed()
    {
        input.StartExclusiveMouseInput();

        input.BreakExclusiveMouseInput();
        input.BreakExclusiveMouseInput();
        Assert.Equal(1, host.CapturesReleased);

        input.ResumeExclusiveMouseInput();
        Assert.Equal(1, host.CapturesStarted);          // still one break pending
        input.ResumeExclusiveMouseInput();
        Assert.Equal(2, host.CapturesStarted);          // all resumed: captured again
    }

    [Fact]
    public void Processing_is_reference_counted_and_ticks_the_mode()
    {
        input.EnableProcessing();
        input.EnableProcessing();
        Assert.Equal(new[] { true }, host.ProcessingStates);   // only the first enable starts the timer

        input.Tick();
        Assert.Equal(1, mode.ProcessCalls);

        input.DisableProcessing();
        Assert.Equal(new[] { true }, host.ProcessingStates);   // still one user
        input.DisableProcessing();
        Assert.Equal(new[] { true, false }, host.ProcessingStates);

        input.DisableProcessing();                              // never below zero
        Assert.Equal(0, input.ProcessingCount);
    }
}
