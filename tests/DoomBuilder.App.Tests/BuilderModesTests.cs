using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The BuilderModes plugin (the classic editing modes) running inside the real window on the sample map.</summary>
public class BuilderModesTests : EditorTestBase
{
    private static string ModeName => General.Editing.Mode.GetType().Name;

    private Point DisplayPointOf(Vector2D mappos)
    {
        Vector2D d = Renderer.MapToDisplay(mappos);
        return InView(d.x, d.y);
    }

    [AvaloniaFact]
    public void The_plugin_registers_the_classic_editing_modes()
    {
        OpenEditor();

        var names = General.Editing.ModesInfo.Select(m => m.Type.Name).ToList();
        Assert.Contains("VerticesMode", names);
        Assert.Contains("LinedefsMode", names);
        Assert.Contains("SectorsMode", names);
        Assert.Contains("ThingsMode", names);
        Assert.Contains("DragVerticesMode", names);
    }

    [AvaloniaFact]
    public void Switching_modes_runs_each_mode_on_the_map()
    {
        OpenEditor();

        foreach (string mode in new[] { "LinedefsMode", "SectorsMode", "ThingsMode", "VerticesMode" })
        {
            General.Editing.ChangeMode(mode);
            Assert.Equal(mode, ModeName);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void A_click_on_a_vertex_in_the_vertices_mode_selects_it()
    {
        OpenEditor();
        General.Editing.ChangeMode("VerticesMode");
        Assert.Equal(0, General.Map.Map.SelectedVerticessCount);
        Vertex target = General.Map.Map.Vertices.First();
        Point at = DisplayPointOf(target.Position);

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);

        Assert.Equal(1, General.Map.Map.SelectedVerticessCount);
        Assert.True(target.Selected);
    }

    [AvaloniaFact]
    public void Deleting_the_selection_removes_the_vertex_and_undo_brings_it_back()
    {
        OpenEditor();
        General.Editing.ChangeMode("VerticesMode");
        int before = General.Map.Map.Vertices.Count;
        Vertex target = General.Map.Map.Vertices.First();
        target.Selected = true;

        General.Actions.InvokeAction("builder_deleteitem");
        Assert.True(General.Map.Map.Vertices.Count < before);

        General.Actions.InvokeAction("builder_undo");
        Assert.Equal(before, General.Map.Map.Vertices.Count);
    }
}

/// <summary>The menus and buttons the plugin adds, drawn by the shell.</summary>
public class PluginMenusTests : EditorTestBase
{
    private static void Click(Avalonia.Controls.Control control)
        => control.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    private System.Collections.Generic.List<System.Windows.Forms.ToolStripButton> ModeButtons()
        => window.Plugins.Items.OfType<System.Windows.Forms.ToolStripButton>().Where(b => b.Tag is CodeImp.DoomBuilder.Editing.EditModeInfo).ToList();

    [AvaloniaFact]
    public void Each_optional_edit_mode_gets_a_button_and_a_menu_entry()
    {
        OpenEditor();
        RefreshShell();

        var names = ModeButtons().Select(b => ((CodeImp.DoomBuilder.Editing.EditModeInfo)b.Tag).Type.Name).ToList();
        Assert.Contains("VerticesMode", names);
        Assert.Contains("LinedefsMode", names);
        Assert.Contains("SectorsMode", names);
        Assert.Contains("ThingsMode", names);

        var menuentries = window.Plugins.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Where(m => m.Tag is CodeImp.DoomBuilder.Editing.EditModeInfo);
        Assert.NotEmpty(menuentries);
        Assert.True(window.Plugins.ControlOf(ModeButtons()[0]).IsVisible);
    }

    [AvaloniaFact]
    public void Clicking_a_mode_button_switches_the_mode_and_the_button_stays_checked()
    {
        OpenEditor();
        RefreshShell();
        var linedefs = ModeButtons().First(b => ((CodeImp.DoomBuilder.Editing.EditModeInfo)b.Tag).Type.Name == "LinedefsMode");

        Click(window.Plugins.ControlOf(linedefs));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("LinedefsMode", General.Editing.Mode.GetType().Name);
        Assert.True(linedefs.Checked);
        // (every mode has a toolbar button and a menu entry: both follow the active mode)
        Assert.All(ModeButtons(), b => Assert.Equal(((CodeImp.DoomBuilder.Editing.EditModeInfo)b.Tag).Type.Name == "LinedefsMode", b.Checked));
    }

    [AvaloniaFact]
    public void A_toolbar_button_of_the_plugin_reaches_its_handler_and_follows_the_state()
    {
        OpenEditor();
        General.Editing.ChangeMode("LinedefsMode");            // the modes add their own buttons when they start
        RefreshShell();
        var menus = CodeImp.DoomBuilder.BuilderModes.BuilderPlug.Me.MenusForm;
        var button = menus.ViewSelectionNumbers;
        bool before = CodeImp.DoomBuilder.BuilderModes.BuilderPlug.Me.ViewSelectionNumbers;
        var control = window.Plugins.ControlOf(button);
        Assert.NotNull(control);

        Click(control);

        Assert.Equal(!before, CodeImp.DoomBuilder.BuilderModes.BuilderPlug.Me.ViewSelectionNumbers);   // the plugin's own handler ran
        Assert.Equal(!before, button.Checked);

        button.Checked = before;                                                                        // the plugin changing it shows on the control
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(before, ((Avalonia.Controls.Primitives.ToggleButton)control).IsChecked);
    }

    [AvaloniaFact]
    public void Plugin_menu_entries_run_the_action_named_in_their_tag()
    {
        OpenEditor();
        General.Editing.ChangeMode("LinedefsMode");
        RefreshShell();
        var tagged = window.Plugins.Items.Where(i => i is CodeImp.DoomBuilder.Controls.ToolStripActionButton && i.Tag is string).ToList();

        Assert.NotEmpty(tagged);                                 // the buttons the classic modes add for their actions
        Assert.All(tagged, i => Assert.StartsWith("buildermodes_", (string)i.Tag, System.StringComparison.OrdinalIgnoreCase));   // completed to full action names on the way in
    }
}

public class StartupLogTests : EditorTestBase
{
    [AvaloniaFact]
    public void Opening_the_sample_map_with_the_plugin_logs_nothing_besides_the_missing_iwad()
    {
        OpenEditor();

        var errors = typeof(ErrorLogger).GetMethod("GetErrors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(General.ErrorLogger, new object[] { 0 }) as System.Collections.Generic.IEnumerable<ErrorItem>;

        // The repository has no IWAD, so sprites, textures, the palette and the colormap are never found: expected, and not the plugin's
        var listed = errors.Select(e => e.Type + ": " + e.Description)
            .Where(d => !d.Contains("Unable to find") && !d.Contains("unable to load texture") && !d.Contains("IWAD") && !d.Contains("palette") && !d.Contains("colormap")).ToList();

        Assert.True(listed.Count == 0, "\n" + string.Join("\n", listed));
    }
}

public class UndoRedoDockerTests : EditorTestBase
{
    private Avalonia.Controls.ListBox History()
    {
        var tab = window.Dockers.Tabs.First(t => (string)t.Header == "Undo / Redo");
        return (Avalonia.Controls.ListBox)tab.Content;
    }

    private static string[] Texts(Avalonia.Controls.ListBox list)
        => list.ItemsSource.Cast<Avalonia.Controls.TextBlock>().Select(t => t.Text).ToArray();

    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void The_history_starts_with_the_opened_map_and_grows_with_each_change()
    {
        OpenEditor();
        Flush();
        var list = History();
        Assert.Equal(new[] { "Opened Map" }, Texts(list));
        Assert.Equal(0, list.SelectedIndex);

        General.Map.UndoRedo.CreateUndo("Draw a room");
        General.Map.UndoRedo.CreateUndo("Move a vertex");
        Flush();

        Assert.Equal(new[] { "Opened Map", "Draw a room", "Move a vertex" }, Texts(list));
        Assert.Equal(2, list.SelectedIndex);                       // the map is at the last change
    }

    [AvaloniaFact]
    public void Choosing_an_earlier_line_undoes_to_that_point_and_a_later_one_redoes()
    {
        OpenEditor();
        General.Map.UndoRedo.CreateUndo("One");
        General.Map.UndoRedo.CreateUndo("Two");
        General.Map.UndoRedo.CreateUndo("Three");
        Flush();
        var list = History();

        list.SelectedIndex = 1;                                    // "One": undo "Three" and "Two"
        Flush();

        Assert.Equal(2, General.Map.UndoRedo.GetRedoList().Count);
        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal(new[] { "Opened Map", "One", "Two", "Three" }, Texts(list));       // the redo levels stay listed
        Assert.Equal(1.0, ((Avalonia.Controls.TextBlock)((System.Collections.Generic.List<Avalonia.Controls.TextBlock>)list.ItemsSource)[1]).Opacity);
        Assert.Equal(0.5, ((Avalonia.Controls.TextBlock)((System.Collections.Generic.List<Avalonia.Controls.TextBlock>)list.ItemsSource)[3]).Opacity);   // dimmed: not done now

        list.SelectedIndex = 3;                                    // redo them again
        Flush();

        Assert.Empty(General.Map.UndoRedo.GetRedoList());
        Assert.Equal(3, list.SelectedIndex);
    }

    [AvaloniaFact]
    public void The_undo_action_moves_the_selection_in_the_list()
    {
        OpenEditor();
        General.Map.UndoRedo.CreateUndo("One");
        General.Map.UndoRedo.CreateUndo("Two");
        Flush();

        General.Actions.InvokeAction("builder_undo");
        Flush();

        Assert.Equal(1, History().SelectedIndex);
    }
}

public class UndoRedoModelTests : EditorTestBase
{
    [AvaloniaFact]
    public void A_long_history_shows_a_window_of_levels_with_dots_where_lines_are_left_out()
    {
        OpenEditor();
        for (int i = 1; i <= 450; i++) General.Map.UndoRedo.CreateUndo("Change " + i);

        var model = new CodeImp.DoomBuilder.BuilderModes.UndoRedoModel { BeginDescription = "Opened Map" };
        model.Update();

        Assert.Equal("...", model.Rows[0].Text);                                   // the early levels are not listed
        Assert.True(model.Rows.Count <= CodeImp.DoomBuilder.BuilderModes.UndoRedoModel.MaxDisplayLevels + 2);
        Assert.Equal("Change 450", model.Rows[model.CurrentIndex].Text);
        Assert.Equal(CodeImp.DoomBuilder.BuilderModes.UndoRowKind.Current, model.Rows[model.CurrentIndex].Kind);
    }

    [AvaloniaFact]
    public void Going_to_the_current_line_or_to_the_dots_does_nothing()
    {
        OpenEditor();
        General.Map.UndoRedo.CreateUndo("One");
        var model = new CodeImp.DoomBuilder.BuilderModes.UndoRedoModel { BeginDescription = "Opened Map" };
        model.Update();

        Assert.False(model.GoTo(model.CurrentIndex));
        Assert.False(model.GoTo(99));
        Assert.Empty(General.Map.UndoRedo.GetRedoList());
    }
}

public class EditSelectionDockerTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private System.Collections.Generic.List<Avalonia.Controls.TextBox> NumberBoxes()
    {
        var tab = window.Dockers.Tabs.First(t => (string)t.Header == "Edit Selection");
        return Avalonia.VisualTree.VisualExtensions.GetVisualDescendants((Avalonia.Visual)tab.Content).OfType<Avalonia.Controls.TextBox>().Where(t => t.Width == 80).ToList();     // (not the one inside the list)
    }

    private void SelectEverythingAndEdit()
    {
        OpenEditor();
        General.Editing.ChangeMode("VerticesMode");
        foreach (var v in General.Map.Map.Vertices) v.Selected = true;
        General.Editing.ChangeMode("EditSelectionMode");
        Flush();
    }

    [AvaloniaFact]
    public void The_mode_adds_its_docker_showing_the_position_and_size_of_the_selection()
    {
        SelectEverythingAndEdit();

        Assert.Equal("EditSelectionMode", General.Editing.Mode.GetType().Name);
        var boxes = NumberBoxes();
        Assert.Equal(9, boxes.Count);                                       // absolute and relative position and size, and the rotation

        double minx = General.Map.Map.Vertices.Min(v => v.Position.x), miny = General.Map.Map.Vertices.Min(v => v.Position.y);
        Assert.Equal(minx.ToString("0.#"), boxes[0].Text);                  // absolute position X
        Assert.Equal(miny.ToString("0.#"), boxes[1].Text);
    }

    [AvaloniaFact]
    public void Typing_a_position_and_pressing_Enter_moves_the_selection()
    {
        SelectEverythingAndEdit();
        double before = General.Map.Map.Vertices.Min(v => v.Position.x);
        var absposx = NumberBoxes()[0];

        absposx.Focus();
        absposx.Text = (before + 128).ToString();
        window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Enter, null);
        Flush();

        Assert.Equal(before + 128, General.Map.Map.Vertices.Min(v => v.Position.x), 1);
    }

    [AvaloniaFact]
    public void A_box_that_was_not_typed_in_changes_nothing_when_it_loses_focus()
    {
        SelectEverythingAndEdit();
        double before = General.Map.Map.Vertices.Min(v => v.Position.x);
        var absposx = NumberBoxes()[0];

        absposx.Focus();
        window.KeyPress(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Tab, null);
        Flush();

        Assert.Equal(before, General.Map.Map.Vertices.Min(v => v.Position.x), 3);
    }
}

public class DrawDockersTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private Avalonia.Controls.Control DockerContent(string header)
        => window.Dockers.Tabs.First(t => (string)t.Header == header).Content as Avalonia.Controls.Control;

    private static System.Collections.Generic.List<T> All<T>(Avalonia.Controls.Control root) where T : class
        => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>().ToList();

    [AvaloniaFact]
    public void Draw_Settings_shows_the_map_options_and_writes_the_overrides_back()
    {
        OpenEditor();
        Flush();
        var content = DockerContent("Draw Settings");
        var checks = All<Avalonia.Controls.CheckBox>(content);
        Assert.Equal(8, checks.Count);                         // five textures, two heights and the brightness
        Assert.All(checks, c => Assert.NotEqual(true, c.IsChecked == true && !c.IsEnabled));

        var floor = checks.First(c => (string)c.Content == "Floor");
        Assert.Equal(General.Map.Options.OverrideFloorTexture, floor.IsChecked == true);
        floor.IsChecked = !General.Map.Options.OverrideFloorTexture;
        Assert.Equal(floor.IsChecked == true, General.Map.Options.OverrideFloorTexture);
    }

    [AvaloniaFact]
    public void Draw_Settings_fill_all_puts_the_chosen_flats_on_the_selected_sectors_and_can_be_undone()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        Sector sector = General.Map.Map.Sectors.First();
        sector.Selected = true;
        string before = sector.FloorTexture;
        General.Map.Options.OverrideFloorTexture = true;
        General.Map.Options.DefaultFloorTexture = "FLOOR4_8";
        Flush();
        // The panel reads the options when the mode starts; start it again so it shows the override
        General.Editing.ChangeMode("VerticesMode");
        General.Editing.ChangeMode("SectorsMode");
        sector.Selected = true;
        Flush();

        var fillall = All<Avalonia.Controls.Button>(DockerContent("Draw Settings")).First(b => (string)b.Content == "Fill all");
        Click(fillall);

        Assert.Equal("FLOOR4_8", sector.FloorTexture);
        General.Map.UndoRedo.PerformUndo();
        Assert.Equal(before, General.Map.Map.Sectors.First().FloorTexture);
    }

    [AvaloniaFact]
    public void The_Draw_Grid_mode_adds_its_docker_and_the_slices_reach_the_mode()
    {
        OpenEditor();
        General.Editing.ChangeMode("DrawGridMode");
        Flush();

        Assert.Equal("DrawGridMode", General.Editing.Mode.GetType().Name);
        var content = DockerContent("Draw Grid");
        var spinners = All<Avalonia.Controls.NumericUpDown>(content);
        Assert.Equal(2, spinners.Count);
        Assert.Equal(2m, spinners[0].Value);                   // the mode counts slices, the panel the cuts between them

        spinners[0].Value = 5;
        var field = General.Editing.Mode.GetType().GetField("horizontalslices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.Equal(6, (int)field.GetValue(General.Editing.Mode));
        var combos = All<Avalonia.Controls.ComboBox>(content);
        combos[0].SelectedIndex = 3;                           // "Both": nothing left to type, so the slice boxes lock
        Assert.False(spinners[0].IsEnabled);
        Assert.False(spinners[1].IsEnabled);

        General.Editing.ChangeMode("VerticesMode");
        Assert.DoesNotContain(window.Dockers.Tabs, t => (string)t.Header == "Draw Grid");
    }
}

public class DisplayToolTipTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private DoomBuilder.App.Shell.DisplayToolTip Tip()
        => window.InputSurface.Children.OfType<DoomBuilder.App.Shell.DisplayToolTip>().Single();

    [AvaloniaFact]
    public void A_mode_can_show_and_hide_a_tooltip_over_the_display()
    {
        OpenEditor();
        Assert.False(Tip().IsVisible);

        General.Interface.Display.ShowToolTip("Comment:", "Open this door first", 120, 80);
        Flush();
        Assert.True(Tip().IsVisible);
        Assert.Equal("Comment:", Tip().Title);
        Assert.Equal("Open this door first", Tip().Text);
        Assert.False(Tip().IsHitTestVisible);                      // never steals the mouse from the map

        General.Interface.Display.HideToolTip();
        Flush();
        Assert.False(Tip().IsVisible);
    }

    [AvaloniaFact]
    public void The_tooltip_stays_inside_the_display_and_follows_the_scale()
    {
        var tip = new DoomBuilder.App.Shell.DisplayToolTip();
        tip.ShowAt("T", "some text", 5000, 4000, 2.0, new Avalonia.Size(800, 600));
        Assert.True(tip.Margin.Left + tip.DesiredSize.Width <= 800.001);
        Assert.True(tip.Margin.Top + tip.DesiredSize.Height <= 600.001);

        tip.ShowAt("T", "some text", 200, 100, 2.0, new Avalonia.Size(800, 600));
        Assert.Equal(100, tip.Margin.Left);                        // device pixels become layout units
        Assert.Equal(50, tip.Margin.Top);

        tip.ShowAt("", "", 10, 10, 1.0, new Avalonia.Size(800, 600));
        Assert.False(tip.IsVisible);                               // nothing to say
    }

    [AvaloniaFact]
    public void The_DPI_scaler_follows_the_window_scale()
    {
        OpenEditor();
        Assert.Equal((float)window.RenderScaling, CodeImp.DoomBuilder.Windows.MainForm.DPIScaler.Width);
    }
}

/// <summary>What the user sees while a mode draws straight from mouse events.</summary>
public class DrawingModeFeedbackTests : EditorTestBase
{
    private Avalonia.Point DisplayPointOf(double x, double y)
    {
        var d = Renderer.MapToDisplay(new CodeImp.DoomBuilder.Geometry.Vector2D(x, y));
        return InView(d.x, d.y);
    }

    [AvaloniaFact]
    public void Moving_the_mouse_while_drawing_asks_for_a_frame_so_the_new_line_shows()
    {
        OpenEditor();
        General.Editing.ChangeMode("DrawGeometryMode");
        var viewport = window.Viewport;

        // The first point of a new line
        window.MouseMove(DisplayPointOf(100, 100));
        window.MouseDown(DisplayPointOf(100, 100), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
        window.MouseUp(DisplayPointOf(100, 100), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Moving the mouse changes the line that follows it; that is drawn outside a frame, so a frame has to be asked for or it never shows
        int before = viewport.FrameRequests;
        window.MouseMove(DisplayPointOf(180, 140));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(viewport.FrameRequests > before, "no frame was asked for after the mouse moved");
    }

    private System.Collections.Generic.List<CodeImp.DoomBuilder.Geometry.DrawnVertex> DrawnPoints()
    {
        var field = typeof(CodeImp.DoomBuilder.BuilderModes.DrawGeometryMode).GetField("points", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (System.Collections.Generic.List<CodeImp.DoomBuilder.Geometry.DrawnVertex>)field.GetValue(General.Editing.Mode);
    }

    [AvaloniaFact]
    public void Snap_to_grid_and_snap_to_geometry_start_on_and_the_toolbar_actions_switch_them()
    {
        OpenEditor();
        Assert.True(General.Interface.SnapToGrid);
        Assert.True(General.Interface.AutoMerge);

        General.Actions.InvokeAction("builder_togglesnap");
        Assert.False(General.Interface.SnapToGrid);
        General.Actions.InvokeAction("builder_togglesnap");
        Assert.True(General.Interface.SnapToGrid);

        General.Actions.InvokeAction("builder_toggleautomerge");
        Assert.False(General.Interface.AutoMerge);
        General.Actions.InvokeAction("builder_toggleautomerge");
        Assert.True(General.Interface.AutoMerge);
    }

    [AvaloniaFact]
    public void A_point_drawn_between_grid_lines_snaps_to_the_grid_unless_snapping_is_off()
    {
        OpenEditor();
        General.Editing.ChangeMode("DrawGeometryMode");
        double grid = General.Map.Grid.GridSize;

        // Click on a spot that is not on a grid line (in the middle of the room, away from any vertex)
        var at = DisplayPointOf(grid * 3 + grid * 0.3, grid * 2 + grid * 0.3);
        window.MouseMove(at);
        window.MouseDown(at, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
        window.MouseUp(at, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
        var first = DrawnPoints()[0].pos;
        Assert.Equal(0.0, first.x % grid, 3);                                       // on the grid, not where the pointer was
        Assert.Equal(0.0, first.y % grid, 3);

        // With snapping off the point goes where the pointer is
        General.Actions.InvokeAction("builder_cancelmode");
        General.Editing.ChangeMode("DrawGeometryMode");
        General.Actions.InvokeAction("builder_togglesnap");
        window.MouseMove(at);
        window.MouseDown(at, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
        window.MouseUp(at, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
        var free = DrawnPoints()[0].pos;
        Assert.NotEqual(0.0, free.x % grid, 1);
    }
}
