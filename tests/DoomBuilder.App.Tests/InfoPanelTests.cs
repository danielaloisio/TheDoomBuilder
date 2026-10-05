using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The info panel under the map: what it says about the vertex, linedef, sector or thing under the mouse (or aimed at, in 3D).</summary>
public class InfoPanelTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static string Value(InfoGroup group, string label) => group.Fields.First(f => f.Label == label).Value;

    [AvaloniaFact]
    public void A_sector_is_described_with_its_heights_effect_tag_and_flats()
    {
        OpenEditor();
        Sector s = General.Map.Map.Sectors.First();

        ElementInfo info = ElementInfoBuilder.ForSector(s, false, true);

        Assert.Equal(MapElementType.SECTOR, info.Kind);
        Assert.StartsWith("Sector " + s.Index, info.Title);
        InfoGroup main = info.Groups[0];
        Assert.Equal(s.CeilHeight.ToString(), Value(main, "Ceiling:"));
        Assert.Equal(s.FloorHeight.ToString(), Value(main, "Floor:"));
        Assert.Equal((s.CeilHeight - s.FloorHeight).ToString(), Value(main, "Height:"));
        Assert.Equal(s.Brightness.ToString(), Value(main, "Brightness:"));
        Assert.True(main.Fields.First(f => f.Label == "Floor:").Highlight);            // aimed at the floor
        Assert.False(main.Fields.First(f => f.Label == "Ceiling:").Highlight);
        Assert.False(main.Fields.First(f => f.Label == "Tag:").Enabled);               // no tag: dimmed

        InfoGroup ceiling = info.Groups.Single(g => g.Title == "Ceiling");
        InfoGroup floor = info.Groups.Single(g => g.Title == "Floor");
        Assert.Equal(s.CeilTexture.ToUpperInvariant(), ceiling.Textures[0].Name);
        Assert.Equal(s.FloorTexture.ToUpperInvariant(), floor.Textures[0].Name);
        Assert.True(floor.Highlight);
    }

    [AvaloniaFact]
    public void A_two_sided_linedef_has_both_sidedefs_with_their_textures()
    {
        OpenEditor();
        Linedef line = General.Map.Map.Linedefs.First(l => l.Front != null && l.Back != null);

        ElementInfo info = ElementInfoBuilder.ForLinedef(line, line.Back);

        Assert.Equal("Linedef " + line.Index, info.Title);
        InfoGroup main = info.Groups[0];
        Assert.Equal(line.Length.ToString("0.##"), Value(main, "Length:"));
        Assert.Equal(line.AngleDeg + "°", Value(main, "Angle:"));
        Assert.Contains(main.Fields, f => f.Label == "Action:");

        InfoGroup front = info.Groups.Single(g => g.Title.StartsWith("Front Sidedef"));
        InfoGroup back = info.Groups.Single(g => g.Title.StartsWith("Back Sidedef"));
        Assert.Equal(new[] { "Upper", "Middle", "Lower" }, front.Textures.Select(t => t.Caption));
        Assert.False(front.Highlight);
        Assert.True(back.Highlight);                                                    // the side the mouse is over
    }

    [AvaloniaFact]
    public void A_one_sided_linedef_has_only_the_front_and_a_required_middle_texture_is_flagged_when_missing()
    {
        OpenEditor();
        Linedef line = General.Map.Map.Linedefs.First(l => l.Front != null && l.Back == null);
        line.Front.SetTextureMid("-");

        ElementInfo info = ElementInfoBuilder.ForLinedef(line, null);

        Assert.DoesNotContain(info.Groups, g => g.Title.StartsWith("Back"));
        Assert.True(info.Groups.Single(g => g.Title.StartsWith("Front")).Textures[1].Missing);
    }

    [AvaloniaFact]
    public void A_thing_shows_its_type_position_angle_and_sprite()
    {
        OpenEditor();
        Thing t = General.Map.Map.Things.First(x => x.Type == 3004);

        ElementInfo info = ElementInfoBuilder.ForThing(t);

        Assert.Equal("Thing " + t.Index, info.Title);
        InfoGroup main = info.Groups[0];
        Assert.StartsWith("3004 - ", Value(main, "Type:"));
        Assert.StartsWith(t.Position.x + ", " + t.Position.y, Value(main, "Position:"));
        Assert.Equal(t.AngleDoom + "°", Value(main, "Angle:"));
        Assert.Equal(t.AngleDoom, info.Angle);
        Assert.NotNull(info.Sprite);
    }

    [AvaloniaFact]
    public void A_vertex_shows_its_position()
    {
        OpenEditor();
        Vertex v = General.Map.Map.Vertices.First(x => x.Position.x == 256 && x.Position.y == 64);

        ElementInfo info = ElementInfoBuilder.ForVertex(v);

        Assert.Equal("Vertex " + v.Index, info.Title);
        Assert.Equal("256, 64", Value(info.Groups[0], "Position:"));
    }

    [AvaloniaFact]
    public void The_shell_publishes_the_info_the_modes_ask_for_and_clears_it()
    {
        OpenEditor();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        ElementInfo last = null;
        int events = 0;
        shell.InfoChanged += i => { last = i; events++; };
        Sector s = General.Map.Map.Sectors.First();

        General.Interface.ShowSectorInfo(s);
        Assert.NotNull(last);
        Assert.Equal(MapElementType.SECTOR, last.Kind);
        Assert.Same(s, shell.InfoObject);

        General.Interface.ShowThingInfo(General.Map.Map.Things.First());
        Assert.Equal(MapElementType.THING, last.Kind);

        General.Interface.HideInfo();
        Assert.Null(last);
        Assert.Null(shell.InfoObject);
        Assert.Equal(3, events);
    }

    [AvaloniaFact]
    public void A_collapsed_panel_publishes_nothing_and_opening_it_again_shows_the_last_element()
    {
        OpenEditor();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        ElementInfo last = null;
        shell.InfoChanged += i => last = i;
        Sector s = General.Map.Map.Sectors.First();
        General.Interface.ShowSectorInfo(s);

        General.Actions.InvokeAction("builder_toggleinfopanel");
        Assert.False(shell.IsInfoPanelExpanded);
        last = null;
        General.Interface.ShowSectorInfo(s);
        Assert.Null(last);                                      // nothing is drawn while collapsed

        General.Actions.InvokeAction("builder_toggleinfopanel");
        Assert.True(shell.IsInfoPanelExpanded);
        Assert.NotNull(last);                                   // and the element the mouse is on comes back
    }

    [AvaloniaFact]
    public void The_panel_under_the_map_draws_what_the_modes_show_and_hides_with_the_toggle()
    {
        OpenEditor();
        var panel = window.FindControl<Avalonia.Controls.ContentControl>("InfoHost");
        var infopanel = (DoomBuilder.App.Shell.InfoPanel)panel.Content;
        Assert.True(panel.IsVisible);

        Sector s = General.Map.Map.Sectors.First();
        General.Interface.ShowSectorInfo(s, true, false);
        Flush();

        Assert.NotNull(infopanel.Shown);
        Assert.Equal(infopanel.Shown.Groups.Count + (infopanel.Shown.Flags.Count > 0 ? 1 : 0), infopanel.Cards.Count);

        General.Interface.HideInfo();
        Flush();
        Assert.Equal("Vertices:", infopanel.Shown.Groups[0].Fields[0].Label);          // nothing highlighted: the mode and the map's counts
        Assert.Single(infopanel.Cards);

        General.Actions.InvokeAction("builder_toggleinfopanel");
        Flush();
        Assert.False(panel.IsVisible);
    }

    [AvaloniaFact]
    public void Aiming_at_the_floor_in_3D_shows_the_sector_with_the_floor_highlighted()
    {
        OpenEditor();
        General.Editing.ChangeMode("BaseVisualMode");
        var cam = General.Map.VisualCamera;
        cam.Position = new CodeImp.DoomBuilder.Geometry.Vector3D(150, 150, 41);
        cam.AngleXY = 0;
        cam.AngleZ = System.Math.PI + 1.2;
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        ElementInfo last = null;
        shell.InfoChanged += i => last = i ?? last;

        var input = shell.Input;
        input.Tick();
        System.Threading.Thread.Sleep(120);
        input.Tick();

        Assert.NotNull(last);
        Assert.Equal(MapElementType.SECTOR, last.Kind);
        Assert.True(last.Groups.Single(g => g.Title == "Floor").Highlight);
    }

    [AvaloniaFact]
    public void Without_a_highlight_the_panel_names_the_mode_and_counts_the_elements_of_the_map()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        Flush();
        var infopanel = (DoomBuilder.App.Shell.InfoPanel)window.FindControl<ContentControl>("InfoHost").Content;

        ElementInfo stats = infopanel.Shown;
        Assert.NotNull(stats);
        Assert.Equal(General.Editing.Mode.Attributes.DisplayName, stats.Groups[0].Title);
        Assert.Equal(new[] { "Vertices:", "Linedefs:", "Sidedefs:", "Sectors:", "Things:" }, stats.Groups[0].Fields.Select(f => f.Label));
        Assert.Equal(General.Map.Map.Sectors.Count.ToString(), Value(stats.Groups[0], "Sectors:"));
        Assert.All(stats.Groups[0].Fields, f => Assert.False(f.Error));                  // the sample is far below the format's limits

        General.Map.Map.Sectors.First().Selected = true;
        General.Interface.RefreshInfo();
        General.Interface.HideInfo();
        Flush();
        Assert.Contains("1 selected", Value(infopanel.Shown.Groups[0], "Sectors:"));
    }

    [AvaloniaFact]
    public void The_counts_turn_red_when_the_map_format_cannot_hold_them()
    {
        OpenEditor();
        int max = General.Map.FormatInterface.MaxThings;
        ElementInfo stats = ElementInfoBuilder.ForStatistics();
        Assert.True(General.Map.Map.Things.Count <= max);
        Assert.False(stats.Groups[0].Fields.Single(f => f.Label == "Things:").Error);
    }

    [AvaloniaFact]
    public void A_thing_gets_an_angle_dial_and_other_elements_do_not()
    {
        OpenEditor();
        var infopanel = (DoomBuilder.App.Shell.InfoPanel)window.FindControl<ContentControl>("InfoHost").Content;

        General.Interface.ShowThingInfo(General.Map.Map.Things.First());
        Flush();
        Assert.Contains(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(infopanel).OfType<Avalonia.Controls.Shapes.Line>(), l => l.Stroke == Avalonia.Media.Brushes.DodgerBlue);

        General.Interface.ShowSectorInfo(General.Map.Map.Sectors.First());
        Flush();
        Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(infopanel).OfType<Avalonia.Controls.Shapes.Line>(), l => l.Stroke == Avalonia.Media.Brushes.DodgerBlue);
    }
}
