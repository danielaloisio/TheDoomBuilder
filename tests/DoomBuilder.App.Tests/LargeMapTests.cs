using System;
using System.Diagnostics;
using System.Text;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>A map with many sectors must not make the editing modes crawl.</summary>
public class LargeMapTests : EditorTestBase
{
    // n x n separate rooms of 64 units, one sector and four one-sided lines each
    private static string Grid(int n)
    {
        var verts = new StringBuilder(); var lines = new StringBuilder(); var sides = new StringBuilder(); var sectors = new StringBuilder();
        int v = 0, sd = 0;
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
            {
                int x = gx * 80, y = gy * 80;
                verts.Append($"vertex {{ x = {x}.0; y = {y}.0; }}\nvertex {{ x = {x}.0; y = {y + 64}.0; }}\nvertex {{ x = {x + 64}.0; y = {y + 64}.0; }}\nvertex {{ x = {x + 64}.0; y = {y}.0; }}\n");
                for (int i = 0; i < 4; i++)
                {
                    lines.Append($"linedef {{ v1 = {v + i}; v2 = {v + (i + 1) % 4}; sidefront = {sd + i}; blocking = true; }}\n");
                    sides.Append($"sidedef {{ sector = {gy * n + gx}; texturemiddle = \"STARTAN1\"; }}\n");
                }
                sectors.Append("sector { heightfloor = 0; heightceiling = 128; texturefloor = \"FLOOR4_8\"; textureceiling = \"CEIL3_5\"; lightlevel = 192; }\n");
                v += 4; sd += 4;
            }
        return "namespace = \"zdoom\";\nthing { x = 32.0; y = 32.0; type = 1; angle = 0; skill1 = true; single = true; }\n" + verts + lines + sides + sectors;
    }

    [AvaloniaFact]
    public void Switching_modes_on_a_map_of_a_thousand_sectors_stays_fast()
    {
        OpenEditor(wadPath: WriteUdmfWad(Grid(32)), config: "GZDoom_DoomUDMF.cfg");
        Assert.Equal(1024, General.Map.Map.Sectors.Count);

        // The sector mode makes a text label for every sector: it used to look the font up again for each one (7 ms each)
        var watch = Stopwatch.StartNew();
        General.Editing.ChangeMode("SectorsMode");
        long sectors = watch.ElapsedMilliseconds;
        Assert.True(sectors < 2000, "entering the sectors mode took " + sectors + " ms");

        watch.Restart();
        General.Editing.ChangeMode("BaseVisualMode");
        var cam = General.Map.VisualCamera;
        cam.Position = new CodeImp.DoomBuilder.Geometry.Vector3D(1280, 1280, 60);
        var input = ((DoomBuilder.App.AvaloniaShell)General.Interface).Input;
        input.Tick();
        System.Threading.Thread.Sleep(100);
        input.Tick();
        long visual = watch.ElapsedMilliseconds;
        Assert.True(visual < 3000, "entering the 3D mode and drawing two frames took " + visual + " ms");
    }
}
