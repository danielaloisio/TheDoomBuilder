using System;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

[Collection("General static state")]
public class ConfigModelTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-config-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public ConfigModelTests()
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

    [Theory]
    [InlineData(@"C:\Games\GZDoom\gzdoom.exe", "GZDoom")]
    [InlineData("/opt/games/zandronum/zandronum", "zandronum")]
    [InlineData("gzdoom.exe", "gzdoom")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void The_engine_is_named_after_its_folder_or_else_its_file(string path, string expected)
        => Assert.Equal(expected, ConfigModel.EngineNameOf(path));

    [Fact]
    public void The_dialog_edits_copies_and_only_Apply_reaches_the_real_configurations()
    {
        var model = new ConfigModel();
        Assert.Equal(General.Configs.Count, model.Entries.Count);
        var entry = model.Entries.First(e => e.Enabled);
        int index = model.Entries.ToList().IndexOf(entry);

        model.Select(entry);
        model.SetTestProgram("/usr/bin/gzdoom");
        entry.Enabled = false;

        Assert.True(General.Configs[index].Enabled);                          // untouched so far
        Assert.NotEqual("/usr/bin/gzdoom", General.Configs[index].TestProgram);

        model.Apply();

        Assert.False(General.Configs[index].Enabled);
        Assert.Equal("/usr/bin/gzdoom", General.Configs[index].TestProgram);
        Assert.Equal("bin", General.Configs[index].TestProgramName);
        General.Configs[index].Enabled = true;
    }

    [Fact]
    public void Short_paths_and_Linux_paths_exclude_each_other()
    {
        var model = new ConfigModel();
        model.Select(model.Entries[0]);

        model.SetShortPaths(true);
        Assert.True(model.ShortPaths);
        model.SetLinuxPaths(true);
        Assert.True(model.LinuxPaths);
        Assert.False(model.ShortPaths);
        model.SetShortPaths(true);
        Assert.False(model.LinuxPaths);
    }

    [Fact]
    public void Changing_resources_asks_the_open_map_to_reload_them_and_missing_ones_block_the_dialog()
    {
        var model = new ConfigModel();
        var entry = model.Entries.First(e => e.Enabled);
        model.Select(entry);
        Assert.False(model.ReloadResources);
        Assert.Null(model.FindInvalidResources());

        var missing = new CodeImp.DoomBuilder.Data.DataLocation(CodeImp.DoomBuilder.Data.DataLocation.RESOURCE_WAD,
            Path.Combine(dir, "does-not-exist.wad"), false, false, false, new System.Collections.Generic.List<string>());
        model.SetResources(model.Resources.Concat(new[] { missing }).ToList());

        Assert.True(model.ReloadResources);
        Assert.Same(entry, model.FindInvalidResources());
    }

    [Fact]
    public void Modes_the_map_format_cannot_use_are_off_and_the_start_mode_is_always_an_enabled_one()
    {
        var model = new ConfigModel();
        model.Select(model.Entries.First(e => e.Enabled));

        Assert.All(model.Modes.Where(m => !m.Supported), m => Assert.False(m.Enabled));

        // The headless editor has no optional modes (they come from plugins), so the start mode can only be checked when there are some
        if (model.Modes.Count(m => m.Enabled && m.Info.Attributes.SafeStartMode) == 0) return;

        Assert.NotNull(model.StartMode);
        Assert.True(model.StartMode.Enabled);
        var start = model.StartMode;
        model.SetModeEnabled(start, false);
        Assert.False(start.Enabled);
        if (model.StartModes.Count > 0) Assert.NotSame(start, model.StartMode);
    }
}
