using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.RejectExplorer;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The reject explorer plugin: a volatile mode that colors the sectors by what the REJECT lump says about the one under the mouse.</summary>
public class RejectExplorerTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private Point DisplayPointOf(Vector2D mappos)
    {
        Vector2D d = Renderer.MapToDisplay(mappos);
        return InView(d.x, d.y);
    }

    // A point inside the sector (the middle of its first triangle)
    private static Vector2D InsideOf(Sector s)
    {
        var v = s.FlatVertices;
        return new Vector2D((v[0].x + v[1].x + v[2].x) / 3, (v[0].y + v[1].y + v[2].y) / 3);
    }

    private static int[] Colors()
    {
        var field = typeof(RejectExplorerMode).GetField("overlayGeometry", BindingFlags.NonPublic | BindingFlags.Instance);
        var geometry = (FlatVertex[])field.GetValue(General.Editing.Mode);
        int pos = 0;
        return General.Map.Map.Sectors.Select(s => { int c = geometry[pos].c; pos += s.FlatVertices.Length; return c; }).ToArray();
    }

    // 4 sectors: bit (from * 4 + to) set means "cannot see". Sector 0 cannot see 1, sector 2 cannot see 0.
    private string WadWithReject() => SampleWadWithLump("REJECT", new byte[] { 0b0000_0010, 0b0000_0001 });

    [AvaloniaFact]
    public void The_mode_refuses_a_map_without_reject()
    {
        OpenEditor();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "RejectExplorerMode");
        General.Actions.InvokeAction("rejectexplorer_rejectexplorermode");
        Flush();
        Assert.NotEqual("RejectExplorerMode", General.Editing.Mode.GetType().Name);
    }

    [AvaloniaFact]
    public void The_sector_under_the_mouse_is_highlighted_and_the_others_colored_by_line_of_sight()
    {
        OpenEditor(wadPath: WadWithReject());
        Assert.Equal(4, General.Map.Map.Sectors.Count);
        General.Actions.InvokeAction("rejectexplorer_rejectexplorermode");
        Flush();
        Assert.True(General.Editing.Mode.GetType().Name == "RejectExplorerMode", "the mode engaged; mode is " + General.Editing.Mode.GetType().Name);

        var colors = BuilderPlug.Me.ColorSettings;
        Assert.All(Colors(), c => Assert.Equal(colors.Default, c));

        var sectors = General.Map.Map.Sectors.ToList();
        window.MouseMove(DisplayPointOf(InsideOf(sectors[0])));
        Flush();
        Assert.Same(sectors[0], General.Editing.Mode.HighlightedObject);
        Assert.Equal(new[] { colors.Highlight, colors.UnidirectionalTo, colors.UnidirectionalFrom, colors.Bidirectional }, Colors());

        window.MouseMove(DisplayPointOf(InsideOf(sectors[1])));
        Flush();
        Assert.Same(sectors[1], General.Editing.Mode.HighlightedObject);
        // Sector 1 sees and is seen by sectors 2 and 3; sector 0 cannot see it but is seen by it
        Assert.Equal(new[] { colors.UnidirectionalFrom, colors.Highlight, colors.Bidirectional, colors.Bidirectional }, Colors());
    }

    [AvaloniaFact]
    public void The_colors_are_configured_in_a_dialog_and_kept()
    {
        OpenEditor(wadPath: WadWithReject());
        General.Actions.InvokeAction("rejectexplorer_rejectexplorermode");
        Flush();
        Assert.Contains(BuilderPlug.Me.MenusForm.ColorConfiguration.Tag, new object[] { "rejectexplorercolorconfiguration", "rejectexplorer_rejectexplorercolorconfiguration" });

        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            Assert.Equal("Color Configuration", d.Title);
            var fields = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(d).OfType<ColorField>().ToList();
            Assert.Equal(5, fields.Count);
            Assert.Equal(ColorField.HexOf(PixelColor.FromInt(BuilderPlug.Me.ColorSettings.Highlight)), fields[1].HexBox.Text);

            fields[0].HexBox.Text = "#102030";
            Assert.Equal(new PixelColor(255, 0x10, 0x20, 0x30).ToInt(), fields[0].Color.ToInt());
            fields[0].HexBox.Text = "#12";                    // half typed: ignored
            Assert.Equal(new PixelColor(255, 0x10, 0x20, 0x30).ToInt(), fields[0].Color.ToInt());

            Click(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(d).OfType<Avalonia.Controls.Button>().First(b => b.Content as string == "Reset colors"));
            Assert.Equal(BuilderPlug.Me.DefaultColorSettings.Default, fields[0].Color.ToInt());
            fields[0].HexBox.Text = "#102030";
            Click(d.OkButton);
        });
        General.Actions.InvokeAction("rejectexplorer_rejectexplorercolorconfiguration");
        Flush();
        Assert.True(shown);
        Assert.Equal(new PixelColor(255, 0x10, 0x20, 0x30).ToInt(), BuilderPlug.Me.ColorSettings.Default);
    }
}
