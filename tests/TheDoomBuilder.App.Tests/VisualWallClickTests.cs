using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>Clicking walls in the 3D mode with the real pointer events, more than once.</summary>
public class VisualWallClickTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    private static void Tick() => ((DoomBuilder.App.AvaloniaShell)General.Interface).Input.Tick();

    // A square room drawn clockwise: the front of each wall faces in
    private const string Room = @"namespace = ""zdoom"";
vertex { x = 0.0; y = 0.0; }
vertex { x = 0.0; y = 128.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 128.0; y = 0.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sector { heightfloor = 0; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
thing { x = 64.0; y = 64.0; type = 1; angle = 90; skill1 = true; skill2 = true; single = true; }
";

    private void LookAtTheWall()
    {
        var cam = General.Map.VisualCamera;
        cam.Position = new CodeImp.DoomBuilder.Geometry.Vector3D(64, 40, 64);
        cam.AngleXY = 0;                                   // looks along +Y: the wall at y = 128
        cam.AngleZ = Math.PI;                              // level
        Tick();
        System.Threading.Thread.Sleep(120);                // the target is picked every 80 ms
        Tick();
    }

    [AvaloniaFact]
    public void A_right_click_on_a_wall_can_be_repeated_after_the_dialog_closes()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        LookAtTheWall();

        var at = ViewCenter();
        var opened = new System.Collections.Generic.List<string>();
        for (int round = 1; round <= 3; round++)
        {
            string name = null;
            WhenShown<Window>(w => { name = w.GetType().Name; w.Close(false); });
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Right, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Right, RawInputModifiers.None);
            Flush();
            opened.Add(name ?? "(nothing)");
            LookAtTheWall();
        }
        Assert.All(opened, n => Assert.NotEqual("(nothing)", n));
    }

    private static Button Named(Window w, string text)
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<Button>().First(b => b.Content as string == text);

    // The whole flow: right click on the wall, click a texture in the dialog, answer the texture browser, OK, and do it again
    [AvaloniaFact]
    public void Changing_a_wall_texture_through_the_dialog_works_every_time()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        LookAtTheWall();

        var at = ViewCenter();
        var log = new System.Collections.Generic.List<string>();
        for (int round = 1; round <= 3; round++)
        {
            string step = "no dialog";
            WhenShown<DoomBuilder.UI.LinedefEditWindow>(dialog =>
            {
                step = "dialog, browser not opened";
                var selector = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<DoomBuilder.UI.TextureSelector>().First(t => t.IsEffectivelyVisible);
                // The browser is owned by the dialog, not by the main window
                var watch = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
                watch.Tick += (s2, e2) =>
                {
                    var browser = dialog.OwnedWindows.OfType<DoomBuilder.UI.ImageBrowserWindow>().FirstOrDefault();
                    if (browser == null || !browser.IsVisible) return;
                    watch.Stop();
                    step = "browser opened";
                    Click(Named(browser, "Cancel"));
                };
                watch.Start();
                var center = selector.TranslatePoint(new Point(selector.Bounds.Width / 2, 20), dialog) ?? new Point(30, 30);
                dialog.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
                dialog.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
                Click(Named(dialog, "OK"));
            });
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Right, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Right, RawInputModifiers.None);
            Flush();
            log.Add("round " + round + ": " + step);
            LookAtTheWall();
        }
        Assert.All(log, l => Assert.EndsWith("browser opened", l));
    }

    // The texture really changes this time (typed into the dialog), so the wall's geometry is rebuilt before the next click
    [AvaloniaFact]
    public void The_wall_can_be_edited_again_after_its_texture_changed()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        LookAtTheWall();

        var at = ViewCenter();
        var wall = General.Map.Map.Linedefs.First(l => l.Start.Position.y == 128 && l.End.Position.y == 128);
        var seen = new System.Collections.Generic.List<string>();
        foreach (string texture in new[] { "STARTAN2", "STARTAN3", "STARTAN1" })
        {
            string state = "no dialog";
            WhenShown<DoomBuilder.UI.LinedefEditWindow>(dialog =>
            {
                var selector = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<DoomBuilder.UI.TextureSelector>().First(t => t.IsEffectivelyVisible);
                selector.TextureName = texture;
                state = "dialog opened";
                // What the real window does when the dialog takes over: it deactivates (every key is released) and the pointer leaves the view
                var input = ((DoomBuilder.App.AvaloniaShell)General.Interface).Input;
                input.ReleaseAllKeys();
                input.MouseLeave(EventArgs.Empty);
                Click(Named(dialog, "OK"));
            });
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Right, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Right, RawInputModifiers.None);
            Flush();
            seen.Add(texture + ": " + state + " (front is now " + (wall.Front.MiddleTexture + "/" + wall.Front.HighTexture + "/" + wall.Front.LowTexture) + ")");
            LookAtTheWall();
        }
        Assert.All(seen, l => Assert.Contains("dialog opened", l));
    }

    // Two walls of the room, one after the other, with a window system that never says "active" after a dialog (the reported bug)
    private static readonly (double x, double y, double angle)[] Views = { (64, 40, 0), (88, 64, Math.PI / 2 * 3) };

    private void LookAt(int view)
    {
        var cam = General.Map.VisualCamera;
        cam.Position = new CodeImp.DoomBuilder.Geometry.Vector3D(Views[view].x, Views[view].y, 64);
        cam.AngleXY = Views[view].angle;
        cam.AngleZ = Math.PI;
        Tick();
        System.Threading.Thread.Sleep(120);
        Tick();
    }

    [AvaloniaFact]
    public void Another_wall_can_be_edited_when_the_window_system_does_not_report_the_activation_back()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        window.PlatformIsActive = () => false;      // a compositor that does not hand the activation back after a dialog
        window.Activity.Deactivated();              // ... after the dialog took the activation away (the headless window was activated when it opened)
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();

        var at = ViewCenter();
        var seen = new System.Collections.Generic.List<string>();
        for (int view = 0; view < Views.Length; view++)
        {
            LookAt(view);
            string state = "no dialog";
            WhenShown<DoomBuilder.UI.LinedefEditWindow>(dialog => { state = "dialog opened"; Click(Named(dialog, "OK")); });
            window.MouseMove(at);
            window.MouseDown(at, MouseButton.Right, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Right, RawInputModifiers.None);
            Flush();
            seen.Add("wall " + (view + 1) + ": " + state);
        }
        Assert.All(seen, l => Assert.EndsWith("dialog opened", l));
    }

    // A point of the window that is not over the display (the menu bar)
    private Point OutsideTheDisplay()
    {
        var surface = window.FindControl<Avalonia.Controls.Panel>("InputSurface");
        var origin = surface.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
        var outside = new Point(4, 4);
        Assert.True(outside.X < origin.X || outside.Y < origin.Y, "the point is outside of the display");
        return outside;
    }

    // Where the pointer cannot be moved back it wanders out of the display while the user turns the view; the click still counts
    [AvaloniaFact]
    public void A_click_outside_the_display_still_edits_the_target_while_the_3D_view_has_the_mouse()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        Assert.True(General.Interface.MouseExclusive, "the 3D view has the mouse");

        var outside = OutsideTheDisplay();
        var seen = new System.Collections.Generic.List<string>();
        for (int view = 0; view < Views.Length; view++)
        {
            LookAt(view);
            string state = "no dialog";
            WhenShown<DoomBuilder.UI.LinedefEditWindow>(dialog => { state = "dialog opened"; Click(Named(dialog, "OK")); });
            window.MouseMove(outside);
            window.MouseDown(outside, MouseButton.Right, RawInputModifiers.None);
            window.MouseUp(outside, MouseButton.Right, RawInputModifiers.None);
            Flush();
            seen.Add("wall " + (view + 1) + ": " + state);
        }
        Assert.All(seen, l => Assert.EndsWith("dialog opened", l));
    }

    // In the 2D modes the pointer matters: a click on the menu bar is the menu's, not the map's
    [AvaloniaFact]
    public void A_click_outside_the_display_is_not_taken_over_in_the_2D_modes()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("SectorsMode");
        Flush();
        Assert.False(General.Interface.MouseExclusive);

        var outside = OutsideTheDisplay();
        var handled = false;
        window.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (s, e) => handled |= e.Handled, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        window.MouseDown(outside, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(outside, MouseButton.Left, RawInputModifiers.None);
        Flush();
        Assert.False(General.Map.Map.Sectors.Any(sec => sec.Selected), "nothing was selected on the map");
    }

    // The pointer is gone from the whole window while the 3D view has the mouse, back for a dialog, and back for good when the mode ends
    [AvaloniaFact]
    public void The_pointer_is_hidden_in_the_whole_window_in_the_3D_view_and_comes_back_for_dialogs_and_when_leaving()
    {
        OpenEditor(wadPath: WriteUdmfWad(Room), config: "GZDoom_DoomUDMF.cfg");
        Assert.Null(window.Cursor);                                 // the 2D modes use the pointer

        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        Assert.NotNull(window.Cursor);                              // hidden everywhere, not only over the view

        LookAtTheWall();
        bool backForTheDialog = false;
        WhenShown<DoomBuilder.UI.LinedefEditWindow>(dialog => { backForTheDialog = window.Cursor == null; Click(Named(dialog, "OK")); });
        var at = ViewCenter();
        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Right, RawInputModifiers.None);
        window.MouseUp(at, MouseButton.Right, RawInputModifiers.None);
        Flush();
        Assert.True(backForTheDialog, "the pointer is shown while the dialog is open");
        Assert.NotNull(window.Cursor);                              // and hidden again after it

        General.Editing.ChangeMode("SectorsMode");
        Flush();
        Assert.Null(window.Cursor);
    }
}
