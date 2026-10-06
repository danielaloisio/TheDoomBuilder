using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Plugins.NodesViewer;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The nodes viewer plugin: a volatile mode and a window to walk the BSP tree of the map.</summary>
public class NodesViewerTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // The nodes of the sample map as the plugin reads them: one split in the middle of the map, two subsectors with half of the lines each.
    // (Not the real BSP of the map: the viewer only needs consistent lumps.)
    private string WadWithNodes()
    {
        byte[] vertexes = SampleLump("VERTEXES");
        byte[] linedefs = SampleLump("LINEDEFS");
        int numlines = linedefs.Length / 14;
        short Vx(int v) => BitConverter.ToInt16(vertexes, v * 4);
        short Vy(int v) => BitConverter.ToInt16(vertexes, v * 4 + 2);

        var segs = new MemoryStream();
        var w = new BinaryWriter(segs);
        for (int i = 0; i < numlines; i++)
        {
            ushort v1 = BitConverter.ToUInt16(linedefs, i * 14), v2 = BitConverter.ToUInt16(linedefs, i * 14 + 2);
            double angle = Math.Atan2(Vy(v2) - Vy(v1), Vx(v2) - Vx(v1));
            w.Write(v1); w.Write(v2);
            w.Write((short)(angle / (2 * Math.PI) * 65536));
            w.Write((ushort)i); w.Write((short)0); w.Write((short)0);
        }

        int half = numlines / 2;
        var ssectors = new MemoryStream();
        w = new BinaryWriter(ssectors);
        w.Write((ushort)half); w.Write((ushort)0);
        w.Write((ushort)(numlines - half)); w.Write((ushort)half);

        int minx = Enumerable.Range(0, vertexes.Length / 4).Min(i => (int)Vx(i)), maxx = Enumerable.Range(0, vertexes.Length / 4).Max(i => (int)Vx(i));
        int miny = Enumerable.Range(0, vertexes.Length / 4).Min(i => (int)Vy(i)), maxy = Enumerable.Range(0, vertexes.Length / 4).Max(i => (int)Vy(i));
        int midx = (minx + maxx) / 2;
        var nodes = new MemoryStream();
        w = new BinaryWriter(nodes);
        w.Write((short)midx); w.Write((short)miny); w.Write((short)0); w.Write((short)(maxy - miny));     // the partition line
        w.Write((short)maxy); w.Write((short)miny); w.Write((short)midx); w.Write((short)maxx);          // right box (top, bottom, left, right)
        w.Write((short)maxy); w.Write((short)miny); w.Write((short)minx); w.Write((short)midx);          // left box
        w.Write((ushort)0x8000);                                                                          // right child: subsector 0
        w.Write((ushort)0x8001);                                                                          // left child: subsector 1

        return SampleWadWithLumps("nodes", ("SEGS", segs.ToArray()), ("SSECTORS", ssectors.ToArray()), ("NODES", nodes.ToArray()));
    }

    private static NodesViewerMode Mode => (NodesViewerMode)General.Editing.Mode;

    private void Engage()
    {
        General.Actions.InvokeAction("nodesviewer_nodesviewermode");
        Flush();
        Assert.True(General.Editing.Mode.GetType().Name == "NodesViewerMode", "the mode engaged; mode is " + General.Editing.Mode.GetType().Name);
    }

    [AvaloniaFact]
    public void The_window_shows_the_numbers_of_the_tree()
    {
        OpenEditor(wadPath: WadWithNodes());
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "NodesViewerMode");
        Engage();
        var form = Mode.Form;
        Assert.NotNull(form);
        Assert.True(form.Window.IsVisible);
        Assert.Equal("Nodes Viewer (Classic nodes format)", form.Text);
        Assert.Equal(0, form.SelectedTab);

        Assert.Equal((SampleLump("LINEDEFS").Length / 14).ToString(), form.Segs);
        Assert.Equal("1", form.Splits);
        Assert.Equal("2", form.Subsectors);
        Assert.Equal(General.Map.Map.Vertices.Count.ToString(), form.Vertices);
        Assert.Equal("1", form.Depth);
        Assert.Equal("100%", form.Balance);
    }

    [AvaloniaFact]
    public void The_splits_and_subsectors_can_be_walked()
    {
        OpenEditor(wadPath: WadWithNodes());
        Engage();
        var form = Mode.Form;

        // The root split has no parent and two subsectors as children
        form.Tabs.SelectedIndex = 1;
        Assert.Equal(0, form.ViewSplitIndex);
        Assert.Equal("(root split)", form.ParentSplitText);
        Assert.False(form.ParentButton.IsEnabled);
        Assert.Equal("Subsector:", form.LeftType);
        Assert.Equal("Subsector:", form.RightType);
        Assert.Equal("1", form.LeftIndex);
        Assert.Equal("0", form.RightIndex);
        Assert.StartsWith("(", form.LeftArea);

        // Go to the left subsector: its tab, with the segs of the second half
        Click(form.LeftButton);
        Assert.Equal(2, form.SelectedTab);
        Assert.Equal(1, form.ViewSubsectorIndex);
        int lines = SampleLump("LINEDEFS").Length / 14;
        Assert.Equal((lines - lines / 2).ToString(), form.SubsectorSegs);
        Assert.Equal(lines / 2, form.ViewSegIndex);
        Assert.Equal((lines / 2).ToString(), form.LineIndex);
        Assert.Equal("Front", form.SegSide);
        Assert.NotEqual("None", form.SideIndex);

        // The next seg
        form.SegIndex.Value = lines / 2 + 1;
        Assert.Equal((lines / 2 + 1).ToString(), form.LineIndex);
        form.ViewSegBox.IsChecked = false;
        Assert.Equal(-1, form.ViewSegIndex);

        // Back to the split that holds the subsector
        Click(form.SubsectorParentButton);
        Assert.Equal(1, form.SelectedTab);
        Assert.Equal(0, form.ViewSplitIndex);

        // The right button goes to subsector 0
        Click(form.RightButton);
        Assert.Equal(0, form.ViewSubsectorIndex);
        Assert.Equal(lines / 2, int.Parse(form.SubsectorSegs));
    }

    [AvaloniaFact]
    public void Closing_the_window_leaves_the_mode_and_leaving_the_mode_closes_the_window()
    {
        OpenEditor(wadPath: WadWithNodes());
        Engage();
        var window = Mode.Form.Window;
        window.Close();
        Flush();
        Assert.NotEqual("NodesViewerMode", General.Editing.Mode.GetType().Name);

        Engage();
        window = Mode.Form.Window;
        General.Editing.CancelMode();
        Flush();
        Assert.False(window.IsVisible);
        Assert.NotEqual("NodesViewerMode", General.Editing.Mode.GetType().Name);
    }
}
