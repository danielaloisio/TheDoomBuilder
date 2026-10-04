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

    [Fact]
    public void Opens_a_map_from_the_command_line_through_the_whole_core_stack()
    {
        string wadpath = MapFiles.WriteSquareRoomWad(dir);
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
        string wadpath = MapFiles.WriteSquareRoomWad(dir);
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
