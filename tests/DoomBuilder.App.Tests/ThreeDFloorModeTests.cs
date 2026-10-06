using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The 3D floor plugin (ThreeDFloorMode: 3D floors and slopes) in the real window.</summary>
public class ThreeDFloorModeTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // Two rooms side by side, drawn clockwise (the front of a wall is on its right, so this is the inside)
    protected const string Rooms = @"namespace = ""zdoom"";
thing { x = 64.0; y = 64.0; type = 1; angle = 0; skill1 = true; skill2 = true; single = true; }
vertex { x = 0.0; y = 0.0; }
vertex { x = 0.0; y = 128.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 128.0; y = 0.0; }
vertex { x = 256.0; y = 0.0; }
vertex { x = 256.0; y = 128.0; }
vertex { x = 384.0; y = 128.0; }
vertex { x = 384.0; y = 0.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 3; sidefront = 2; blocking = true; }
linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }
linedef { v1 = 4; v2 = 5; sidefront = 4; blocking = true; }
linedef { v1 = 5; v2 = 6; sidefront = 5; blocking = true; }
linedef { v1 = 6; v2 = 7; sidefront = 6; blocking = true; }
linedef { v1 = 7; v2 = 4; sidefront = 7; blocking = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sector { heightfloor = 0; heightceiling = 256; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
sector { heightfloor = 0; heightceiling = 256; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
";

    protected void OpenRooms() => OpenEditor(wadPath: WriteUdmfWad(Rooms), config: "GZDoom_DoomUDMF.cfg");

    [AvaloniaFact]
    public void The_plugin_loads_and_registers_its_modes()
    {
        OpenRooms();
        var names = General.Editing.ModesInfo.Select(m => m.Type.Name).ToList();
        Assert.Contains("ThreeDFloorHelperMode", names);
        Assert.Contains("SlopeMode", names);
        Assert.Contains("DrawSlopesMode", names);
        Assert.NotNull(CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me);
    }

    [AvaloniaFact]
    public void The_3D_floor_mode_opens_its_docker_and_cancel_goes_back()
    {
        OpenRooms();
        General.Actions.InvokeAction("threedfloormode_threedfloorhelpermode");
        Flush();
        Assert.Equal("ThreeDFloorHelperMode", General.Editing.Mode.GetType().Name);
        Assert.Equal("3D floors", General.Interface.ActiveDockerTabName);

        General.Editing.CancelMode();
        Flush();
        Assert.Equal("SectorsMode", General.Editing.Mode.GetType().Name);      // like UDB, it returns to the sectors mode
    }

    private static Avalonia.Controls.Button Named(Avalonia.Controls.Window w, string text)
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w).OfType<Avalonia.Controls.Button>().First(b => (string)b.Content == text && b.IsEffectivelyVisible);

    // The mode does this when Edit is pressed on a selected sector (3D floor editor, then the floors are built in the map)
    [AvaloniaFact]
    public void A_3D_floor_is_added_through_the_editor_and_built_with_its_control_sector_and_line()
    {
        OpenRooms();
        General.Actions.InvokeAction("threedfloormode_threedfloorhelpermode");
        Flush();
        var room = General.Map.Map.Sectors.First();
        room.Selected = true;
        int sectors = General.Map.Map.Sectors.Count, lines = General.Map.Map.Linedefs.Count;

        bool shown = false;
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            shown = true;
            var editor = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.TDFEW;
            Assert.Equal("3D floors", d.Title);
            Assert.True(editor.NoFloorsMessageVisible, "no 3D floors yet");

            Click(Named(d, "Add 3D floor"));
            Assert.Single(editor.UsedControls);
            Assert.False(editor.NoFloorsMessageVisible);

            var ctrl = editor.UsedControls.First();
            Assert.True(ctrl.IsNew);
            Assert.Contains(ctrl.Buttons, b => (string)b.Content == "Duplicate");
            Assert.Equal(1, ctrl.checkedListBoxSectors.Items.Count);               // the selected sector
            Assert.Equal(System.Windows.Forms.CheckState.Checked, ctrl.checkedListBoxSectors.GetItemCheckState(0));

            ctrl.FloorHeightBox.Text = "64";
            ctrl.CeilingHeightBox.Text = "96";
            Assert.Equal("32", ctrl.BorderHeightText.Text);                          // the border is the difference
            Click(d.OkButton);
        });

        var result = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.ThreeDFloorEditor();
        Flush();
        Assert.True(shown);
        Assert.Equal(System.Windows.Forms.DialogResult.OK, result);

        var floors = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.TDFEW.ThreeDFloors;
        Assert.Single(floors);
        Assert.Equal(96, floors[0].TopHeight);
        Assert.Equal(64, floors[0].BottomHeight);
        Assert.Contains(room, floors[0].TaggedSectors);

        // The mode then builds the 3D floor into the map: a control sector in the control sector area and the line that makes the effect
        CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.ProcessThreeDFloors(floors);
        General.Map.Map.Update();
        Assert.True(General.Map.Map.Sectors.Count > sectors, "a control sector was made");
        Assert.True(General.Map.Map.Linedefs.Count > lines, "and the lines around it");
        Assert.Contains(General.Map.Map.Linedefs, l => l.Action == 160);          // Sector_Set3dFloor

        var found = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.GetThreeDFloors(General.Map.Map.Sectors.ToList());
        Assert.Single(found);
        Assert.Equal(96, found[0].TopHeight);
        Assert.Equal(64, found[0].BottomHeight);

        General.Editing.CancelMode();
    }

    // Two rooms selected: one 3D floor tagged to both, which "Split" turns into one per room, and "Duplicate" copies
    [AvaloniaFact]
    public void A_3D_floor_over_two_sectors_can_be_duplicated_split_and_detached()
    {
        OpenRooms();
        General.Actions.InvokeAction("threedfloormode_threedfloorhelpermode");
        Flush();
        foreach (var sector in General.Map.Map.Sectors) sector.Selected = true;

        var counts = new System.Collections.Generic.List<string>();
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            var editor = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.TDFEW;
            Click(Named(d, "Add 3D floor"));
            var first = editor.UsedControls.Single();
            Assert.Equal(2, first.checkedListBoxSectors.Items.Count);

            // Duplicate: a second row (it copies what the 3D floor holds, like in UDB; the boxes are only read on OK)
            Click(Named(d, "Duplicate"));
            counts.Add("duplicate: " + editor.UsedControls.Count());
            var copy = editor.UsedControls.Last();
            Assert.Equal(2, copy.checkedListBoxSectors.Items.Count);

            // Split (all): every row with two checked sectors gives one row per sector
            Click(Named(d, "Split all"));
            counts.Add("split: " + editor.UsedControls.Count());
            foreach (var ctrl in editor.UsedControls)
                Assert.Equal(1, Enumerable.Range(0, ctrl.checkedListBoxSectors.Items.Count).Count(i => ctrl.checkedListBoxSectors.GetItemCheckState(i) == System.Windows.Forms.CheckState.Checked));

            // Uncheck all / check all reach every row
            Click(Named(d, "Uncheck all"));
            Assert.All(editor.UsedControls, c => Assert.Equal(0, Enumerable.Range(0, c.checkedListBoxSectors.Items.Count).Count(i => c.checkedListBoxSectors.GetItemCheckState(i) == System.Windows.Forms.CheckState.Checked)));
            Click(Named(d, "Check all"));
            Assert.All(editor.UsedControls, c => Assert.Equal(2, Enumerable.Range(0, c.checkedListBoxSectors.Items.Count).Count(i => c.checkedListBoxSectors.GetItemCheckState(i) == System.Windows.Forms.CheckState.Checked)));

            Click(d.CancelButton);
        });
        CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.ThreeDFloorEditor();
        Flush();

        Assert.Equal("duplicate: 2", counts[0]);
        Assert.Equal("split: 4", counts[1]);                 // each of the two rows gave one more
        // Cancelling takes the dummy sectors of the rows away again
        Assert.Equal(2, General.Map.Map.Sectors.Count);
        General.Editing.CancelMode();
    }

    [AvaloniaFact]
    public void The_docker_shows_the_3D_floors_of_the_sector_under_the_pointer()
    {
        OpenRooms();
        var room = General.Map.Map.Sectors.First();
        room.Selected = true;
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            Click(Named(d, "Add 3D floor"));
            var ctrl = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.TDFEW.UsedControls.Single();
            ctrl.FloorHeightBox.Text = "64";
            ctrl.CeilingHeightBox.Text = "96";
            Click(d.OkButton);
        });
        CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.ThreeDFloorEditor();
        CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.ProcessThreeDFloors(CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.TDFEW.ThreeDFloors);
        General.Map.Map.Update();
        room.Selected = false;

        General.Actions.InvokeAction("threedfloormode_threedfloorhelpermode");
        Flush();
        var mode = (CodeImp.DoomBuilder.ThreeDFloorMode.ThreeDFloorHelperMode)General.Editing.Mode;

        // The mode fills the docker when the highlight moves to a sector that has 3D floors
        var update = typeof(CodeImp.DoomBuilder.ThreeDFloorMode.ThreeDFloorHelperMode).GetMethod("UpdateDocker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        update.Invoke(mode, new object[] { room });
        var panel = (CodeImp.DoomBuilder.ThreeDFloorMode.ThreeDFloorPanel)typeof(CodeImp.DoomBuilder.ThreeDFloorMode.ThreeDFloorHelperMode).GetField("panel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(mode);
        Assert.Equal(1, panel.Count);

        var elements = (System.Collections.Generic.List<CodeImp.DoomBuilder.ThreeDFloorMode.ThreeDFloorHelperTooltipElementControl>)typeof(CodeImp.DoomBuilder.ThreeDFloorMode.ThreeDFloorHelperMode).GetField("tooltipelements", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(mode);
        Assert.Single(elements);
        Assert.True(elements[0].Visible);
        Assert.Equal("96", elements[0].topHeight.Text);
        Assert.Equal("64", elements[0].bottomHeight.Text);
        Assert.Equal("32", elements[0].borderHeight.Text);
        Assert.All(elements[0].Pictures, p => Assert.True(p.HasImage, "a picture of the flat or texture"));

        // A sector without 3D floors hides the element again
        update.Invoke(mode, new object[] { General.Map.Map.Sectors.Last() });
        Assert.False(elements[0].Visible);
        General.Editing.CancelMode();
    }

    [AvaloniaFact]
    public void The_slope_mode_asks_for_a_slope_data_sector_and_makes_one_in_the_control_sector_area()
    {
        OpenRooms();
        int sectors = General.Map.Map.Sectors.Count;
        Assert.Null(CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.SlopeDataSector);

        bool shown = false;
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            shown = true;
            Assert.Equal("Slope data sector", d.Title);
            Click(Named(d, "Create sector in CSA"));
        });
        General.Actions.InvokeAction("threedfloormode_threedslopemode");
        Flush();

        Assert.True(shown);
        Assert.NotNull(CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.SlopeDataSector);
        Assert.Equal(sectors + 1, General.Map.Map.Sectors.Count);
        Assert.Equal("SlopeMode", General.Editing.Mode.GetType().Name);
        General.Editing.CancelMode();
    }

    [AvaloniaFact]
    public void Cancelling_the_slope_data_sector_dialog_leaves_the_slope_mode_and_the_map_alone()
    {
        OpenRooms();
        int sectors = General.Map.Map.Sectors.Count;
        WhenShown<DoomBuilder.UI.SimpleDialog>(d => Click(d.CancelButton));
        General.Actions.InvokeAction("threedfloormode_threedslopemode");
        Flush();
        Assert.NotEqual("SlopeMode", General.Editing.Mode.GetType().Name);
        Assert.Equal(sectors, General.Map.Map.Sectors.Count);
        Assert.Null(CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.SlopeDataSector);
    }

    [AvaloniaFact]
    public void The_slope_data_sector_dialog_wants_exactly_one_selected_sector_to_use_it()
    {
        OpenRooms();
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;
        var dialog = new CodeImp.DoomBuilder.ThreeDFloorMode.SlopeDataSectorDialog();
        var shown = false;
        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            shown = true;
            // none selected
            Click(dialog.UseSelectedButton);
            Assert.Contains(scripted.Messages, m => m.Contains("No sectors selected"));
            Assert.True(d.IsVisible);

            // two selected
            foreach (var sector in General.Map.Map.Sectors) sector.Selected = true;
            scripted.Messages.Clear();
            Click(dialog.UseSelectedButton);
            Assert.Contains(scripted.Messages, m => m.Contains("2 sectors selected"));
            Assert.True(d.IsVisible);

            // one selected: it is the slope data sector
            General.Map.Map.Sectors.Last().Selected = false;
            Click(dialog.UseSelectedButton);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, dialog.ShowDialog());
        Assert.True(shown);
        Assert.Single(General.Map.Map.GetMarkedSectors(true));
        Assert.Same(General.Map.Map.Sectors.First(), General.Map.Map.GetMarkedSectors(true)[0]);
    }

    [AvaloniaFact]
    public void The_control_sector_area_configuration_checks_the_tag_range_and_applies_it()
    {
        OpenRooms();
        var csa = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.ControlSectorArea;
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;
        var form = new CodeImp.DoomBuilder.ThreeDFloorMode.ControlSectorAreaConfig(csa);
        Assert.False(form.FirstTagBox.IsEnabled);                      // the range is off until "Use tag range" is checked

        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            form.UseTagRangeBox.IsChecked = true;
            Assert.True(form.FirstTagBox.IsEnabled && form.LastTagBox.IsEnabled);

            form.FirstTagBox.Text = "50";
            form.LastTagBox.Text = "40";
            Click(d.OkButton);
            Assert.True(d.IsVisible, "a range that ends before it starts is refused");
            Assert.Contains(scripted.Messages, m => m.Contains("Last tag of range must be bigger"));

            form.LastTagBox.Text = "90";
            Click(d.OkButton);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, form.ShowDialog());
        Assert.True(csa.UseCustomTagRnage);
        Assert.Equal(50, csa.FirstTag);
        Assert.Equal(90, csa.LastTag);
    }

    [AvaloniaFact]
    public void The_plugin_adds_its_tab_to_the_preferences_and_saves_the_label_options_on_OK()
    {
        OpenRooms();
        Assert.Equal(CodeImp.DoomBuilder.ThreeDFloorMode.LabelDisplayOption.Always, CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.SectorLabelDisplayOption);
        bool seen = false;
        WhenShown<DoomBuilder.App.Dialogs.PreferencesWindow>(w =>
        {
            var tab = w.Tabs.Items.OfType<Avalonia.Controls.TabItem>().First(t => (string)t.Header == "3D Floor Plugin");
            seen = true;
            var combos = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Avalonia.Controls.Control)tab.Content).OfType<Avalonia.Controls.ComboBox>().ToList();
            Assert.Equal(2, combos.Count);                               // sector labels, slope vertex labels
            combos[0].SelectedIndex = 1;                                // Never
            combos[1].SelectedIndex = 2;                                // On slope vertex highlight
            Click(w.OkButton);
        });
        General.Actions.InvokeAction("builder_preferences");
        Flush();

        Assert.True(seen);
        Assert.Equal(1, General.Settings.ReadPluginSetting("threedfloormode", "sectorlabeldisplayoption", 0));
        Assert.Equal(2, General.Settings.ReadPluginSetting("threedfloormode", "slopevertexlabeldisplayoption", 0));
        Assert.Equal(CodeImp.DoomBuilder.ThreeDFloorMode.LabelDisplayOption.Never, CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.SectorLabelDisplayOption);
    }

    // The slope mode with a slope data sector and one group of three slope vertices
    private (CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertexGroup group, System.Collections.Generic.List<CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex> vertices) EnterSlopeModeWithAGroup()
    {
        OpenRooms();
        WhenShown<DoomBuilder.UI.SimpleDialog>(d => Click(Named(d, "Create sector in CSA")));
        General.Actions.InvokeAction("threedfloormode_threedslopemode");
        Flush();
        Assert.Equal("SlopeMode", General.Editing.Mode.GetType().Name);

        var vertices = new System.Collections.Generic.List<CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex>
        {
            new CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex(new CodeImp.DoomBuilder.Geometry.Vector2D(10, 20), 5),
            new CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex(new CodeImp.DoomBuilder.Geometry.Vector2D(30, 20), 5),
            new CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex(new CodeImp.DoomBuilder.Geometry.Vector2D(30, 60), 40),
        };
        var group = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.AddSlopeVertexGroup(vertices, out int id);
        return (group, vertices);
    }

    [AvaloniaFact]
    public void The_slope_vertex_dialog_edits_the_position_and_adds_the_selected_sector_to_the_group()
    {
        var (group, vertices) = EnterSlopeModeWithAGroup();
        var room = General.Map.Map.Sectors.First();
        room.Selected = true;

        var form = new CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertexEditForm();
        form.Setup(new System.Collections.Generic.List<CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex> { vertices[0] });
        Assert.Equal("Edit Slope Vertex", form.Title);
        Assert.Equal("10", form.PositionX.Text);
        Assert.Equal("20", form.PositionY.Text);
        Assert.Equal("5", form.PositionZ.Text);

        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            form.PositionX.Text = "15";
            form.PositionZ.Text = "8";
            form.AddToCeiling.IsChecked = true;
            Assert.True(form.AddToCeiling.IsEnabled);
            form.RemoveFromCeiling.IsChecked = true;                 // the two exclude each other
            Assert.False(form.AddToCeiling.IsChecked);
            form.AddToCeiling.IsChecked = true;
            Assert.False(form.RemoveFromCeiling.IsChecked);
            Click(d.OkButton);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, form.ShowDialog());

        Assert.Equal(15, vertices[0].Pos.x);
        Assert.Equal(20, vertices[0].Pos.y);                          // untouched
        Assert.Equal(8, vertices[0].Z);
        Assert.Contains(room, group.Sectors);                         // the sector joined the group
    }

    [AvaloniaFact]
    public void The_slope_vertex_dialog_with_several_vertices_leaves_blank_what_differs()
    {
        var (group, vertices) = EnterSlopeModeWithAGroup();
        General.Map.Map.ClearAllSelected();                           // no sector selected: the add/remove choices are off

        var form = new CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertexEditForm();
        form.Setup(new System.Collections.Generic.List<CodeImp.DoomBuilder.ThreeDFloorMode.SlopeVertex> { vertices[0], vertices[1] });
        Assert.Equal("Edit slope vertices (2)", form.Title);
        Assert.Equal("", form.PositionX.Text);                        // 10 and 30
        Assert.Equal("20", form.PositionY.Text);                      // the same
        Assert.Equal("5", form.PositionZ.Text);
        Assert.False(form.AddToCeiling.IsEnabled);
        Assert.False(form.RemoveFromFloor.IsEnabled);

        WhenShown<DoomBuilder.UI.SimpleDialog>(d =>
        {
            form.PositionY.Text = "25";                               // only the field that was filled in changes
            Click(d.OkButton);
        });
        form.ShowDialog();
        Assert.Equal(10, vertices[0].Pos.x);
        Assert.Equal(30, vertices[1].Pos.x);
        Assert.Equal(25, vertices[0].Pos.y);
        Assert.Equal(25, vertices[1].Pos.y);
    }

    [AvaloniaFact]
    public void The_slope_mode_context_menu_adds_and_removes_sectors_of_the_selected_group()
    {
        var (group, vertices) = EnterSlopeModeWithAGroup();
        var room = General.Map.Map.Sectors.First();
        var menu = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.MenusForm.AddSectorsContextMenu;
        menu.Tag = new System.Collections.Generic.List<CodeImp.DoomBuilder.Map.Sector> { room };

        // Nothing selected: adding is off (a sector can only join one group)
        menu.UpdateEnabled();
        Assert.False(menu.AddToFloor.IsEnabled);
        Assert.False(menu.AddToCeiling.IsEnabled);

        foreach (var v in vertices) v.Selected = true;                // one group selected
        menu.UpdateEnabled();
        Assert.True(menu.AddToFloor.IsEnabled && menu.AddToCeiling.IsEnabled);

        menu.AddSlope(CodeImp.DoomBuilder.ThreeDFloorMode.PlaneType.Floor);
        Assert.Contains(room, group.Sectors);
        menu.RemoveSlope(CodeImp.DoomBuilder.ThreeDFloorMode.PlaneType.Floor);
        Assert.DoesNotContain(room, group.Sectors);
    }

    [AvaloniaFact]
    public void The_toolbar_buttons_of_the_plugin_have_their_icons_and_actions()
    {
        OpenRooms();
        var menus = CodeImp.DoomBuilder.ThreeDFloorMode.BuilderPlug.Me.MenusForm;
        foreach (var button in new[] { menus.FloorSlope, menus.CeilingSlope, menus.FloorAndCeilingSlope, menus.UpdateSlopes, menus.RelocateControlSectors })
        {
            Assert.NotNull(button.Image);
            Assert.False(string.IsNullOrEmpty(button.Text));
        }
        Assert.Equal("drawfloorslope", menus.FloorSlope.Tag);
        Assert.Equal("relocate3dfloorcontrolsectors", menus.RelocateControlSectors.Tag);
    }
}
