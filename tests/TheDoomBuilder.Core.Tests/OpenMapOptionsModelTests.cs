using System;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

[Collection("General static state")]
public class OpenMapOptionsModelTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-omom-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public OpenMapOptionsModelTests()
    {
        Directory.CreateDirectory(dir);
        Assert.True(General.Startup(new[] { "-nosettings" }, () => new HeadlessMainWindow(), appdir, dir));
    }

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        if (Directory.Exists(appdir)) Directory.Delete(appdir, true);
    }

    [Fact]
    public void A_missing_file_is_reported()
    {
        using var model = new OpenMapOptionsModel(Path.Combine(dir, "nope.wad"), null);
        Assert.Contains("does not exist", model.LoadError);
    }

    [Fact]
    public void A_doom_format_wad_selects_a_doom_configuration_and_lists_its_map()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);

        using var model = new OpenMapOptionsModel(wad, null);

        Assert.Null(model.LoadError);
        Assert.False(model.NothingToOpen);
        Assert.NotNull(model.SelectedConfig);
        Assert.Equal(new[] { "MAP01" }, model.Maps);
        Assert.Equal("MAP01", model.SelectedMap);
    }

    [Fact]
    public void Switching_to_a_configuration_with_another_format_changes_the_map_list()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);           // a Doom-format map has no BEHAVIOR lump
        using var model = new OpenMapOptionsModel(wad, null);

        model.SelectedConfig = model.Configs.First(c => c.Filename == "GZDoom_DoomHexen.cfg");   // Hexen format needs BEHAVIOR

        Assert.Empty(model.Maps);
        Assert.Null(model.SelectedMap);
    }

    [Fact]
    public void A_wad_without_maps_has_nothing_to_open()
    {
        string path = Path.Combine(dir, "empty.wad");
        using (var wad = new WAD(path)) { wad.Insert("PLAYPAL", 0, 0); wad.WriteHeaders(); }

        using var model = new OpenMapOptionsModel(path, null);

        Assert.True(model.NothingToOpen);
    }

    [Fact]
    public void Options_are_built_from_the_choices()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);
        using var model = new OpenMapOptionsModel(wad, null);
        model.StrictPatches = true;
        model.Resources.Add(new CodeImp.DoomBuilder.Data.DataLocation(CodeImp.DoomBuilder.Data.DataLocation.RESOURCE_DIRECTORY, dir, false, false, false, []));

        Assert.Null(model.Validate(out string confirmation));
        var options = model.BuildOptions();

        Assert.Equal("MAP01", options.CurrentName);
        Assert.Equal(model.SelectedConfig.Filename, options.ConfigFile);
        Assert.True(options.StrictPatches);
        Assert.Single(options.GetResources(), r => r.location == dir);
    }

    [Fact]
    public void A_map_without_any_resources_asks_for_confirmation()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);
        using var model = new OpenMapOptionsModel(wad, null);

        // configurations without an IWAD configured and no extra resources: textures will not show
        Assert.Null(model.Validate(out string confirmation));
        Assert.Contains("without selecting any resources", confirmation);
    }

    [Fact]
    public void A_missing_resource_is_refused()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);
        using var model = new OpenMapOptionsModel(wad, null);
        model.Resources.Add(new CodeImp.DoomBuilder.Data.DataLocation(CodeImp.DoomBuilder.Data.DataLocation.RESOURCE_WAD, Path.Combine(dir, "gone.wad"), false, false, false, []));

        Assert.Contains("resource doesn't exist", model.Validate(out _));
    }

    [Fact]
    public void Starting_from_existing_options_keeps_their_configuration_and_resources()
    {
        string wad = MapFiles.WriteSquareRoomWad(dir);
        var initial = new CodeImp.DoomBuilder.Map.MapOptions(new Configuration(true), "MAP01", false) { ConfigFile = "Doom_DoomDoom.cfg" };
        initial.CopyResources(new CodeImp.DoomBuilder.Data.DataLocationList {
            new CodeImp.DoomBuilder.Data.DataLocation(CodeImp.DoomBuilder.Data.DataLocation.RESOURCE_DIRECTORY, dir, false, false, false, []) });

        using var model = new OpenMapOptionsModel(wad, initial);

        Assert.Equal("Doom_DoomDoom.cfg", model.SelectedConfig.Filename);
        Assert.Single(model.Resources);
    }
}
