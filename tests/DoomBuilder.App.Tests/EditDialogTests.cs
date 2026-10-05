using System.Linq;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.App.Dialogs;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The element edit dialogs, driven through the real window (they change the map live and Cancel withdraws it).</summary>
public class EditDialogTests : EditorTestBase
{
    private static void Ok(EditDialogBase d) => Click(Find<Avalonia.Controls.Button>(d, "OK"));
    private static void Cancel(EditDialogBase d) => Click(Find<Avalonia.Controls.Button>(d, "Cancel"));

    private static T Find<T>(EditDialogBase d, string text) where T : Avalonia.Controls.Button
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(d).OfType<T>().First(b => (string)b.Content == text);

    [AvaloniaFact]
    public void Editing_one_vertex_moves_it_and_one_undo_puts_it_back()
    {
        OpenEditor();
        Vertex v = General.Map.Map.Vertices.First();
        double oldx = v.Position.x;
        WhenShown<VertexEditWindow>(d =>
        {
            Assert.Equal(oldx.ToString(), d.XBox.Text);
            d.XBox.Text = "1000";
            Ok(d);
        });

        var result = General.Interface.ShowEditVertices(new[] { v });

        Assert.Equal(System.Windows.Forms.DialogResult.OK, result);
        Assert.Equal(1000, v.Position.x);
        General.Map.UndoRedo.PerformUndo();
        Assert.Equal(oldx, v.Position.x);
    }

    [AvaloniaFact]
    public void Cancelling_the_vertex_dialog_withdraws_the_live_changes()
    {
        OpenEditor();
        Vertex v = General.Map.Map.Vertices.First();
        double oldx = v.Position.x;
        bool wasChanged = General.Map.IsChanged;
        WhenShown<VertexEditWindow>(d =>
        {
            d.XBox.Text = "777";
            Assert.Equal(777, v.Position.x);      // live
            Cancel(d);
        });

        var result = General.Interface.ShowEditVertices(new[] { v });

        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, result);
        Assert.Equal(oldx, v.Position.x);
        Assert.Equal(wasChanged, General.Map.IsChanged);
    }

    [AvaloniaFact]
    public void Several_vertices_show_an_empty_box_when_they_differ_and_a_relative_value_moves_each()
    {
        OpenEditor();
        var two = General.Map.Map.Vertices.Where(v => v.Position.x != General.Map.Map.Vertices.First().Position.x).Take(1)
            .Concat(new[] { General.Map.Map.Vertices.First() }).ToList();
        var before = two.Select(v => v.Position.x).ToList();
        WhenShown<VertexEditWindow>(d =>
        {
            Assert.Equal("", d.XBox.Text);
            d.XBox.Text = "++10";
            Ok(d);
        });

        General.Interface.ShowEditVertices(two);

        for (int i = 0; i < two.Count; i++) Assert.Equal(before[i] + 10, two[i].Position.x);
    }

    [AvaloniaFact]
    public void The_position_boxes_are_disabled_when_the_position_may_not_change()
    {
        OpenEditor();
        WhenShown<VertexEditWindow>(d =>
        {
            Assert.False(d.XBox.IsEnabled);
            Assert.False(d.YBox.IsEnabled);
            Cancel(d);
        });

        General.Interface.ShowEditVertices(new[] { General.Map.Map.Vertices.First() }, false);
    }

    [AvaloniaFact]
    public void Editing_a_sector_changes_heights_brightness_and_tag_and_cancel_withdraws_them()
    {
        OpenEditor();
        Sector sector = General.Map.Map.Sectors.First();
        int ceil = sector.CeilHeight, floor = sector.FloorHeight, light = sector.Brightness;

        WhenShown<SectorEditWindow>(d =>
        {
            Assert.Equal(ceil.ToString(), d.CeilingHeightBox.Text);
            Assert.Equal((ceil - floor).ToString(), d.SectorHeightText);
            d.CeilingHeightBox.Text = (ceil + 64).ToString();
            d.BrightnessBox.Text = "96";
            Assert.Equal(ceil + 64, sector.CeilHeight);      // live
            Assert.Equal(96, sector.Brightness);
            Cancel(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, General.Interface.ShowEditSectors(new[] { sector }));
        Assert.Equal(ceil, sector.CeilHeight);
        Assert.Equal(light, sector.Brightness);

        WhenShown<SectorEditWindow>(d =>
        {
            d.CeilingHeightBox.Text = "++16";
            d.Tag.SetTag(7);
            Ok(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, General.Interface.ShowEditSectors(new[] { sector }));
        Assert.Equal(ceil + 16, sector.CeilHeight);
        Assert.Equal(7, sector.Tag);
    }

    [AvaloniaFact]
    public void An_invalid_tag_keeps_the_sector_dialog_open_with_a_warning()
    {
        OpenEditor();
        Sector sector = General.Map.Map.Sectors.First();
        int oldtag = sector.Tag;
        string warning = null;
        WhenShown<MessageBoxWindow>(box => { warning = box.Message; Click(box.ButtonFor(System.Windows.Forms.DialogResult.OK)); });

        WhenShown<SectorEditWindow>(d =>
        {
            d.Tag.SetTag(General.Map.FormatInterface.MaxTag + 1);
            Ok(d);                                    // refused: a warning, and the dialog stays
            Assert.True(d.IsVisible);
            Cancel(d);
        });
        General.Interface.ShowEditSectors(new[] { sector });

        Assert.Equal(oldtag, sector.Tag);
        Assert.Contains("Sector tag must be between", warning);
    }

    [AvaloniaFact]
    public void The_udmf_sector_dialog_edits_flags_and_custom_fields()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        Assert.True(General.Map.UDMF);
        Sector sector = General.Map.Map.Sectors.Single();

        WhenShown<SectorEditWindow>(d =>
        {
            Assert.Equal("5", d.Tag.Text);
            var flag = d.Flags.Boxes.First(b => ((CodeImp.DoomBuilder.Windows.FlagItem)b.Tag).Key == "silent");
            Assert.False(flag.IsChecked);
            flag.IsChecked = true;
            Assert.True(sector.IsFlagSet("silent"));                     // live

            d.Fields.Model.AddField("user_note", out var row);
            Assert.NotNull(row);
            d.Fields.Model.SetType(row, "Text");
            d.Fields.Model.SetValue(row, "hello");
            Ok(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, General.Interface.ShowEditSectors(new[] { sector }));

        Assert.True(sector.IsFlagSet("silent"));
        Assert.Equal("hello", sector.Fields["user_note"].Value);
    }

    [AvaloniaFact]
    public void Editing_a_thing_changes_position_angle_and_type_live_and_cancel_withdraws_them()
    {
        OpenEditor();
        Thing thing = General.Map.Map.Things.First();
        double x = thing.Position.x;
        int angle = thing.AngleDoom, type = thing.Type;
        int othertype = type == 3004 ? 3001 : 3004;

        WhenShown<ThingEditWindow>(d =>
        {
            Assert.Equal(((int)x).ToString(), d.PosX.Text);
            Assert.Equal(type, d.TypeBrowser.GetResult(-1));
            d.PosX.Text = "500";
            Assert.Equal(500, thing.Position.x);                 // live
            d.Angle.Text = "90";
            Assert.Equal(90, thing.AngleDoom);
            Assert.Equal(90, d.Dial.Angle);
            d.TypeBrowser.SelectType(othertype);
            Assert.Equal(othertype, thing.Type);
            Cancel(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, General.Interface.ShowEditThings(new[] { thing }));
        Assert.Equal(x, thing.Position.x);
        Assert.Equal(angle, thing.AngleDoom);
        Assert.Equal(type, thing.Type);
    }

    [AvaloniaFact]
    public void Accepting_the_thing_dialog_keeps_the_changes_and_writes_the_flags()
    {
        OpenEditor();
        Thing thing = General.Map.Map.Things.First();
        string flag = General.Map.Config.ThingFlags.Keys.First();
        bool before = thing.IsFlagSet(flag);

        WhenShown<ThingEditWindow>(d =>
        {
            var box = d.Flags.Boxes.First(b => ((CodeImp.DoomBuilder.Windows.FlagItem)b.Tag).Key == flag);
            box.IsChecked = !before;
            d.PosY.Text = "321";
            Ok(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, General.Interface.ShowEditThings(new[] { thing }));

        Assert.Equal(!before, thing.IsFlagSet(flag));
        Assert.Equal(321, thing.Position.y);
        General.Map.UndoRedo.PerformUndo();
        Assert.Equal(before, thing.IsFlagSet(flag));
    }

    [AvaloniaFact]
    public void The_thing_browser_filters_the_types_by_title()
    {
        OpenEditor();
        var model = new CodeImp.DoomBuilder.Windows.ThingBrowserModel();
        Assert.NotEmpty(model.Roots);
        model.Filter = "zombie";
        Assert.All(model.Roots, n => Assert.Contains("ZOMBIE", n.Title.ToUpperInvariant()));
        model.Filter = "";
        model.SelectType(3004);
        Assert.NotNull(model.Info);
        Assert.Equal("Floor", model.PositionText);
        Assert.Single(model.Selected);
    }

    [AvaloniaFact]
    public void Editing_a_linedef_changes_flags_textures_and_offsets_live_and_cancel_withdraws_them()
    {
        OpenEditor();
        Linedef line = General.Map.Map.Linedefs.First(l => l.Front != null);
        string flag = General.Map.Config.LinedefFlags.Keys.First();
        bool flagbefore = line.IsFlagSet(flag);
        string midbefore = line.Front.MiddleTexture;
        int offsetbefore = line.Front.OffsetX;

        WhenShown<LinedefEditWindow>(d =>
        {
            d.Flags.Boxes.First(b => ((CodeImp.DoomBuilder.Windows.FlagItem)b.Tag).Key == flag).IsChecked = !flagbefore;
            Assert.Equal(!flagbefore, line.IsFlagSet(flag));         // live
            d.Front.Middle.TextureName = "NEWTEX";
            Assert.Equal("NEWTEX", line.Front.MiddleTexture);
            d.Front.OffsetX.Text = "++8";
            Assert.Equal(offsetbefore + 8, line.Front.OffsetX);
            Cancel(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.Cancel, General.Interface.ShowEditLinedefs(new[] { line }));

        Assert.Equal(flagbefore, line.IsFlagSet(flag));
        Assert.Equal(midbefore, line.Front.MiddleTexture);
        Assert.Equal(offsetbefore, line.Front.OffsetX);
    }

    [AvaloniaFact]
    public void Accepting_the_linedef_dialog_writes_the_tag_and_action()
    {
        OpenEditor();
        Linedef line = General.Map.Map.Linedefs.First(l => l.Front != null);

        WhenShown<LinedefEditWindow>(d =>
        {
            Assert.Equal(line.Action, d.Action.Value);
            d.Action.Value = 1;
            d.Tag.SetTag(12);
            Ok(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, General.Interface.ShowEditLinedefs(new[] { line }));

        Assert.Equal(1, line.Action);
        Assert.Equal(12, line.Tag);
    }

    [AvaloniaFact]
    public void Unchecking_the_back_side_removes_it_and_several_lines_with_different_sides_show_a_mixed_box()
    {
        OpenEditor();
        Linedef two = General.Map.Map.Linedefs.First(l => l.Front != null && l.Back != null);
        Linedef one = General.Map.Map.Linedefs.First(l => l.Front != null && l.Back == null);

        WhenShown<LinedefEditWindow>(d =>
        {
            Assert.Null(d.Back.Exists.IsChecked);                    // some have it, some do not
            Cancel(d);
        });
        General.Interface.ShowEditLinedefs(new[] { two, one });

        WhenShown<LinedefEditWindow>(d =>
        {
            Assert.True(d.Back.Exists.IsChecked);
            d.Back.Exists.IsChecked = false;
            Ok(d);
        });
        General.Interface.ShowEditLinedefs(new[] { two });
        Assert.Null(two.Back);
    }

    [AvaloniaFact]
    public void The_udmf_linedef_and_thing_dialogs_open_with_their_extra_tabs()
    {
        OpenEditor(wadPath: WriteUdmfWad(UdmfSample), config: "GZDoom_DoomUDMF.cfg");
        Linedef line = General.Map.Map.Linedefs.First();
        Thing thing = General.Map.Map.Things.Single();

        WhenShown<LinedefEditWindow>(d =>
        {
            Assert.NotNull(d.Front.Flags);
            d.Fields.Model.AddField("user_a", out var row);
            d.Fields.Model.SetType(row, "Integer");
            d.Fields.Model.SetValue(row, "42");
            Ok(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, General.Interface.ShowEditLinedefs(new[] { line }));
        Assert.Equal(42, line.Fields["user_a"].Value);

        WhenShown<ThingEditWindow>(d =>
        {
            Assert.Equal(1, d.TypeBrowser.GetResult(-1));
            Assert.Equal("90", d.Angle.Text);
            d.Fields.Model.AddField("user_b", out var row);
            d.Fields.Model.SetType(row, "Text");
            d.Fields.Model.SetValue(row, "x");
            Ok(d);
        });
        Assert.Equal(System.Windows.Forms.DialogResult.OK, General.Interface.ShowEditThings(new[] { thing }));
        Assert.Equal("x", thing.Fields["user_b"].Value);
    }

    [AvaloniaFact]
    public void Right_clicking_a_sector_in_the_sectors_mode_opens_the_sector_dialog()
    {
        OpenEditor();
        General.Editing.ChangeMode("SectorsMode");
        Sector sector = General.Map.Map.Sectors.First();
        // A point inside: the middle of its first triangle
        var tri = sector.Triangles.Vertices;
        var inside = new CodeImp.DoomBuilder.Geometry.Vector2D((tri[0].x + tri[1].x + tri[2].x) / 3, (tri[0].y + tri[1].y + tri[2].y) / 3);
        var d0 = Renderer.MapToDisplay(inside);
        var at = InView(d0.x, d0.y);
        Sector shown = null;
        WhenShown<SectorEditWindow>(d => { shown = d.Model.Sectors.First(); Cancel(d); });

        window.MouseMove(at);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.MouseDown(at, Avalonia.Input.MouseButton.Right, Avalonia.Input.RawInputModifiers.None);
        window.MouseUp(at, Avalonia.Input.MouseButton.Right, Avalonia.Input.RawInputModifiers.None);

        Assert.Same(sector, shown);
    }
}
