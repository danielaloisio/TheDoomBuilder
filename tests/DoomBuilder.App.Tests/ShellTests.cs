using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Editing;
using DoomBuilder.App.Shell;
using Xunit;

namespace DoomBuilder.App.Tests;

public class UiModelTests
{
    [Fact]
    public void The_extracted_layout_has_the_menus_toolbar_and_status_bar_of_udb()
    {
        var model = UiModel.Load();

        Assert.Equal(new[] { "&File", "&Edit", "&View", "&Mode", "&Prefabs", "&Tools", "&Help" },
                     model["menumain"].Items.Select(i => i.Text));
        Assert.True(model["toolbar"].AllItems().Count(i => i.Type == "button") >= 30);
        Assert.Contains(model["statusbar"].AllItems(), i => i.Name == "zoomlabel");
    }

    [AvaloniaFact]     // decodes bitmaps, which needs the Avalonia rendering platform (headless) up
    public void Every_item_that_invokes_an_action_names_one_and_every_icon_exists()
    {
        var model = UiModel.Load();

        foreach (var strip in model.Values)
            foreach (UiItem item in strip.AllItems())
            {
                if (item.InvokesAction) Assert.StartsWith("builder_", item.Action.Contains('_') ? item.Action : "builder_" + item.Action);
                if (item.Image != null) Assert.True(ImageCache.Get(item.Image) != null, "icon " + item.Image + " of " + item.Name);
            }
    }

    [Fact]
    public void Custom_handlers_carry_a_numeric_argument()
    {
        var zoom = UiModel.Load()["menumain"].AllItems().Where(i => i.Handler == "itemzoomto_Click").ToList();

        Assert.NotEmpty(zoom);
        Assert.All(zoom, i => Assert.True(int.TryParse(i.Action, out _), i.Name));
    }

    [Theory]
    [InlineData("&File", "_File")]
    [InlineData("Open &Map...", "Open _Map...")]
    [InlineData("No access key", "No access key")]
    [InlineData("snake_case &Item", "snake__case _Item")]
    [InlineData("Trailing &", "Trailing ")]
    [InlineData(null, "")]
    public void Access_keys_follow_the_avalonia_convention(string winforms, string expected)
        => Assert.Equal(expected, ShellUi.AccessKeyText(winforms));
}

public class RtfTextTests
{
    [Fact]
    public void Formatting_is_dropped_and_the_text_kept()
        => Assert.Equal("Press F1 to show help", RtfText.ToPlain(@"{\rtf1 Press {\b F1} to show help}"));

    [Fact]
    public void Paragraphs_become_line_breaks()
        => Assert.Equal("one\ntwo", RtfText.ToPlain(@"{\rtf1 one\par two}"));

    [Fact]
    public void Escaped_characters_survive()
        => Assert.Equal(@"a{b}c\d", RtfText.ToPlain(@"{\rtf1 a\{b\}c\\d}"));

    [Fact]
    public void Code_page_escapes_are_decoded()
        => Assert.Equal("café", RtfText.ToPlain(@"{\rtf1 caf\'e9}"));

    [Fact]
    public void Plain_text_is_left_alone()
        => Assert.Equal("just text", RtfText.ToPlain("just text"));
}

public class ShellWindowTests : EditorTestBase
{
    private Menu Menu => (Menu)window.FindControl<ContentControl>("MenuHost").Content;

    private MenuItem Top(string header) => Menu.Items.OfType<MenuItem>().First(m => m.Header is DockPanel d && d.Children.OfType<AccessText>().Any(a => a.Text.Replace("_", "") == header));

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [AvaloniaFact]
    public void Before_a_map_is_open_only_the_menus_that_need_none_are_shown()
    {
        window = new MainWindow(startEditor: false);
        window.Show();

        Assert.True(Top("File").IsVisible);
        Assert.True(Top("Help").IsVisible);
        Assert.False(Top("Edit").IsVisible);
        Assert.False(Top("View").IsVisible);
        Assert.False(Top("Mode").IsVisible);
    }

    [AvaloniaFact]
    public void Opening_a_map_reveals_the_map_menus_and_the_title_and_status_follow()
    {
        OpenEditor();
        RefreshShell();

        Assert.True(Top("Edit").IsVisible);
        Assert.True(Top("View").IsVisible);
        Assert.Contains("room", window.Title.ToLowerInvariant().Replace("sample", "room"));
        Assert.Contains("MAP01", window.Title);
        Assert.False(string.IsNullOrEmpty(window.FindControl<TextBlock>("ConfigText").Text));
    }

    [AvaloniaFact]
    public void Undo_is_disabled_until_there_is_something_to_undo()
    {
        OpenEditor();
        RefreshShell();

        var edit = Top("Edit");
        var undo = edit.Items.OfType<MenuItem>().First(m => m.Header is DockPanel d && d.Children.OfType<AccessText>().Any(a => a.Text.StartsWith("Undo")));
        Assert.False(undo.IsEnabled);
    }

    [AvaloniaFact]
    public void Clicking_a_zoom_item_zooms_the_classic_mode()
    {
        OpenEditor();
        RefreshShell();

        var view = Top("View");
        var zoom25 = AllMenuItems(view).First(m => (m.Header as DockPanel)?.Children.OfType<AccessText>().Any(a => a.Text == "25%") == true);
        Click(zoom25);

        Assert.Equal(0.25f, General.Map.Renderer2D.Scale, 3);
    }

    [AvaloniaFact]
    public void Clicking_an_action_item_invokes_the_action()
    {
        OpenEditor();
        RefreshShell();
        Assert.NotNull(General.Map);

        var file = Top("File");
        var close = AllMenuItems(file).First(m => (m.Header as DockPanel)?.Children.OfType<AccessText>().Any(a => a.Text.Contains("Close")) == true);
        Click(close);

        Assert.Null(General.Map);                      // builder_closemap ran
    }

    private static System.Collections.Generic.IEnumerable<MenuItem> AllMenuItems(MenuItem root)
    {
        foreach (var child in root.Items.OfType<MenuItem>())
        {
            yield return child;
            foreach (var grand in AllMenuItems(child)) yield return grand;
        }
    }
}
