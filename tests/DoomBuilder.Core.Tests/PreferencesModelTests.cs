using System;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Windows;
using Xunit;

namespace DoomBuilder.Core.Tests;

[Collection("General static state")]
public class PreferencesModelTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "udb-prefs-" + Guid.NewGuid().ToString("N"));
    private readonly string appdir = TestAssets.CreateAppDirectory();

    public PreferencesModelTests()
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
    public void The_settings_are_grouped_in_tabs_with_unique_keys()
    {
        var model = new PreferencesModel();

        Assert.Equal(new[] { "Interface", "Display", "Recovery", "Colors" }, model.Tabs);
        Assert.Equal(model.Items.Count, model.Items.Select(i => i.Key).Distinct().Count());
        Assert.All(model.Items, i => Assert.False(i.IsChanged));
    }

    [Fact]
    public void Edits_stay_on_the_items_until_Apply_and_then_reach_the_settings()
    {
        var model = new PreferencesModel();
        bool toolbar = General.Settings.ToolbarFile;

        model.Find("toolbar_file").Value = !toolbar;
        Assert.True(model.Find("toolbar_file").IsChanged);
        Assert.Equal(toolbar, General.Settings.ToolbarFile);               // not yet

        model.Apply();
        Assert.Equal(!toolbar, General.Settings.ToolbarFile);
    }

    [Fact]
    public void Slider_values_are_converted_to_the_units_of_the_settings_and_clamped()
    {
        var model = new PreferencesModel();

        model.Find("doublesidedalpha").Value = 3;           // 3 steps of transparency
        model.Find("fieldofview").Value = 9;                // x10 degrees
        model.Find("viewdistance").Value = 4;               // x500 map units
        model.Find("mousespeed").Value = 999;               // out of range: clamped to 20
        model.Apply();

        Assert.Equal(0.7, (double)General.Settings.DoubleSidedAlpha, 3);
        Assert.Equal(90, General.Settings.VisualFOV);
        Assert.Equal(2000f, General.Settings.ViewDistance);
        Assert.Equal(2000, General.Settings.MouseSpeed);

        var again = new PreferencesModel();                 // reads back what was written
        Assert.Equal(3, again.Find("doublesidedalpha").Value);
        Assert.Equal(9, again.Find("fieldofview").Value);
        Assert.Equal(4, again.Find("viewdistance").Value);
    }

    [Fact]
    public void Colors_round_trip_and_choices_are_indexes()
    {
        var model = new PreferencesModel();
        int red = PixelColor.FromInt(unchecked((int)0xFFCC2200)).ToInt();

        model.Find("colorgrid").Value = red;
        model.Find("dockersposition").Value = 2;
        model.Apply();

        Assert.Equal(red, General.Colors.Grid.ToInt());
        Assert.Equal(2, General.Settings.DockersPosition);
        Assert.Equal(new[] { "Left", "Right", "None" }, model.Find("dockersposition").Choices);
    }

    [Fact]
    public void Changing_the_brightness_asks_for_the_resources_to_be_loaded_again()
    {
        var model = new PreferencesModel();
        Assert.False(model.ReloadResources);

        var brightness = model.Find("imagebrightness");
        brightness.Value = brightness.Value is int v && v < 10 ? v + 1 : 0;

        Assert.True(model.ReloadResources);
    }

    [Fact]
    public void A_screenshots_folder_that_does_not_exist_is_refused_only_when_it_was_changed()
    {
        var model = new PreferencesModel();
        Assert.Null(model.Validate());                      // untouched: whatever it is, it is accepted

        model.Find("screenshotspath").Value = Path.Combine(dir, "nope");
        Assert.Contains("does not exist", model.Validate());

        model.Find("screenshotspath").Value = dir;
        Assert.Null(model.Validate());
        model.Apply();
        Assert.Equal(dir, General.Settings.ScreenshotsPath);
    }

    [Fact]
    public void The_number_of_recent_files_is_kept_between_eight_and_twenty_five()
    {
        var model = new PreferencesModel();
        model.Find("recentfiles").Value = 3;
        Assert.Equal(8, model.Find("recentfiles").Value);
        model.Find("recentfiles").Value = 100;
        Assert.Equal(25, model.Find("recentfiles").Value);
    }
}
