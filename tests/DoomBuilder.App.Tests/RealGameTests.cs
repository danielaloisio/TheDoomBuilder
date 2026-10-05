using System;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Data;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>
/// Tests against a real game: they need files the repository does not carry, so they run only when the environment says where
/// they are (UDB_TEST_IWAD = a Doom IWAD, UDB_TEST_GZDOOM = a GZDoom executable) and do nothing otherwise.
/// </summary>
public class RealGameTests : EditorTestBase
{
    private static string Iwad => Environment.GetEnvironmentVariable("UDB_TEST_IWAD") is string p && File.Exists(p) ? p : null;
    private static string Engine => Environment.GetEnvironmentVariable("UDB_TEST_GZDOOM") is string p && File.Exists(p) ? p : null;

    private static void Pump(int ms)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(10); }
    }

    [AvaloniaFact]
    public void With_an_IWAD_the_palette_textures_and_sprites_load_and_nothing_is_logged_as_an_error()
    {
        if (Iwad == null) return;
        OpenEditor(iwad: Iwad);

        Assert.Empty(General.ErrorLogger.GetErrors(0).Where(e => e.Type == ErrorType.Error).Select(e => e.Description));
        ImageData wall = General.Map.Data.GetTextureImage("STARTAN1");
        Assert.IsNotType<UnknownImage>(wall);
        for (int i = 0; i < 200 && !wall.IsImageLoaded; i++) Pump(20);
        Assert.True(wall.IsImageLoaded);
        Assert.True(wall.Width > 0 && wall.Height > 0);
        Assert.IsNotType<UnknownImage>(General.Map.Data.GetFlatImage("FLOOR4_8"));
        Assert.IsNotType<UnknownImage>(General.Map.Data.GetSpriteImage("TROOA2A8"));
    }

    [AvaloniaFact]
    public void The_3D_mode_shows_the_real_textures_in_the_info_panel()
    {
        if (Iwad == null) return;
        OpenEditor(iwad: Iwad);
        var shell = (DoomBuilder.App.AvaloniaShell)General.Interface;
        General.Interface.ShowSectorInfo(General.Map.Map.Sectors.First());

        var info = (CodeImp.DoomBuilder.Windows.ElementInfo)typeof(CodeImp.DoomBuilder.Windows.ElementInfoBuilder)
            .GetMethod("ForSector").Invoke(null, new object[] { General.Map.Map.Sectors.First(), false, false });
        var ceiling = info.Groups.Single(g => g.Title == "Ceiling").Textures[0];
        Assert.IsNotType<UnknownImage>(ceiling.Image);
        for (int i = 0; i < 200 && !info.IsComplete(); i++) Pump(20);
        Assert.True(info.IsComplete());
        Assert.NotEqual("", ceiling.SizeText);                               // "64x64": the size is known once the image is loaded
    }

    [AvaloniaFact]
    public void Test_map_starts_GZDoom_with_the_IWAD_and_the_saved_map()
    {
        if (Iwad == null || Engine == null || OperatingSystem.IsWindows()) return;

        // GZDoom is wrapped so that it logs to a file and stops by itself
        Directory.CreateDirectory(dir);
        string log = Path.Combine(dir, "gzdoom.log"), args = Path.Combine(dir, "args.txt");
        string wrapper = Path.Combine(dir, "engine.sh");
        File.WriteAllText(wrapper, "#!/bin/sh\necho \"$@\" > '" + args + "'\ntimeout 10 '" + Engine + "' -stdout -nosound -noautoload \"$@\" > '" + log + "' 2>&1\n");
        File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // A Doom 1 IWAD has E1M1, not MAP01: the sample map is opened under that name
        string e1m1 = Path.Combine(dir, "e1m1.wad");
        var bytes = File.ReadAllBytes(FindRepoFile("assets", "samples", "sample.wad"));
        int dirpos = BitConverter.ToInt32(bytes, 8), count = BitConverter.ToInt32(bytes, 4);
        for (int i = 0; i < count; i++)
        {
            int e = dirpos + 16 * i + 8;
            if (System.Text.Encoding.ASCII.GetString(bytes, e, 5) == "MAP01")
                System.Text.Encoding.ASCII.GetBytes("E1M1\0\0\0\0").CopyTo(bytes, e);
        }
        File.WriteAllBytes(e1m1, bytes);

        OpenEditor(wadPath: e1m1, mapName: "E1M1", iwad: Iwad);
        General.Map.ConfigSettings.TestProgram = wrapper;
        General.Map.ConfigSettings.CustomParameters = true;
        General.Map.ConfigSettings.TestParameters = "-iwad \"%WP\" -skill \"%S\" -file \"%AP\" \"%F\" -warp %L1 %L2";

        General.Actions.InvokeAction("builder_testmap");
        for (int i = 0; i < 600 && !(File.Exists(log) && File.ReadAllText(log).Contains("E1M1")); i++) Pump(50);

        string started = File.ReadAllText(args);
        Assert.Contains("-iwad " + Iwad, started);
        Assert.Contains("-warp 1 1", started);
        Assert.Contains(".wad", started);
        string output = File.ReadAllText(log);
        Assert.Contains("E1M1", output);                                     // the engine got as far as playing the level
        Assert.DoesNotContain("Script error", output);
    }
}
