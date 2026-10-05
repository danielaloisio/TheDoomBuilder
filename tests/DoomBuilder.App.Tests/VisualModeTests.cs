using System;
using System.Linq;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The 3D visual mode (BuilderModes' BaseVisualMode) running in the real window on the sample map.</summary>
public class VisualModeTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Probe_entering_the_visual_mode()
    {
        OpenEditor();
        var names = General.Editing.ModesInfo.Select(m => m.Type.Name).ToList();
        Assert.Contains("BaseVisualMode", names);
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        Assert.Equal("BaseVisualMode", General.Editing.Mode.GetType().Name);
    }

    private static void Pump(int ms)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end) { Flush(); System.Threading.Thread.Sleep(10); }
    }

    // Ticks the input processing by hand (the shell does it every 10 ms)
    private static void Tick() => ((DoomBuilder.App.AvaloniaShell)General.Interface).Input.Tick();

    [AvaloniaFact]
    public void The_visual_mode_key_leaves_the_visual_mode_again()
    {
        OpenEditor();
        window.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.Q, null);
        window.KeyRelease(Key.Q, RawInputModifiers.None, PhysicalKey.Q, null);
        Flush();
        Assert.Equal("BaseVisualMode", General.Editing.Mode.GetType().Name);

        window.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.Q, null);
        window.KeyRelease(Key.Q, RawInputModifiers.None, PhysicalKey.Q, null);
        Flush();
        Assert.NotEqual("BaseVisualMode", General.Editing.Mode.GetType().Name);
    }

    [AvaloniaFact]
    public void Holding_the_forward_key_moves_the_camera_the_way_it_looks()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        var cam = General.Map.VisualCamera;
        var before = cam.Position;
        cam.AngleXY = 0;                                        // looking along +Y (UDB's 3D angle 0)
        cam.AngleZ = Math.PI;                                   // level

        window.MouseMove(ViewCenter());
        window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, null);
        Tick();
        System.Threading.Thread.Sleep(60);
        Tick();
        window.KeyRelease(Key.E, RawInputModifiers.None, PhysicalKey.E, null);

        var after = cam.Position;
        Assert.True(after.y > before.y, "moved forward");
        Assert.Equal(before.x, after.x, 3);
        Assert.Equal(before.z, after.z, 3);

        Tick();                                                 // released: it stops
        Assert.Equal(after.y, cam.Position.y, 3);
    }

    [AvaloniaFact]
    public void Mouse_input_turns_the_camera_and_keeps_the_pitch_within_limits()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        var cam = General.Map.VisualCamera;
        double yaw = cam.AngleXY;

        General.Editing.Mode.OnMouseInput(new CodeImp.DoomBuilder.Geometry.Vector2D(40, 0));
        Assert.NotEqual(yaw, cam.AngleXY);

        General.Editing.Mode.OnMouseInput(new CodeImp.DoomBuilder.Geometry.Vector2D(0, 100000));
        Assert.InRange(cam.AngleZ, CodeImp.DoomBuilder.VisualModes.VisualCamera.MAX_ANGLEZ_LOW, CodeImp.DoomBuilder.VisualModes.VisualCamera.MAX_ANGLEZ_HIGH);
        General.Editing.Mode.OnMouseInput(new CodeImp.DoomBuilder.Geometry.Vector2D(0, -100000));
        Assert.InRange(cam.AngleZ, CodeImp.DoomBuilder.VisualModes.VisualCamera.MAX_ANGLEZ_LOW, CodeImp.DoomBuilder.VisualModes.VisualCamera.MAX_ANGLEZ_HIGH);
    }

    [AvaloniaFact]
    public void The_hints_name_the_keys_instead_of_showing_their_codes()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();

        string hint = null;
        ((DoomBuilder.App.AvaloniaShell)General.Interface).HintsChanged += text => hint = text;
        General.Editing.ChangeMode("VerticesMode");
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();

        Assert.NotNull(hint);
        Assert.DoesNotMatch(@"\b69\b", hint);
        Assert.Contains("E", hint);
    }

    [AvaloniaFact]
    public void Fit_textures_opens_on_the_selected_walls_and_applies_or_cancels()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        var mode = (CodeImp.DoomBuilder.BuilderModes.BaseVisualMode)General.Editing.Mode;

        var sides = new System.Collections.Generic.List<CodeImp.DoomBuilder.BuilderModes.BaseVisualGeometrySidedef>();
        foreach (var sector in General.Map.Map.Sectors)
        {
            var vs = (CodeImp.DoomBuilder.BuilderModes.BaseVisualSector)mode.GetVisualSector(sector);
            foreach (var parts in vs.Sides.Values)
                foreach (var part in new CodeImp.DoomBuilder.BuilderModes.BaseVisualGeometrySidedef[] { parts.middlesingle, parts.upper, parts.lower, parts.middledouble })
                    if (part != null && part.Sidedef.Line.Front == part.Sidedef) sides.Add(part);
        }
        Assert.NotEmpty(sides);

        General.Map.UndoRedo.CreateUndo("Fit textures");
        var form = new CodeImp.DoomBuilder.BuilderModes.FitTexturesForm();
        Assert.True(form.Setup(sides));
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            Assert.Equal("Fit Textures", d.Title);
            Assert.Equal("Apply", (string)d.OkButton.Content);
            Click(d.OkButton);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, form.ShowDialog(General.Interface));

        General.Map.UndoRedo.CreateUndo("Fit textures again");
        var second = new CodeImp.DoomBuilder.BuilderModes.FitTexturesForm();
        Assert.True(second.Setup(sides));
        WhenShown<DoomBuilder.UI.SimpleDialog>(d => Click(d.CancelButton));
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, second.ShowDialog(General.Interface));
    }

    // Puts the camera in the first room, aimed at the floor (the pitch sign is tried both ways: it is UDB's convention, not obvious)
    private CodeImp.DoomBuilder.Map.Sector AimAtTheFloor()
    {
        var cam = General.Map.VisualCamera;
        cam.Position = new CodeImp.DoomBuilder.Geometry.Vector3D(150, 150, 41);     // away from the things, which would be picked first
        cam.AngleXY = 0;
        var sector = General.Map.Map.GetSectorByCoordinates(new CodeImp.DoomBuilder.Geometry.Vector2D(150, 150));
        Assert.NotNull(sector);

        foreach (double pitch in new[] { Math.PI + 1.2, Math.PI - 1.2 })
        {
            cam.AngleZ = pitch;
            Tick();                                             // culls the geometry for the new view
            System.Threading.Thread.Sleep(120);                 // the target is picked every 80 ms
            Tick();
            int before = sector.FloorHeight;
            General.Actions.InvokeAction("buildermodes_raisesector8");
            if (sector.FloorHeight != before) { General.Actions.InvokeAction("buildermodes_lowersector8"); return sector; }
        }
        return null;
    }

    [AvaloniaFact]
    public void Raising_the_floor_under_the_crosshair_changes_the_sector_and_undo_brings_it_back()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        var sector = AimAtTheFloor();
        Assert.NotNull(sector);                                 // the crosshair is on the floor in one of the two pitches

        int floor = sector.FloorHeight;
        General.Actions.InvokeAction("buildermodes_raisesector8");
        Assert.Equal(floor + 8, sector.FloorHeight);
        General.Actions.InvokeAction("buildermodes_raisesector1");
        Assert.Equal(floor + 9, sector.FloorHeight);
        General.Actions.InvokeAction("buildermodes_lowersector8");
        Assert.Equal(floor + 1, sector.FloorHeight);

        General.Map.UndoRedo.PerformUndo();
        General.Map.UndoRedo.PerformUndo();
        General.Map.UndoRedo.PerformUndo();
        Assert.Equal(floor, sector.FloorHeight);
    }

    [AvaloniaFact]
    public void Selecting_with_the_crosshair_and_clearing_the_selection()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        var sector = AimAtTheFloor();
        Assert.NotNull(sector);

        var selectedobjects = typeof(CodeImp.DoomBuilder.VisualModes.VisualMode).GetField("selectedobjects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? typeof(CodeImp.DoomBuilder.BuilderModes.BaseVisualMode).GetField("selectedobjects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int Count() => ((System.Collections.ICollection)selectedobjects.GetValue(General.Editing.Mode)).Count;
        Assert.Equal(0, Count());

        General.Actions.InvokeAction("builder_visualselect");
        Assert.Equal(1, Count());                               // the floor under the crosshair

        General.Actions.InvokeAction("builder_clearselection");
        Assert.Equal(0, Count());
    }

    [AvaloniaFact]
    public void Edit_on_the_floor_opens_the_sector_dialog_and_changes_it_from_3D()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        var sector = AimAtTheFloor();
        Assert.NotNull(sector);
        int brightness = sector.Brightness;

        bool shown = false;
        WhenShown<DoomBuilder.UI.SectorEditWindow>(w =>
        {
            shown = true;
            Click(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<Avalonia.Controls.Button>().First(b => (string)b.Content == "Cancel"));
        });
        General.Actions.InvokeAction("builder_visualedit");
        Flush();

        Assert.True(shown, "the sector dialog opened");
        Assert.Equal(brightness, sector.Brightness);            // cancelled
    }

    [AvaloniaFact]
    public void Choosing_a_texture_from_3D_opens_the_browser_and_sets_it_on_the_surface()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        var sector = AimAtTheFloor();
        Assert.NotNull(sector);

        string chosen = null;
        WhenShown<DoomBuilder.UI.ImageBrowserWindow>(w =>
        {
            chosen = "the browser opened";
            w.Close(null);                                       // no choice: nothing changes
        });
        string before = sector.FloorTexture;
        General.Actions.InvokeAction("buildermodes_textureselect");
        Flush();

        Assert.NotNull(chosen);
        Assert.Equal(before, sector.FloorTexture);
    }

    [AvaloniaFact]
    public void Switching_between_the_2D_modes_and_the_3D_mode_keeps_the_map_and_the_selection()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        var first = General.Map.Map.Sectors.First();
        ((CodeImp.DoomBuilder.BuilderModes.BaseClassicMode)General.Editing.Mode).SelectMapElement(first);

        General.Editing.ChangeMode("BaseVisualMode");
        Flush();
        Assert.Equal("BaseVisualMode", General.Editing.Mode.GetType().Name);

        General.Editing.ChangeMode("SectorsMode");
        Flush();
        Assert.Equal("SectorsMode", General.Editing.Mode.GetType().Name);
        Assert.Equal(4, General.Map.Map.Sectors.Count);
    }

    // Three rooms in a row, the outer two at different heights: slope handles on their outer walls, the middle one to be arched
    private const string ThreeRooms = @"namespace = ""zdoom"";
thing { x = 64.0; y = 64.0; type = 1; angle = 0; skill1 = true; skill2 = true; single = true; }
vertex { x = 0.0; y = 0.0; }
vertex { x = 128.0; y = 0.0; }
vertex { x = 256.0; y = 0.0; }
vertex { x = 384.0; y = 0.0; }
vertex { x = 384.0; y = 128.0; }
vertex { x = 256.0; y = 128.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 0.0; y = 128.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
linedef { v1 = 3; v2 = 4; sidefront = 3; blocking = true; }
linedef { v1 = 4; v2 = 5; sidefront = 4; blocking = true; }
linedef { v1 = 5; v2 = 6; sidefront = 5; blocking = true; }
linedef { v1 = 6; v2 = 7; sidefront = 6; blocking = true; }
linedef { v1 = 7; v2 = 0; sidefront = 7; blocking = true; }
linedef { v1 = 1; v2 = 6; sidefront = 8; sideback = 9; twosided = true; }
linedef { v1 = 5; v2 = 2; sidefront = 10; sideback = 11; twosided = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 2; texturemiddle = ""STARTAN1""; }
sidedef { sector = 2; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturetop = ""STARTAN1""; texturebottom = ""STARTAN1""; }
sidedef { sector = 1; texturetop = ""STARTAN1""; texturebottom = ""STARTAN1""; }
sidedef { sector = 1; texturetop = ""STARTAN1""; texturebottom = ""STARTAN1""; }
sidedef { sector = 2; texturetop = ""STARTAN1""; texturebottom = ""STARTAN1""; }
sector { heightfloor = 0; heightceiling = 256; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
sector { heightfloor = 0; heightceiling = 256; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
sector { heightfloor = 64; heightceiling = 256; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
";

    [AvaloniaFact]
    public void Create_arch_shows_the_angle_and_scale_and_applies_a_curved_slope_to_the_chosen_floor()
    {
        OpenEditor(wadPath: WriteUdmfWad(ThreeRooms), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("BaseVisualMode");
        var mode = (CodeImp.DoomBuilder.BuilderModes.BaseVisualMode)General.Editing.Mode;
        var sectors = General.Map.Map.Sectors.ToList();
        var left = sectors[0]; var middle = sectors[1]; var right = sectors[2];

        // The handles sit on a wall of the low room and on a wall of the high room
        var lowhandle = new CodeImp.DoomBuilder.VisualModes.VisualSidedefSlope(mode, mode.GetSectorData(left).Floor, left.Sidedefs.First(), true);
        var highhandle = new CodeImp.DoomBuilder.VisualModes.VisualSidedefSlope(mode, mode.GetSectorData(right).Floor, right.Sidedefs.First(), true);
        var floor = (CodeImp.DoomBuilder.BuilderModes.BaseVisualGeometrySector)((CodeImp.DoomBuilder.BuilderModes.BaseVisualSector)mode.GetVisualSector(middle)).Floor;
        var targets = new System.Collections.Generic.List<CodeImp.DoomBuilder.BuilderModes.IVisualEventReceiver> { floor };

        var archer = new CodeImp.DoomBuilder.BuilderModes.SlopeArcher(mode, targets, lowhandle, highhandle, Math.PI / 2, Math.PI / 2, 1.0);
        var form = new CodeImp.DoomBuilder.BuilderModes.SlopeArchForm(archer);
        int changes = 0;
        form.UpdateChangedObjects += (s, e) => changes++;

        System.Collections.Generic.List<DoomBuilder.UI.NumberBox> boxes = null;
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            Assert.Equal("Create arch", d.Title);
            boxes = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Avalonia.Controls.Control)d.Content).OfType<DoomBuilder.UI.NumberBox>().ToList();
            Assert.Equal(new[] { "90", "90", "100", "0" }, boxes.Select(b => b.Text));        // angle, offset, scale %, height offset
            Assert.True(changes >= 1);                                                       // the arch is applied as soon as it opens

            boxes[0].Text = "120";                                                            // 120 + the offset 90 is more than half a circle: refused
            Assert.Equal("90", boxes[0].Text);
            Assert.Equal(90.0, Angle(archer), 3);

            boxes[1].Text = "30";                                                             // with a smaller offset a wider arch is fine, and applied again
            int before = changes;
            boxes[0].Text = "120";
            Assert.Equal(120.0, Angle(archer), 3);
            Assert.True(changes > before);

            Click(d.OkButton);
        });

        Assert.Equal(System.Windows.Forms.DialogResult.OK, form.ShowDialog());
        Assert.False(middle.FloorSlope.GetLengthSq() == 0.0, "the middle floor is sloped");
    }

    // Selects what the arch action needs: two slope handles and the floor and ceiling of the middle room
    private void SelectForArch(CodeImp.DoomBuilder.BuilderModes.BaseVisualMode mode, System.Collections.Generic.IList<CodeImp.DoomBuilder.Map.Sector> sectors)
    {
        var lowhandle = new CodeImp.DoomBuilder.VisualModes.VisualSidedefSlope(mode, mode.GetSectorData(sectors[0]).Floor, sectors[0].Sidedefs.First(), true);
        var highhandle = new CodeImp.DoomBuilder.VisualModes.VisualSidedefSlope(mode, mode.GetSectorData(sectors[2]).Floor, sectors[2].Sidedefs.First(), true);
        var visual = (CodeImp.DoomBuilder.BuilderModes.BaseVisualSector)mode.GetVisualSector(sectors[1]);

        var field = typeof(CodeImp.DoomBuilder.BuilderModes.BaseVisualMode).GetField("selectedobjects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var selection = (System.Collections.IList)field.GetValue(mode);
        selection.Clear();
        foreach (var picked in new CodeImp.DoomBuilder.BuilderModes.IVisualEventReceiver[] { lowhandle, highhandle, (CodeImp.DoomBuilder.BuilderModes.BaseVisualGeometrySector)visual.Floor, (CodeImp.DoomBuilder.BuilderModes.BaseVisualGeometrySector)visual.Ceiling })
        {
            if (picked is CodeImp.DoomBuilder.VisualModes.VisualSlope slope) slope.Selected = true;
            else if (picked is CodeImp.DoomBuilder.VisualModes.VisualGeometry geometry) geometry.Selected = true;
            selection.Add(picked);
        }
    }

    [AvaloniaFact]
    public void The_arch_action_slopes_the_selected_floors_and_Cancel_puts_everything_back()
    {
        OpenEditor(wadPath: WriteUdmfWad(ThreeRooms), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("BaseVisualMode");
        var mode = (CodeImp.DoomBuilder.BuilderModes.BaseVisualMode)General.Editing.Mode;
        var sectors = General.Map.Map.Sectors.ToList();
        var middle = sectors[1];
        int floor = middle.FloorHeight, ceiling = middle.CeilHeight;

        // Cancelled: the arch that was shown while the dialog was open is withdrawn with its undo level
        bool shown = false;
        SelectForArch(mode, sectors);
        WhenShown<DoomBuilder.UI.SimpleDialog>(d => { shown = true; Click(d.CancelButton); });
        General.Actions.InvokeAction("buildermodes_archbetweenhandles");
        Flush();
        Assert.True(shown, "the dialog opened");
        Assert.True(middle.FloorSlope.GetLengthSq() == 0.0, "no slope is left on the floor");
        Assert.Equal(floor, middle.FloorHeight);
        Assert.Equal(ceiling, middle.CeilHeight);

        // Applied: the floor keeps the arch, and one undo takes it away again
        shown = false;
        SelectForArch(mode, sectors);
        WhenShown<DoomBuilder.UI.SimpleDialog>(d => { shown = true; Click(d.OkButton); });
        General.Actions.InvokeAction("buildermodes_archbetweenhandles");
        Flush();
        Assert.True(shown, "the dialog opened again");
        Assert.False(middle.FloorSlope.GetLengthSq() == 0.0, "the floor is sloped");

        General.Map.UndoRedo.PerformUndo();
        Assert.True(middle.FloorSlope.GetLengthSq() == 0.0);
    }

    private static double Angle(CodeImp.DoomBuilder.BuilderModes.SlopeArcher a) => CodeImp.DoomBuilder.Geometry.Angle2D.RadToDeg(a.Theta);
}
