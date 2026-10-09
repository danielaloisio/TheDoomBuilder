using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>
/// A real IWAD in the real window: the bundled sample is tiny, so this is where a map with hundreds of sectors and the whole texture set shows what the
/// samples do not. It runs only when the DOOMBUILDER_IWAD environment variable names a Doom/Doom II IWAD (the game it is for goes in
/// DOOMBUILDER_IWAD_MAP: "E1M1" by default), because IWADs are not part of the repository.
/// </summary>
public class IwadSmokeTests : EditorTestBase
{
    private static void Flush() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static string Iwad()
    {
        string path = Environment.GetEnvironmentVariable("DOOMBUILDER_IWAD");
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
    }

    [AvaloniaFact]
    public void A_map_of_the_iwad_opens_and_every_mode_and_the_3d_view_run()
    {
        string iwad = Iwad();
        if (iwad == null) return;                                        // no IWAD here: nothing to check
        string map = Environment.GetEnvironmentVariable("DOOMBUILDER_IWAD_MAP") ?? "E1M1";
        string config = Environment.GetEnvironmentVariable("DOOMBUILDER_IWAD_CFG") ?? "Doom_DoomDoom.cfg";

        var watch = Stopwatch.StartNew();
        OpenEditor(wadPath: iwad, config: config, iwad: iwad, mapName: map);
        long opened = watch.ElapsedMilliseconds;

        Assert.True(General.Map.Map.Vertices.Count > 100, "vertices: " + General.Map.Map.Vertices.Count);
        Assert.True(General.Map.Map.Sectors.Count > 20, "sectors: " + General.Map.Map.Sectors.Count);
        Assert.True(General.Map.Data.Textures.Count > 100, "textures: " + General.Map.Data.Textures.Count);
        Assert.True(General.Map.Data.Flats.Count > 50, "flats: " + General.Map.Data.Flats.Count);

        foreach (string mode in new[] { "VerticesMode", "LinedefsMode", "SectorsMode", "ThingsMode" })
        {
            watch.Restart();
            General.Editing.ChangeMode(mode);
            Flush();
            Assert.Equal(mode, General.Editing.Mode.GetType().Name);
            Assert.True(watch.ElapsedMilliseconds < 5000, mode + " took " + watch.ElapsedMilliseconds + " ms");
        }

        // The 3D view from the player 1 start: a few frames must draw without throwing
        var start = General.Map.Map.Things.FirstOrDefault(t => t.Type == 1);
        Assert.NotNull(start);
        General.Editing.ChangeMode("BaseVisualMode");
        var cam = General.Map.VisualCamera;
        cam.Position = new Vector3D(start.Position.x, start.Position.y, 41);
        var input = ((DoomBuilder.App.AvaloniaShell)General.Interface).Input;
        watch.Restart();
        for (int i = 0; i < 20; i++)
        {
            input.Tick();
            Flush();
            System.Threading.Thread.Sleep(10);
        }
        Assert.True(watch.ElapsedMilliseconds < 10000, "20 frames of the 3D view took " + watch.ElapsedMilliseconds + " ms");
        Assert.Equal("BaseVisualMode", General.Editing.Mode.GetType().Name);

        // What the program logged as errors while loading the real data (the number is information: see the failure text when it grows)
        int errors = General.ErrorLogger.ErrorsCount;
        Assert.True(errors < 50, "errors logged while loading " + map + ": " + errors + " (opened in " + opened + " ms)");
    }
}
