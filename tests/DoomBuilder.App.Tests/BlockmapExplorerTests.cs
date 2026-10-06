using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.BlockmapExplorer;
using CodeImp.DoomBuilder.Geometry;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The blockmap explorer plugin: a volatile mode with a docker that describes the BLOCKMAP of the map.</summary>
public class BlockmapExplorerTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // A BLOCKMAP of 3 x 2 blocks starting at (-64,-64): header, offset table (row by row), then the block lists (all in 16 bit words).
    // Word 10 is the empty list that blocks without lines share; the list at 12 is shared by two blocks.
    private static readonly short[] Blockmap =
    {
        -64, -64, 3, 2,
        12, 17, 10, 21, 12, 10,
        0, -1,
        0, 0, 1, 2, -1,
        0, 0, 1, -1,
        0, 2, 3, -1,
    };

    private string WadWithBlockmap()
    {
        byte[] data = new byte[Blockmap.Length * 2];
        Buffer.BlockCopy(Blockmap, 0, data, 0, data.Length);
        return SampleWadWithLump("BLOCKMAP", data);
    }

    private Point DisplayPointOf(Vector2D mappos)
    {
        Vector2D d = Renderer.MapToDisplay(mappos);
        return InView(d.x, d.y);
    }

    private static BlockmapExplorerMode Mode => (BlockmapExplorerMode)General.Editing.Mode;

    private void Engage()
    {
        General.Actions.InvokeAction("blockmapexplorer_blockmapexplorermode");
        Flush();
        Assert.True(General.Editing.Mode.GetType().Name == "BlockmapExplorerMode", "the mode engaged; mode is " + General.Editing.Mode.GetType().Name);
    }

    [AvaloniaFact]
    public void The_mode_is_registered_and_refuses_a_map_without_blockmap()
    {
        OpenEditor();                                           // the sample map has no BLOCKMAP lump
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "BlockmapExplorerMode");
        Assert.NotNull(General.Actions.GetActionByName("blockmapexplorer_blockmapexplorermode"));

        General.Actions.InvokeAction("blockmapexplorer_blockmapexplorermode");
        Flush();
        Assert.NotEqual("BlockmapExplorerMode", General.Editing.Mode.GetType().Name);
    }

    [AvaloniaFact]
    public void The_docker_describes_the_blockmap_and_the_block_under_the_mouse()
    {
        OpenEditor(wadPath: WadWithBlockmap());
        Engage();
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        Assert.Equal("Blockmap Explorer", General.Interface.ActiveDockerTabName);
        Assert.Contains(shell.Dockers.Dockers, d => d.Title == "Blockmap Explorer");

        var panel = (CodeImp.DoomBuilder.BlockmapExplorer.Controls.BlockmapExplorerDocker)shell.Dockers.Dockers.First(d => d.Title == "Blockmap Explorer").Control;
        Assert.Equal("3", panel.TotalColumns);
        Assert.Equal("2", panel.TotalRows);
        Assert.Equal("6", panel.TotalBlocks);
        Assert.Equal("4", panel.UniqueBlocks);                   // the lists at 10, 12, 17 and 21
        Assert.Equal((Blockmap.Length * 2).ToString(), panel.LumpSize);
        Assert.Equal((8 + 6 * 2).ToString(), panel.OffsetListEnd);
        Assert.False(panel.QuestionableOffsets.IsVisible);

        Mode.CenterOnCoordinates(new Vector2D(128, 32), 1.0f);           // the whole blockmap is in view
        Flush();

        // The block in column 1, row 0: starts at (64,-64)
        window.MouseMove(DisplayPointOf(new Vector2D(-64 + 128 + 10, -64 + 10)));
        Flush();
        Assert.Equal("1", panel.Column);
        Assert.Equal("0", panel.Row);
        Assert.Equal("34", panel.Offset);                       // in bytes: word 17
        Assert.Equal("2", panel.LinesInBlock);                    // lines 0 and 1 (the lists are read as sets: 0, 0, 1)
        Assert.Equal("No", panel.IsSublist);

        // The empty list shared by the last column
        window.MouseMove(DisplayPointOf(new Vector2D(-64 + 256 + 10, -64 + 128 + 10)));
        Flush();
        Assert.Equal("2", panel.Column);
        Assert.Equal("1", panel.Row);
        Assert.Equal("20", panel.Offset);
        Assert.Equal("1", panel.LinesInBlock);                    // the 0 that starts every list is read as line 0, like in UDB

        // Leaving the mode takes the docker away
        General.Editing.CancelMode();
        Flush();
        Assert.DoesNotContain(shell.Dockers.Dockers, d => d.Title == "Blockmap Explorer");
        Assert.NotEqual("BlockmapExplorerMode", General.Editing.Mode.GetType().Name);
    }
}
