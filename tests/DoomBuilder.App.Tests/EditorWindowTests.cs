using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Editing;
using Xunit;
using DoomBuilder.App.Input;

namespace DoomBuilder.App.Tests;

/// <summary>
/// Drives the real MainWindow with synthetic keyboard and mouse input and checks what the editor does: Avalonia event ->
/// KeyMap -> InputDispatcher -> ActionManager -> edit mode -> Renderer2D. No GPU needed (the GL context never exists, the
/// backend just queues its work), so this runs on every CI platform.
/// </summary>
public class EditorWindowTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_sample_map_opens_in_the_viewer_mode_and_is_fitted_to_the_window()
    {
        OpenEditor();

        Assert.IsAssignableFrom<ClassicMode>(General.Editing.Mode);
        Assert.Equal(16, General.Map.Map.Vertices.Count);
        Assert.True(Renderer.Scale > 0);
    }

    [AvaloniaFact]
    public void The_wheel_zooms_in_and_out()
    {
        OpenEditor();
        var over = ViewCenter();
        window.MouseMove(over);
        float start = Renderer.Scale;

        window.MouseWheel(over, new Vector(0, 1));
        float zoomedin = Renderer.Scale;
        Assert.True(zoomedin > start, $"zoom in: {start} -> {zoomedin}");

        window.MouseWheel(over, new Vector(0, -1));
        window.MouseWheel(over, new Vector(0, -1));
        Assert.True(Renderer.Scale < zoomedin, $"zoom out: {zoomedin} -> {Renderer.Scale}");
    }

    [AvaloniaFact]
    public void Arrow_keys_scroll_the_view_and_Home_fits_the_map_again()
    {
        OpenEditor();
        window.MouseMove(ViewCenter());   // the display must have the keyboard

        // The Help docker appears after the map opens and narrows the display: fit to the size it has now
        window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        window.KeyRelease(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        float fitted = Renderer.TranslateX;

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        float scrolled = Renderer.TranslateX;
        Assert.NotEqual(fitted, scrolled);

        window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        window.KeyRelease(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        Assert.Equal(fitted, Renderer.TranslateX, 3);
    }

    [AvaloniaFact]
    public void The_mouse_position_reaches_the_edit_mode_in_view_pixels()
    {
        OpenEditor();
        var mode = (ClassicMode)General.Editing.Mode;

        window.MouseMove(InView(120, 80));

        Assert.True(General.Interface.MouseInDisplay);
        Assert.Equal(120, mode.MousePos.x, 1);
        Assert.Equal(80, mode.MousePos.y, 1);
    }

    [AvaloniaFact]
    public void Modifier_state_follows_the_keyboard_and_clears_when_the_window_loses_focus()
    {
        OpenEditor();
        window.MouseMove(InView(50, 50));

        window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft, null);
        Assert.True(General.Interface.CtrlState);
        window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.ControlLeft, null);
        Assert.False(General.Interface.CtrlState);
    }
}

public class MouseCaptureTests : IDisposable
{
    private sealed class FakeWarp : IPointerWarp
    {
        public readonly System.Collections.Generic.List<(int x, int y)> Moves = new();
        public bool Supported => true;
        public void MoveTo(int x, int y) => Moves.Add((x, y));
    }

    private sealed class FakeRaw : DoomBuilder.App.Input.IRelativeMotionSource
    {
        public CodeImp.DoomBuilder.Geometry.Vector2D Next;
        public CodeImp.DoomBuilder.Geometry.Vector2D Poll() { var d = Next; Next = new CodeImp.DoomBuilder.Geometry.Vector2D(); return d; }
        public void Dispose() { }
    }

    private MainWindow window;

    public void Dispose() => window?.Close();

    [AvaloniaFact]
    public void Exclusive_mode_reports_relative_movement_and_recenters_the_pointer()
    {
        window = new MainWindow(startEditor: false) { Width = 600, Height = 400 };   // only the viewport and input wiring are under test
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var viewport = window.FindControl<MapViewport>("Viewport");
        var surface = window.FindControl<Avalonia.Controls.Panel>("InputSurface");
        viewport.InputSurface = surface;
        var warp = new FakeWarp();

        using var capture = new DoomBuilder.App.Input.ViewportMouseCapture(viewport, warp);
        Assert.NotNull(surface.Cursor);      // hidden where the pointer is received (the panel over the GL view)
        Assert.NotNull(window.Cursor);       // ... and in the rest of the window, where it drifts to when it cannot be moved back
        Assert.Same(surface.Cursor, window.Cursor);

        // starting a capture puts the pointer in the middle of the view
        Assert.Single(warp.Moves);
        var center = new Point(viewport.Bounds.Width / 2, viewport.Bounds.Height / 2);

        window.MouseMove(surface.TranslatePoint(center + new Point(12, -5), window).Value);
        var delta = capture.Poll();

        Assert.Equal(12, delta.x, 1);
        Assert.Equal(-5, delta.y, 1);
        Assert.Single(warp.Moves);                       // a small move away from the center needs no recentering

        // moved far away: the pointer is brought back, and the jump the warp causes adds nothing
        window.MouseMove(surface.TranslatePoint(center + new Point(250, 0), window).Value);
        Assert.Equal(238, capture.Poll().x, 1);
        Assert.Equal(2, warp.Moves.Count);
        window.MouseMove(surface.TranslatePoint(center, window).Value);
        Assert.Equal(0, capture.Poll().x, 3);
        Assert.Equal(2, warp.Moves.Count);
    }

    [AvaloniaFact]
    public void With_raw_motion_the_pointer_is_put_back_in_the_middle_now_and_then_so_it_stays_in_the_view()
    {
        window = new MainWindow(startEditor: false) { Width = 600, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var viewport = window.FindControl<MapViewport>("Viewport");
        viewport.InputSurface = window.FindControl<Avalonia.Controls.Panel>("InputSurface");
        var warp = new FakeWarp();
        var raw = new FakeRaw();

        using var capture = new DoomBuilder.App.Input.ViewportMouseCapture(viewport, warp, raw);
        Assert.Single(warp.Moves);                       // put in the middle when the capture starts

        raw.Next = new CodeImp.DoomBuilder.Geometry.Vector2D(10, -4);
        var delta = capture.Poll();
        Assert.Equal(10, delta.x, 3);                    // the camera gets the device movement as it is
        Assert.Equal(-4, delta.y, 3);
        Assert.Single(warp.Moves);                       // a little movement: no need to move the pointer

        raw.Next = new CodeImp.DoomBuilder.Geometry.Vector2D(20, 0);   // 30 in total since it was in the middle
        Assert.Equal(20, capture.Poll().x, 3);
        Assert.Equal(2, warp.Moves.Count);               // brought back before it can leave the window
        Assert.Equal(warp.Moves[0], warp.Moves[1]);      // to the same point as at the start

        Assert.Equal(0, capture.Poll().x, 3);            // standing still: nothing to do
        Assert.Equal(2, warp.Moves.Count);
    }
}
