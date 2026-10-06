using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.BuilderEffects;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The Builder Effects plugin: randomizing vertices, things and sectors, directional shading and the import of a .obj as terrain.</summary>
public class BuilderEffectsTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // The sample room (128 x 128, floor 8, ceiling 128) with two zombiemen in it
    private const string Map = UdmfSample
        + "thing { x = 32.0; y = 32.0; type = 3004; angle = 90; skill1 = true; skill2 = true; single = true; }\n"
        + "thing { x = 96.0; y = 96.0; type = 3004; angle = 180; skill1 = true; skill2 = true; single = true; }\n";

    private void OpenMap() => OpenEditor(wadPath: WriteUdmfWad(Map), config: "GZDoom_DoomUDMF.cfg");

    private static Sector Room => General.Map.Map.Sectors.First();

    private static void Select(string mode)
    {
        General.Editing.ChangeMode(mode);
        General.Map.Map.ClearAllSelected();
    }

    // ---------------------------------------------------------------- the plugin

    [AvaloniaFact]
    public void The_plugin_registers_its_actions_and_menu_entries()
    {
        OpenMap();
        Assert.NotNull(General.Actions.GetActionByName("buildereffects_applyjitter"));
        Assert.NotNull(General.Actions.GetActionByName("buildereffects_applydirectionalshading"));
        Assert.NotNull(General.Actions.GetActionByName("buildereffects_importobjasterrain"));
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "ImportObjAsTerrainMode");
        var menus = BuilderPlug.Me.Menus;
        Assert.EndsWith("applyjitter", (string)menus.JitterButton.Tag);
        Assert.EndsWith("applydirectionalshading", (string)menus.ShadingMenu.Tag);
        Assert.EndsWith("importobjasterrain", (string)menus.ImportMenu.Tag);
    }

    [AvaloniaFact]
    public void Nothing_is_randomized_without_a_selection_or_in_a_mode_that_has_nothing_to_randomize()
    {
        OpenMap();
        // (a window would wait for an answer: nothing must open)
        bool shown = false;
        WhenShown<EffectForm>(w => { shown = true; w.Close(); });
        Select("ThingsMode");
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Select("SectorsMode");
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Select("VerticesMode");
        General.Actions.InvokeAction("buildereffects_applyjitter");
        General.Editing.ChangeMode("DrawLinesMode");
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Flush();
        Assert.False(shown);
    }

    // ---------------------------------------------------------------- vertices

    [AvaloniaFact]
    public void The_vertices_are_moved_at_random_by_the_amount_and_Apply_keeps_them()
    {
        OpenMap();
        Select("VerticesMode");
        foreach (var v in General.Map.Map.Vertices) v.Selected = true;
        var before = General.Map.Map.Vertices.Select(v => v.Position).ToList();
        int undos = General.Map.UndoRedo.GetUndoList().Count;

        bool shown = false;
        WhenShown<JitterVerticesForm>(w =>
        {
            shown = true;
            Assert.Equal("Randomize 4 vertices", w.Text);
            Assert.Equal(64, w.PositionControl.Maximum);                              // half of the distance to the closest other line
            w.PositionControl.Number.Value = 10;
            var moved = General.Map.Map.Vertices.Select((v, i) => (v.Position - before[i]).GetLength()).ToList();
            Assert.All(moved, d => Assert.InRange(d, 0, 15));                         // at most 10 on each axis
            Assert.Contains(moved, d => d > 0);

            // "Update": other random directions
            var first = General.Map.Map.Vertices.Select(v => v.Position).ToList();
            bool different = false;
            for (int i = 0; i < 20 && !different; i++)
            {
                w.UpdateButton.PerformClick();
                different = General.Map.Map.Vertices.Select(v => v.Position).Zip(first, (a, b) => a != b).Any(x => x);
            }
            Assert.True(different, "other directions");
            w.ApplyButton.View.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        });
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Flush();
        Assert.True(shown);
        Assert.Equal(undos + 1, General.Map.UndoRedo.GetUndoList().Count);
        Assert.Equal("Randomize 4 vertices", General.Map.UndoRedo.NextUndo.Description);
        Assert.Contains(General.Map.Map.Vertices.Select((v, i) => v.Position != before[i]), moved => moved);
        Assert.Equal(0, General.Map.Map.SelectedVerticessCount);                       // Apply clears the selection
    }

    [AvaloniaFact]
    public void Cancel_puts_the_vertices_back()
    {
        OpenMap();
        Select("LinedefsMode");
        General.Map.Map.Linedefs.First().Selected = true;
        var before = General.Map.Map.Vertices.Select(v => v.Position).ToList();
        int undos = General.Map.UndoRedo.GetUndoList().Count;

        WhenShown<JitterVerticesForm>(w =>
        {
            Assert.Equal("Randomize 1 linedef", w.Text);
            w.PositionControl.Number.Value = 20;
            Assert.Contains(General.Map.Map.Vertices.Select((v, i) => v.Position != before[i]), moved => moved);
            w.CancelButton.PerformClick();
            w.Close();
        });
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Flush();
        Assert.Equal(before, General.Map.Map.Vertices.Select(v => v.Position).ToList());
        Assert.Equal(undos, General.Map.UndoRedo.GetUndoList().Count);
    }

    // ---------------------------------------------------------------- things

    [AvaloniaFact]
    public void Things_are_rotated_moved_and_scaled_at_random_and_the_choices_are_remembered()
    {
        OpenMap();
        Select("ThingsMode");
        var zombies = General.Map.Map.Things.Where(t => t.Type == 3004).ToList();
        foreach (var t in zombies) t.Selected = true;
        var angles = zombies.Select(t => t.AngleDoom).ToList();
        var positions = zombies.Select(t => t.Position).ToList();

        bool shown = false;
        WhenShown<JitterThingsForm>(w =>
        {
            shown = true;
            Assert.Equal("Randomize 2 things", w.Text);

            // The distance to the closest thing limits how far a thing can go
            Assert.True(w.PositionControl.Maximum > 0);
            w.PositionControl.Number.Value = 10;
            w.PositionControl.CommitChange();
            Assert.Contains(zombies.Select((t, i) => t.Position != positions[i]), moved => moved);

            // Rotation: the angle of each thing changes by a random amount of at most the value
            w.AngleControl.Number.Value = 90;
            w.AngleControl.CommitChange();
            Assert.All(zombies.Select(t => t.AngleDoom), a => Assert.InRange(a, 0, 359));

            // Scale (UDMF): from the minimum to the maximum
            w.MinScaleX.Value = 2;
            w.MaxScaleX.Value = 3;
            w.MinScaleY.Value = 2;
            w.MaxScaleY.Value = 2;
            Assert.All(zombies, t => { Assert.InRange(t.ScaleX, 2, 3); Assert.Equal(2, t.ScaleY, 3); });

            // "Same width and height": the height follows the width
            w.UniformScale.Checked = true;
            Assert.All(zombies, t => Assert.Equal(t.ScaleX, t.ScaleY, 3));

            w.CancelButton.PerformClick();
            w.Close();
        });
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Flush();
        Assert.True(shown);
        Assert.Equal(angles, zombies.Select(t => t.AngleDoom).ToList());                 // Cancel puts it all back
        Assert.Equal(positions, zombies.Select(t => t.Position).ToList());
        Assert.All(zombies, t => Assert.Equal(1, t.ScaleX, 3));
    }

    // ---------------------------------------------------------------- sectors

    [AvaloniaFact]
    public void The_heights_of_a_sector_move_at_random_with_the_offset_modes_and_Apply_keeps_them()
    {
        OpenMap();
        Select("SectorsMode");
        Room.Selected = true;
        int ceil = Room.CeilHeight, floor = Room.FloorHeight;

        bool shown = false;
        WhenShown<JitterSectorsForm>(w =>
        {
            shown = true;
            Assert.Equal("Randomize 1 sector", w.Text);
            Assert.Equal((ceil - floor) / 2, w.CeilingControl.Maximum);                  // half of the height of the sector

            // The ceiling can only be lowered
            w.CeilingOffsetMode.SelectedIndex = 1;
            w.CeilingControl.Number.Value = 20;
            Assert.InRange(Room.CeilHeight, ceil - 20, ceil);

            // The floor can only be raised
            w.FloorOffsetMode.SelectedIndex = 1;
            w.FloorControl.Number.Value = 20;
            Assert.InRange(Room.FloorHeight, floor, floor + 20);

            // Raise and lower: both ways
            w.FloorOffsetMode.SelectedIndex = 0;
            w.CeilingOffsetMode.SelectedIndex = 0;
            bool up = false, down = false;
            for (int i = 0; i < 60 && !(up && down); i++)
            {
                w.UpdateFloorButton.PerformClick();
                up |= Room.FloorHeight > floor;
                down |= Room.FloorHeight < floor;
            }
            Assert.True(up && down, "the floor goes both ways");

            // The texture styles of the sides: they follow the heights
            Assert.True(w.UpperStyle.Enabled);
            Assert.False(w.UpperTexture.Enabled);                                         // only for "Pick upper texture"
            w.UpperStyle.SelectedIndex = 2;
            Assert.True(w.UpperTexture.Enabled);
            Assert.True(w.UpperGroup.Enabled);

            w.ApplyButton.PerformClick();
        });
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Flush();
        Assert.True(shown);
        Assert.Equal("Randomize 1 sector", General.Map.UndoRedo.NextUndo.Description);
        Assert.Equal(0, General.Map.Map.SelectedSectorsCount);
    }

    [AvaloniaFact]
    public void Cancel_puts_the_heights_back()
    {
        OpenMap();
        Select("SectorsMode");
        Room.Selected = true;
        int ceil = Room.CeilHeight, floor = Room.FloorHeight;

        WhenShown<JitterSectorsForm>(w =>
        {
            w.CeilingControl.Number.Value = 30;
            w.FloorControl.Number.Value = 30;
            w.CancelButton.PerformClick();
            w.Close();
        });
        General.Actions.InvokeAction("buildereffects_applyjitter");
        Flush();
        Assert.Equal((floor, ceil), (Room.FloorHeight, Room.CeilHeight));
    }

    // ---------------------------------------------------------------- directional shading

    [AvaloniaFact]
    public void Directional_shading_lights_the_sectors_by_the_sun_and_Apply_keeps_the_choices()
    {
        OpenMap();
        Select("SectorsMode");
        Room.Selected = true;
        Assert.False(Room.Fields.ContainsKey("lightfloor"));

        bool shown = false;
        WhenShown<DirectionalShadingForm>(w =>
        {
            shown = true;
            Assert.Equal(45, w.SunAngleDial.Angle);                                       // the defaults
            Assert.Equal(64, w.LightAmount.Value);
            Assert.Equal(16, w.ShadeAmount.Value);
            Assert.Equal(0xFDEBD7, w.LightColor.Color.ToInt() & 0xFFFFFF);

            // A flat floor faces up: the sun is at a fixed height, so only the amount and the colors matter
            w.LightAmount.Number.Value = 100;
            Assert.True(Room.Fields.ContainsKey("lightfloor"));
            int light = (int)Room.Fields["lightfloor"].Value;
            w.LightAmount.Number.Value = 0;
            w.ShadeAmount.Number.Value = 0;
            Assert.False(Room.Fields.ContainsKey("lightfloor"));                          // no light: the default is not stored
            Assert.NotEqual(0, light);

            // The sun angle: the text and the dial go together
            w.SunAngleBox.Text = "90";
            Assert.Equal(90, w.SunAngleDial.Angle);

            w.LightAmount.Number.Value = 80;
            w.ApplyButton.PerformClick();
        });
        General.Actions.InvokeAction("buildereffects_applydirectionalshading");
        Flush();
        Assert.True(shown);
        Assert.StartsWith("Apply directional shading", General.Map.UndoRedo.NextUndo.Description);

        // The next time the window starts with what was applied
        WhenShown<DirectionalShadingForm>(w =>
        {
            Assert.Equal(90, w.SunAngleDial.Angle);
            Assert.Equal(80, w.LightAmount.Value);
            w.CancelButton.PerformClick();
        });
        General.Actions.InvokeAction("buildereffects_applydirectionalshading");
        Flush();
    }

    [AvaloniaFact]
    public void Cancel_takes_the_shading_back()
    {
        OpenMap();
        Select("SectorsMode");
        Room.Selected = true;
        int undos = General.Map.UndoRedo.GetUndoList().Count;
        WhenShown<DirectionalShadingForm>(w =>
        {
            w.ShadeAmount.Number.Value = 100;
            w.CancelButton.PerformClick();
            w.Close();
        });
        General.Actions.InvokeAction("buildereffects_applydirectionalshading");
        Flush();
        Assert.False(Room.Fields.ContainsKey("lightfloor"));
        Assert.Equal(undos, General.Map.UndoRedo.GetUndoList().Count);
    }

    [AvaloniaFact]
    public void Directional_shading_needs_a_UDMF_map()
    {
        OpenEditor();                                                                     // a Doom map
        bool shown = false;
        WhenShown<EffectForm>(w => { shown = true; w.Close(); });
        General.Editing.ChangeMode("SectorsMode");
        General.Map.Map.Sectors.First().Selected = true;
        General.Actions.InvokeAction("buildereffects_applydirectionalshading");
        Flush();
        Assert.False(shown);
    }

    // ---------------------------------------------------------------- terrain importer

    [AvaloniaFact]
    public void A_wavefront_model_is_imported_as_terrain_made_of_sectors()
    {
        OpenMap();
        string obj = Path.Combine(dir, "terrain.obj");
        File.WriteAllText(obj, "# a square of two triangles\nv 0 0 0\nv 256 0 16\nv 256 256 32\nv 0 256 16\nf 1 2 3\nf 1 3 4\n");
        int sectors = General.Map.Map.Sectors.Count, lines = General.Map.Map.Linedefs.Count;

        bool shown = false;
        WhenShown<ObjImportSettingsForm>(w =>
        {
            shown = true;
            Assert.Equal("1", w.ScaleBox.Value.ToString());
            w.PathBox.Text = obj;
            w.AxisZ.Checked = true;
            w.ScaleBox.Value = 1;
            w.ImportButton.PerformClick();
        });
        General.Actions.InvokeAction("buildereffects_importobjasterrain");
        Flush();
        Assert.True(shown);
        Assert.Equal(sectors + 2, General.Map.Map.Sectors.Count);                        // one sector for each triangle
        Assert.True(General.Map.Map.Linedefs.Count >= lines + 5);
        Assert.NotEqual("ImportObjAsTerrainMode", General.Editing.Mode.GetType().Name);   // the mode is over
    }

    [AvaloniaFact]
    public void The_importer_refuses_a_zero_scale_and_a_file_that_is_not_there()
    {
        OpenMap();
        var dialogs = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = dialogs;
        int sectors = General.Map.Map.Sectors.Count;

        bool shown = false;
        WhenShown<ObjImportSettingsForm>(w =>
        {
            shown = true;
            w.PathBox.Text = Path.Combine(dir, "nothing.obj");
            w.ImportButton.PerformClick();
            Assert.Contains(dialogs.Messages, m => m.Contains("does not exist"));
            Assert.True(w.IsVisible, "the window stays open");

            w.ScaleBox.Value = 0;
            w.ImportButton.PerformClick();
            Assert.Contains(dialogs.Messages, m => m.Contains("zero"));
            w.CancelButton.PerformClick();
            w.Close();
        });
        General.Actions.InvokeAction("buildereffects_importobjasterrain");
        Flush();
        Assert.True(shown);
        Assert.Equal(sectors, General.Map.Map.Sectors.Count);
    }
}
