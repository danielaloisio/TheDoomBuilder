using System;
using System.IO;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>
/// Opens a map the way the editor does (Startup, auto map loading, MapManager, DataManager, editing mode) with the
/// UI-less shell and the null render backend. This is the closest thing to a full-stack test the Core has.
/// </summary>
[Collection("General static state")]
public class OpenMapTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-open-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public OpenMapTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        if (Directory.Exists(appdir)) Directory.Delete(appdir, true);
    }

    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, true)) write(w);
        return ms.ToArray();
    }

    private static void Name8(BinaryWriter w, string name)
    {
        var b = new byte[8];
        System.Text.Encoding.ASCII.GetBytes(name, 0, name.Length, b, 0);
        w.Write(b);
    }

    private string WriteSquareRoomWad()
    {
        string path = Path.Combine(dir, "room.wad");
        using var wad = new WAD(path);
        void Add(string name, byte[] data) { var l = wad.Insert(name, wad.Lumps.Count, data.Length); l.Stream.Write(data, 0, data.Length); }

        Add("MAP01", Array.Empty<byte>());
        Add("THINGS", Bytes(w => { w.Write((short)64); w.Write((short)64); w.Write((short)90); w.Write((short)1); w.Write((short)7); }));
        Add("LINEDEFS", Bytes(w =>
        {
            for (int i = 0; i < 4; i++)
            {
                w.Write((short)i); w.Write((short)((i + 1) % 4)); w.Write((short)1);
                w.Write((short)0); w.Write((short)0); w.Write((short)i); w.Write((short)-1);
            }
        }));
        Add("SIDEDEFS", Bytes(w =>
        {
            for (int i = 0; i < 4; i++)
            {
                w.Write((short)0); w.Write((short)0);
                Name8(w, "-"); Name8(w, "-"); Name8(w, "STARTAN1"); w.Write((short)0);
            }
        }));
        Add("VERTEXES", Bytes(w =>
        {
            foreach (var (x, y) in new[] { (0, 0), (128, 0), (128, 128), (0, 128) }) { w.Write((short)x); w.Write((short)y); }
        }));
        Add("SECTORS", Bytes(w =>
        {
            w.Write((short)0); w.Write((short)128);
            Name8(w, "FLOOR4_8"); Name8(w, "CEIL3_5"); w.Write((short)192); w.Write((short)0); w.Write((short)0);
        }));
        wad.WriteHeaders();
        return path;
    }

    [Fact]
    public void Opens_a_map_from_the_command_line_through_the_whole_core_stack()
    {
        string wadpath = WriteSquareRoomWad();
        string settings = Path.Combine(dir, "settings");
        Directory.CreateDirectory(settings);

        bool ok = General.Startup(new[] { wadpath, "-map", "MAP01", "-cfg", "Doom_DoomDoom.cfg", "-nosettings" },
                                  () => new HeadlessMainWindow(), appdir, settings);
        Assert.True(ok);

        // The WinForms main window did this from its Shown event, i.e. after Startup and once the window is up.
        General.MainWindow.PerformAutoMapLoading();

        Assert.NotNull(General.Map);
        Assert.Equal(4, General.Map.Map.Vertices.Count);
        Assert.Equal(4, General.Map.Map.Linedefs.Count);
        Assert.Single(General.Map.Map.Sectors);
        Assert.Single(General.Map.Map.Things);

        Assert.NotNull(General.Map.Data);
        Assert.NotNull(General.Map.Graphics);
        Assert.NotNull(General.Map.Renderer2D);
        // No editing mode yet: modes come from the BuilderModes plugin, which is ported in Phase 5.
        Assert.Equal("MAP01", General.Map.Options.CurrentName);
    }

    [Fact]
    public void Shutdown_releases_the_map_file()
    {
        string wadpath = WriteSquareRoomWad();
        string settings = Path.Combine(dir, "settings");
        Directory.CreateDirectory(settings);

        General.Startup(new[] { wadpath, "-map", "MAP01", "-cfg", "Doom_DoomDoom.cfg", "-nosettings" }, () => new HeadlessMainWindow(), appdir, settings);
        General.MainWindow.PerformAutoMapLoading();
        Assert.NotNull(General.Map);

        General.ShutdownHeadless();

        // Windows cannot delete an open file (this broke the CI cleanup); an exclusive open proves nothing holds it on any OS
        using var exclusive = new FileStream(wadpath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
