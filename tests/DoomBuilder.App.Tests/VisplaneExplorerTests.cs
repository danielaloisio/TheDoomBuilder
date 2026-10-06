using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Plugins.VisplaneExplorer;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The visplane explorer plugin: a volatile mode that colors the map by what the renderer of Doom needs to show each spot.</summary>
public class VisplaneExplorerTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    // Three rooms in a row; the middle one is a closed door (as high as its floor)
    private static RowMap Rooms() => new RowMap(new RowMap.Room(), new RowMap.Room { Width = 32, Ceiling = 0 }, new RowMap.Room { Width = 128 })
    {
        BorderSpecials = { [1] = 1, [2] = 1 },
    };

    // A nodebuilder that "builds" by handing back the WAD with the nodes of the map (the explorer needs the real BSP to render)
    private void InstallNodebuilder(RowMap map)
    {
        Directory.CreateDirectory(dir);
        string built = map.Write(Path.Combine(dir, "built.wad"));
        string nodedir = Path.Combine(dir, "app", "Compilers", "Nodebuilders");
        Directory.CreateDirectory(nodedir);
        File.WriteAllText(Path.Combine(nodedir, "fakebsp.cfg"),
            "compilers { fakebsp { interface = \"NodesCompiler\"; program = \"fakebsp.sh\"; } }\n" +
            "nodebuilders { fake_normal { title = \"Fake\"; compiler = \"fakebsp\"; parameters = \"-o%FO %FI\"; } }\n");
        string bsp = Path.Combine(nodedir, "fakebsp.sh");
        File.WriteAllText(bsp, "#!/bin/sh\nfor a in \"$@\"; do case \"$a\" in -o*) out=\"${a#-o}\";; esac; done\ncp '" + built + "' \"$out\"\n");
        File.SetUnixFileMode(bsp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private void OpenRooms()
    {
        Directory.CreateDirectory(dir);
        var map = Rooms().Build();
        string wad = map.Write(Path.Combine(dir, "rooms.wad"));
        InstallNodebuilder(Rooms().Build());
        OpenEditor(wadPath: wad);
        General.Map.ConfigSettings.NodebuilderTest = "fake_normal";
        General.Map.ConfigSettings.NodebuilderSave = "fake_normal";
    }

    private static InterfaceForm Interface => BuilderPlug.InterfaceForm;

    private static void Wait(Func<bool> done, int ms = 20000)
    {
        for (int i = 0; i < ms / 20 && !done(); i++) { Flush(); System.Threading.Thread.Sleep(20); }
    }

    [AvaloniaFact]
    public void The_mode_is_registered_and_engages_on_a_map_with_nodes()
    {
        if (OperatingSystem.IsWindows()) return;                              // the nodebuilder stand-in is a shell script
        OpenRooms();
        Assert.Contains(General.Editing.ModesInfo, m => m.Type.Name == "VisplaneExplorerMode");
        Assert.NotNull(General.Actions.GetActionByName("visplaneexplorer_visplaneexplorermode"));

        General.Map.IsChanged = true;                                         // (so the nodes are built)
        General.Actions.InvokeAction("visplaneexplorer_visplaneexplorermode");
        Flush();
        Assert.True(General.Editing.Mode.GetType().Name == "VisplaneExplorerMode", "the mode engaged; mode is " + General.Editing.Mode.GetType().Name);
        Assert.NotNull(Interface);
        Assert.NotNull(BuilderPlug.VPO);

        General.Editing.CancelMode();
        Flush();
        Assert.NotEqual("VisplaneExplorerMode", General.Editing.Mode.GetType().Name);
    }

    private Point DisplayPointOf(double x, double y)
    {
        Vector2D d = Renderer.MapToDisplay(new Vector2D(x, y));
        return InView(d.x, d.y);
    }

    private void Engage()
    {
        General.Map.IsChanged = true;                                         // (so the nodes are built)
        General.Actions.InvokeAction("visplaneexplorer_visplaneexplorermode");
        Flush();
        Assert.True(General.Editing.Mode.GetType().Name == "VisplaneExplorerMode", "the mode engaged; mode is " + General.Editing.Mode.GetType().Name);
    }

    // What the explorer says about a spot ("value / limit") once the threads have worked it out; null when it does not (the void)
    private string Hover(double x, double y, int ms = 20000, bool allowzero = false)
    {
        var texts = new List<string>();
        Action<string, string, int, int> handler = (title, text, px, py) => texts.Add(text);
        General.Interface.Display.ToolTipRequested += handler;
        try
        {
            for (int i = 0; i < ms / 50; i++)
            {
                General.Editing.Mode.OnProcess(0);              // (the shell calls this from its timer)
                Flush();
                window.MouseMove(DisplayPointOf(x + 20, y + 20));
                window.MouseMove(DisplayPointOf(x, y));
                Flush();
                // (a spot that was not worked out yet says 0)
                var worked = texts.Where(t => allowzero || !t.StartsWith("0 ")).ToList();
                if (worked.Count > 0) return worked.Last();
                System.Threading.Thread.Sleep(50);
            }
            return null;
        }
        finally { General.Interface.Display.ToolTipRequested -= handler; }
    }

    [AvaloniaFact]
    public void The_explorer_tells_what_each_spot_needs()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenRooms();
        Engage();

        // Room A: a floor and a ceiling (visplanes), against the limit of the game configuration
        string text = Hover(64, 64);
        Assert.NotNull(text);
        Assert.Equal("2 / " + General.Map.Config.StaticLimits.Visplanes, text);

        // Room B too
        Assert.Equal("2 / " + General.Map.Config.StaticLimits.Visplanes, Hover(224, 64));

        // The statistics to show: the drawsegs of room A are the walls that can be seen from it
        var stats = Interface.StatsButton.DropDownItems.Cast<System.Windows.Forms.ToolStripMenuItem>().ToList();
        Assert.Equal(new[] { "Visplanes", "Drawsegs", "Solidsegs", "Openings" }, stats.Select(i => i.Text));
        stats[1].PerformClick();
        Assert.Equal(ViewStats.Drawsegs, Interface.ViewStats);
        Assert.True(stats[1].Checked);
        Assert.False(stats[0].Checked);
        string drawsegs = Hover(64, 64);
        Assert.EndsWith(" / " + General.Map.Config.StaticLimits.Drawsegs, drawsegs);
        Assert.InRange(int.Parse(drawsegs.Split(' ')[0]), 1, 20);
    }

    [AvaloniaFact]
    public void Open_doors_let_the_explorer_see_the_door_sectors_and_what_is_behind_them()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenRooms();
        Engage();

        // A closed door has no room to stand in: the explorer shows the lowest value for it
        string closed = Hover(144, 64);
        Assert.Equal("1 / " + General.Map.Config.StaticLimits.Visplanes, closed);

        // Opened, the sector is a place like any other, and its ceiling (lower than the others) is a plane of its own
        Interface.OpenDoorsBox.Checked = true;                                   // restarts the work
        string opened = Hover(144, 64);
        Assert.NotNull(opened);
        Assert.True(int.Parse(opened.Split(' ')[0]) >= 2, "an open door needs its own planes: " + opened);
    }

    [AvaloniaFact]
    public void The_view_height_is_chosen_in_a_menu_and_a_custom_one_in_a_dialog()
    {
        if (OperatingSystem.IsWindows()) return;
        OpenRooms();
        Engage();

        int defaultheight = Interface.ViewHeightDefault;
        Assert.Equal(defaultheight, Interface.ViewHeight);
        Assert.Equal("View Height (" + defaultheight + ")", Interface.HeightButton.Text);
        var items = Interface.HeightButton.DropDownItems.Cast<System.Windows.Forms.ToolStripMenuItem>().ToList();
        Assert.Contains(items, i => ((string)i.Tag) == defaultheight.ToString() && i.Text.EndsWith("(default)") && i.Checked);
        Assert.False(Interface.CustomHeightItem.Visible);

        // A custom height
        bool shown = false;
        WhenShown<SimpleDialog>(d =>
        {
            shown = true;
            Assert.Equal("Visplane Explorer - Set custom height", d.Title);
            Interface.CustomHeightDialog.Input.Text = "56";
            Click(d.OkButton);
        });
        Interface.CustomHeightAdd.PerformClick();
        Flush();
        Assert.True(shown);
        Assert.Equal(56, Interface.ViewHeight);
        Assert.True(Interface.CustomHeightItem.Visible);
        Assert.Equal("56 - Custom", Interface.CustomHeightItem.Text);
        Assert.Equal("View Height (56)", Interface.HeightButton.Text);
        Assert.True(Interface.CustomHeightItem.Checked);

        // A height out of range goes back to the default
        WhenShown<SimpleDialog>(d => { Interface.CustomHeightDialog.Input.Text = "99999"; Click(d.OkButton); });
        Interface.CustomHeightAdd.PerformClick();
        Flush();
        Assert.Equal(defaultheight, Interface.ViewHeight);
        Assert.False(Interface.CustomHeightItem.Visible);
    }
}
