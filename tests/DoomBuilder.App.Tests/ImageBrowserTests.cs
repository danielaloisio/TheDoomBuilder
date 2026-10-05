using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;
using Xunit;

namespace DoomBuilder.App.Tests;

/// <summary>The texture/flat browser and the selector controls, on the sample map (no IWAD: the images that exist are the WAD's own).</summary>
public class ImageBrowserTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_model_always_ends_with_the_all_images_set_and_selects_it_when_nothing_matches()
    {
        OpenEditor();

        var model = new ImageBrowserModel("NOSUCHTEX", false);

        Assert.NotEmpty(model.Roots);
        Assert.Same(model.Roots.Last(), model.Selected);
        Assert.Equal(General.Map.Data.WallTextureSet.Name, model.Selected.Name);
    }

    [AvaloniaFact]
    public void Listing_at_root_level_shows_the_sets_and_up_returns_there()
    {
        OpenEditor();
        var model = new ImageBrowserModel("-", false);

        Assert.True(model.Up());            // from "All" to the root
        Assert.Null(model.Selected);
        Assert.False(model.Up());

        ImageBrowserListing root = model.List("", -1, -1, false);
        Assert.Equal(model.Roots.Count, root.Folders.Count);
        Assert.Null(root.UpCaption);

        Assert.True(model.Open(model.Roots.Last().FolderName));
        Assert.Equal("All Texture Sets", model.List("", -1, -1, false).UpCaption);
    }

    [AvaloniaFact]
    public void The_name_filter_narrows_the_list_and_the_listing_has_no_duplicates()
    {
        OpenEditor();
        var model = new ImageBrowserModel("-", false);

        ImageBrowserListing all = model.List("", -1, -1, false);
        Assert.Equal(all.Images.Count, all.Images.Select(ImageBrowserModel.DisplayName).Distinct(System.StringComparer.OrdinalIgnoreCase).Count());

        ImageBrowserListing none = model.List("ZZZ_NO_MATCH_ZZZ", -1, -1, false);
        Assert.Empty(none.Images);
        Assert.Empty(none.Folders);
    }

    [AvaloniaFact]
    public void Cancelling_the_browser_keeps_the_current_name()
    {
        OpenEditor();
        WhenShown<ImageBrowserWindow>(browser => Click(browser.GetVisualDescendants().OfType<Button>().First(b => (string)b.Content == "Cancel")));

        string result = ImageBrowserWindow.Browse(window, "STARTAN2", false);

        Assert.Equal("STARTAN2", result);
    }

    [AvaloniaFact]
    public void The_dialog_service_browses_through_the_same_window()
    {
        OpenEditor();
        WhenShown<ImageBrowserWindow>(browser => Click(browser.GetVisualDescendants().OfType<Button>().First(b => (string)b.Content == "Cancel")));

        Assert.Equal("FLOOR4_8", General.Dialogs.BrowseImage("FLOOR4_8", true));
    }

    [AvaloniaFact]
    public void A_selector_shows_the_typed_name_and_a_dash_clears_it()
    {
        OpenEditor();
        var selector = new TextureSelector { Width = 96, Height = 120 };
        window.Content = selector;
        selector.Initialize();

        selector.TextureName = "stone";
        Assert.Equal("STONE", selector.TextureName);   // names are upper-case unless the map uses long names

        int changes = 0;
        selector.ValueChanged += (s, e) => changes++;
        selector.TextureName = "-";
        Assert.Equal("-", selector.TextureName);
        Assert.Equal(1, changes);
        selector.TextureName = "  ";
        Assert.Equal("ORIG", selector.GetResult("ORIG"));   // nothing typed keeps the original
        selector.StopUpdate();
    }
}
