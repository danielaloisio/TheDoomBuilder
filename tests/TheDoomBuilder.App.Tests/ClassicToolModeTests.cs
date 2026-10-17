using System;
using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The classic modes that came with Phase 7 (bridge, block map, flat alignment) in the real window.</summary>
public class ClassicToolModeTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // Two rooms with a gap between them: the right wall of the first and the left wall of the second face each other
    private const string TwoRooms = @"namespace = ""zdoom"";
thing { x = 64.0; y = 64.0; type = 1; angle = 0; skill1 = true; skill2 = true; single = true; }
vertex { x = 0.0; y = 0.0; }
vertex { x = 128.0; y = 0.0; }
vertex { x = 128.0; y = 128.0; }
vertex { x = 0.0; y = 128.0; }
vertex { x = 256.0; y = 0.0; }
vertex { x = 384.0; y = 0.0; }
vertex { x = 384.0; y = 128.0; }
vertex { x = 256.0; y = 128.0; }
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
sector { heightfloor = 0; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
sector { heightfloor = 0; heightceiling = 128; texturefloor = ""FLOOR4_8""; textureceiling = ""CEIL3_5""; lightlevel = 192; }
";

    [AvaloniaFact]
    public void The_new_modes_are_registered()
    {
        OpenEditor(wadPath: WriteUdmfWad(TwoRooms), config: "GZDoom_DoomUDMF.cfg");
        var names = General.Editing.ModesInfo.Select(m => m.Type.Name).ToList();
        foreach (var name in new[] { "BridgeMode", "FloorAlignMode", "CeilingAlignMode" })
            Assert.Contains(name, names);
    }

    [AvaloniaFact]
    public void The_bridge_mode_joins_two_walls_with_new_sectors_and_the_options_window_follows_it()
    {
        OpenEditor(wadPath: WriteUdmfWad(TwoRooms), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("LinedefsMode");
        Flush();
        var lines = General.Map.Map.Linedefs.ToList();
        foreach (var l in new[] { lines[1], lines[7] }) l.Selected = true;       // the right wall of the first room, the left wall of the second
        int sectors = General.Map.Map.Sectors.Count;

        General.Actions.InvokeAction("buildermodes_bridgemode");
        Flush();
        Assert.Equal("BridgeMode", General.Editing.Mode.GetType().Name);
        var options = window.OwnedWindows.FirstOrDefault(w => w.Title == "Options");
        Assert.NotNull(options);
        Assert.True(options.IsVisible);

        // OK builds the bridge and leaves the mode (the window goes away with it)
        var ok = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(options).OfType<Avalonia.Controls.Button>().First(b => b.Content as string == "OK");
        Click(ok);
        Flush();
        Assert.NotEqual("BridgeMode", General.Editing.Mode.GetType().Name);
        Assert.False(options.IsVisible);
        Assert.True(General.Map.Map.Sectors.Count > sectors, $"the bridge made new sectors (sectors {sectors}->{General.Map.Map.Sectors.Count}, lines 8->{General.Map.Map.Linedefs.Count}, undo: {General.Map.UndoRedo.NextUndo?.Description})");
    }

    [AvaloniaFact]
    public void Cancelling_the_bridge_changes_nothing()
    {
        OpenEditor(wadPath: WriteUdmfWad(TwoRooms), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("LinedefsMode");
        Flush();
        var lines = General.Map.Map.Linedefs.ToList();
        foreach (var l in new[] { lines[1], lines[7] }) l.Selected = true;
        int sectors = General.Map.Map.Sectors.Count, linedefs = General.Map.Map.Linedefs.Count;

        General.Actions.InvokeAction("buildermodes_bridgemode");
        Flush();
        Assert.Equal("BridgeMode", General.Editing.Mode.GetType().Name);
        var options = window.OwnedWindows.First(w => w.Title == "Options");
        var cancel = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(options).OfType<Avalonia.Controls.Button>().First(b => b.Content as string == "Cancel");
        Click(cancel);
        Flush();
        Assert.NotEqual("BridgeMode", General.Editing.Mode.GetType().Name);
        Assert.False(options.IsVisible);
        Assert.Equal(sectors, General.Map.Map.Sectors.Count);
        Assert.Equal(linedefs, General.Map.Map.Linedefs.Count);
    }

    [AvaloniaTheory]
    [InlineData("buildermodes_flooralignmode", "FloorAlignMode")]
    [InlineData("buildermodes_ceilingalignmode", "CeilingAlignMode")]
    public void The_flat_align_modes_open_on_a_selected_sector_and_cancel_goes_back(string action, string mode)
    {
        OpenEditor(wadPath: WriteUdmfWad(TwoRooms), config: "GZDoom_DoomUDMF.cfg");
        General.Editing.ChangeMode("SectorsMode");
        Flush();
        General.Map.Map.Sectors.First().Selected = true;
        var before = General.Editing.Mode.GetType().Name;

        General.Actions.InvokeAction(action);
        Flush();
        Assert.Equal(mode, General.Editing.Mode.GetType().Name);

        General.Editing.CancelMode();
        Flush();
        Assert.Equal(before, General.Editing.Mode.GetType().Name);
    }
}
