using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.SoundPropagationMode;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The sound modes: sound propagation (which sectors a noise reaches) and sound environments (ZDoom reverb zones).</summary>
public class SoundPropagationTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private Point DisplayPointOf(Vector2D mappos)
    {
        Vector2D d = Renderer.MapToDisplay(mappos);
        return InView(d.x, d.y);
    }

    private static T Private<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

    // ---------------------------------------------------------------- sound propagation

    // Three rooms in a row; the line between the second and the third blocks sound
    private string OpenRooms()
    {
        Directory.CreateDirectory(dir);
        var map = new RowMap(new RowMap.Room(), new RowMap.Room(), new RowMap.Room()) { BorderFlags = { [2] = 64 } };
        string wad = map.Build().Write(Path.Combine(dir, "rooms.wad"));
        OpenEditor(wadPath: wad);
        return wad;
    }

    [AvaloniaFact]
    public void The_mode_groups_the_sectors_the_noise_goes_through_and_finds_what_it_wakes_up()
    {
        OpenRooms();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "SoundPropagationMode");
        General.Actions.InvokeAction("soundpropagationmode_soundpropagationmode");
        Flush();
        Assert.Equal("SoundPropagationMode", General.Editing.Mode.GetType().Name);

        // The mouse over the first room: its domain holds the rooms that are open to each other
        var rooms = General.Map.Map.Sectors.ToList();
        window.MouseMove(DisplayPointOf(new Vector2D(64, 64)));
        Flush();
        Assert.Same(rooms[0], General.Editing.Mode.HighlightedObject);

        var domains = Private<IDictionary>(General.Editing.Mode, "sector2domain");
        Assert.Equal(3, domains.Count);
        var d0 = (SoundPropagationDomain)domains[rooms[0]];
        var d1 = (SoundPropagationDomain)domains[rooms[1]];
        var d2 = (SoundPropagationDomain)domains[rooms[2]];
        Assert.Same(d0, d1);                                                // the line between them lets sound through
        Assert.NotSame(d0, d2);                                             // the one with the sound block flag does not
        Assert.Contains(rooms[2], d0.AdjacentSectors);                      // but the third room is next to them: monsters there still hear it
        Assert.Contains(rooms[1], d2.AdjacentSectors);
        Assert.NotEqual(d0.Color, d2.Color);

        // The blocking line is known
        Assert.Single(BuilderPlug.Me.BlockingLinedefs);

        // The things that the noise makes hunt for the player: all the things in the rooms it reaches (not the ones that wait in ambush)
        var hunting = Private<List<Thing>>(General.Editing.Mode, "huntingThings");
        Assert.Equal(General.Map.Map.Things.Count, hunting.Count);
    }

    [AvaloniaFact]
    public void The_colors_are_configured_in_a_dialog_and_kept()
    {
        OpenRooms();
        General.Actions.InvokeAction("soundpropagationmode_soundpropagationmode");
        Flush();
        Assert.EndsWith("soundpropagationcolorconfiguration", (string)BuilderPlug.Me.MenusForm.ColorConfiguration.Tag);

        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            Assert.Equal("Color Configuration", d.Title);
            var fields = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(d).OfType<ColorField>().ToList();
            Assert.Equal(5, fields.Count);
            fields[0].HexBox.Text = "#102030";
            fields[4].HexBox.Text = "#FF00FF";
            Click(d.OkButton);
        });
        General.Actions.InvokeAction("soundpropagationmode_soundpropagationcolorconfiguration");
        Flush();
        Assert.True(shown);
        Assert.Equal(new PixelColor(255, 0x10, 0x20, 0x30).ToInt(), BuilderPlug.Me.HighlightColor.ToInt());
        Assert.Equal(new PixelColor(255, 0xFF, 0, 0xFF).ToInt(), BuilderPlug.Me.BlockSoundColor.ToInt());
    }

    // ---------------------------------------------------------------- sound environments

    // Two rooms side by side, with a sound zone boundary between them and a sound environment thing (type 9048) in each.
    // Room A also has a boundary flag on one of its outer walls: a single-sided line with the flag, which is a mistake.
    private const string Zones = @"namespace = ""zdoom"";
thing { x = 64.0; y = 64.0; type = 1; angle = 0; skill1 = true; skill2 = true; single = true; }
thing { x = 32.0; y = 32.0; type = 9048; arg0 = 1; arg1 = 2; skill1 = true; skill2 = true; single = true; }
thing { x = 96.0; y = 96.0; type = 9048; arg0 = 1; arg1 = 2; dormant = true; skill1 = true; skill2 = true; single = true; }
thing { x = 192.0; y = 64.0; type = 9048; arg0 = 3; arg1 = 4; skill1 = true; skill2 = true; single = true; }
vertex { x = 0.0; y = 0.0; }
vertex { x = 0.0; y = 128.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 128.0; y = 0.0; }
vertex { x = 256.0; y = 128.0; }
vertex { x = 256.0; y = 0.0; }
linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; zoneboundary = true; }
linedef { v1 = 1; v2 = 2; sidefront = 1; blocking = true; }
linedef { v1 = 2; v2 = 3; sidefront = 2; sideback = 3; twosided = true; zoneboundary = true; }
linedef { v1 = 3; v2 = 0; sidefront = 4; blocking = true; }
linedef { v1 = 2; v2 = 4; sidefront = 5; blocking = true; }
linedef { v1 = 4; v2 = 5; sidefront = 6; blocking = true; }
linedef { v1 = 5; v2 = 3; sidefront = 7; blocking = true; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 0; texturemiddle = ""-""; }
sidedef { sector = 1; texturemiddle = ""-""; }
sidedef { sector = 0; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sidedef { sector = 1; texturemiddle = ""STARTAN1""; }
sector { heightfloor = 0; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
sector { heightfloor = 0; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
";

    private static SoundEnvironmentPanel Panel => (SoundEnvironmentPanel)typeof(SoundEnvironmentMode).GetProperty("SoundEnvironmentPanel", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

    private void OpenZones()
    {
        OpenEditor(wadPath: WriteUdmfWad(Zones), config: "GZDoom_DoomUDMF.cfg");
        General.Actions.InvokeAction("soundpropagationmode_soundenvironmentmode");
        Flush();
        Assert.Equal("SoundEnvironmentMode", General.Editing.Mode.GetType().Name);
        // The environments are found by a worker: wait for them
        // (and for the panel to be told that they are all there: that is the last thing the worker does)
        for (int i = 0; i < 1000 && (Panel.Nodes.Count < 2 || !BuilderPlug.Me.SoundEnvironmentIsUpdated || !((string)Panel.ShowWarningsOnly.Content).Contains("(")); i++) { Flush(); System.Threading.Thread.Sleep(10); }
        Flush();
    }

    [AvaloniaFact]
    public void The_sound_environments_are_listed_with_their_things_and_lines_and_the_mistakes_are_marked()
    {
        OpenZones();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        Assert.Contains(shell.Dockers.Dockers, d => d.Title == "Sound Environments");
        Assert.Equal("Sound Environments", General.Interface.ActiveDockerTabName);

        var panel = Panel;
        Assert.Equal(2, panel.Nodes.Count);
        Assert.Equal(2, BuilderPlug.Me.SoundEnvironments.Count);

        // Room A: two things (one dormant) and two boundary lines (the outer wall flagged by mistake, and the one between the rooms)
        var a = panel.Nodes.Single(n => ((SoundEnvironment)n.Tag).Sectors.Any(s => s.Index == 0));
        Assert.Equal("Things (2)", a.Nodes[0].Text);
        Assert.Equal("Linedefs (2)", a.Nodes[1].Text);
        Assert.Contains(a.Nodes[0].Nodes, n => n.Text.EndsWith("(dormant)"));
        Assert.Equal(panel.WarningIconIndex, a.ImageIndex);                                // the single-sided line with the flag
        Assert.Contains(a.Nodes[1].Nodes, n => n.ToolTipText != null && n.ToolTipText.Contains("single-sided"));
        Assert.Contains("(1 2)", a.Text);                                                  // named after the reverb of its active thing

        // Room B is fine
        var b = panel.Nodes.Single(n => ((SoundEnvironment)n.Tag).Sectors.Any(s => s.Index == 1));
        Assert.NotEqual(panel.WarningIconIndex, b.ImageIndex);
        Assert.Equal("Things (1)", b.Nodes[0].Text);
        Assert.Contains("(3 4)", b.Text);

        // Only the ones with warnings
        Assert.Contains("(1)", (string)panel.ShowWarningsOnly.Content);
        panel.ShowWarningsOnly.IsChecked = true;
        Flush();
        Assert.Equal(1, panel.Nodes.Count);
    }

    [AvaloniaFact]
    public void Clicking_a_node_centers_the_view_and_the_environment_under_the_mouse_is_opened()
    {
        OpenZones();
        var panel = Panel;
        var b = panel.Nodes.Single(n => ((SoundEnvironment)n.Tag).Sectors.Any(s => s.Index == 1));
        panel.NodeClicked(b);
        Assert.InRange(Renderer.OffsetX, 150, 230);                                         // the view moved to room B (x from 128 to 256)

        panel.Tree.SelectedItem = null;
        Flush();
        window.MouseMove(DisplayPointOf(new Vector2D(64, 64)));
        Flush();
        var a = panel.Nodes.Single(n => ((SoundEnvironment)n.Tag).Sectors.Any(s => s.Index == 0));
        Assert.True(a.Bold && a.IsExpanded, "room A is the highlighted environment");
        Assert.False(b.Bold);
    }

    [AvaloniaFact]
    public void The_reverb_picker_puts_the_choice_and_the_active_state_into_a_thing()
    {
        OpenZones();
        var thing = General.Map.Map.Things.First(t => t.Type == 9048 && t.IsFlagSet("dormant"));
        var picker = new ReverbsPickerForm(thing);
        Assert.False(picker.ActiveBox.IsChecked == true);                                  // the thing is dormant
        WhenShown<SimpleDialog>(d =>
        {
            Assert.Equal("Choose a Sound Environment", d.Title);
            picker.ActiveBox.IsChecked = true;
            Click(d.CancelButton);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, picker.ShowDialog());

        picker.ApplyTo(thing);                                                              // (nothing picked: only when a reverb is chosen)
        Assert.Equal(new[] { 1, 2 }, thing.Args.Take(2).ToArray());
        Assert.True(thing.IsFlagSet("dormant"));
    }
}
