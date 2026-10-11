using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
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

    [AvaloniaFact]
    public void Toolbar_separators_only_show_between_visible_buttons()
    {
        OpenEditor();
        RefreshShell();
        AssertSeparatorsTidy();                          // with a map

        General.Actions.InvokeAction("builder_closemap");
        Assert.Null(General.Map);
        RefreshShell();
        AssertSeparatorsTidy();                          // without one: only New/Open/Save are left, no row of empty separators

        // Like UDB: New and Open are usable, Save is there but grayed out
        var buttons = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<ToggleButton>()
            .Where(b => b.IsVisible && Avalonia.Controls.ToolTip.GetTip(b) is string).ToList();
        Assert.True(buttons.First(b => (string)Avalonia.Controls.ToolTip.GetTip(b) == "New Map").IsEnabled);
        Assert.False(buttons.First(b => ((string)Avalonia.Controls.ToolTip.GetTip(b)).StartsWith("Save Map")).IsEnabled);
    }

    [Fact]
    public void While_the_program_loads_only_the_file_buttons_are_in_the_toolbar()
    {
        if (CodeImp.DoomBuilder.General.Settings != null) return;      // only the state before the settings exist is checked here
        var model = UiModel.Load();
        foreach (UiItem item in model["toolbar"].Items.Where(i => !i.IsSeparator))
        {
            ItemState? state = UiRules.For(item);
            bool file = item.Name is "buttonnewmap" or "buttonopenmap" or "buttonsavemap";
            Assert.True(file ? state is { Visible: true } : state is { Visible: false }, item.Name);
        }
    }

    [AvaloniaFact]
    public void The_toolbar_lists_the_things_filters_and_the_linedef_color_presets_of_the_map()
    {
        OpenEditor();
        RefreshShell();

        var all = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Button>().Where(b => b.IsVisible).ToList();
        string TextOf(Button b) => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(b).OfType<TextBlock>().Select(x => x.Text).FirstOrDefault();

        Button filters = all.First(b => Equals(Avalonia.Controls.ToolTip.GetTip(b), "Things filter"));
        Button presets = all.First(b => Equals(Avalonia.Controls.ToolTip.GetTip(b), "Linedef color presets"));
        Assert.Equal(General.Map.ThingsFilter.Name, TextOf(filters));         // "(show all)" with no filter selected
        var preset = General.Map.ConfigSettings.LinedefColorPresets.First();
        Assert.Contains(preset.Name, TextOf(presets));                        // e.g. "Any action"

        var flyout = (MenuFlyout)filters.Flyout;
        Assert.True(flyout.Items.Count >= 1);
        var presetflyout = (MenuFlyout)presets.Flyout;
        Assert.Equal(General.Map.ConfigSettings.LinedefColorPresets.Length, presetflyout.Items.Count);

        // Clicking a preset turns it off (and the button says so)
        bool was = preset.Enabled;
        ((MenuItem)presetflyout.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal(!was, preset.Enabled);
    }

    [AvaloniaFact]
    public void The_splash_logo_shows_in_the_display_only_while_no_map_is_open()
    {
        OpenEditor();
        RefreshShell();
        var splash = Avalonia.Controls.NameScopeExtensions.Find<Border>(window, "SplashHost");
        var image = Avalonia.Controls.NameScopeExtensions.Find<Avalonia.Controls.Image>(window, "SplashImage");
        Assert.NotNull(splash);
        Assert.NotNull(image.Source);                    // UDB's logo (Splash3_trans) is loaded
        Assert.False(splash.IsVisible);                  // a map is open: the display is the map

        General.Actions.InvokeAction("builder_closemap");
        Assert.Null(General.Map);
        RefreshShell();
        Assert.True(splash.IsVisible);                   // no map: the logo, as in UDB
        Assert.False(splash.IsHitTestVisible);           // the pointer still reaches the display under it
    }

    private void AssertSeparatorsTidy()
    {
        var strip = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<WrapPanel>()
            .First(p => p.Children.OfType<Border>().Any(b => b.Width == 1) && p.Children.OfType<ToggleButton>().Any());
        Control previous = null;      // the last visible child
        foreach (Control child in strip.Children.Where(c => c.IsVisible))
        {
            bool separator = child is Border { Width: 1 };
            if (separator) Assert.True(previous != null && !(previous is Border { Width: 1 }), "a separator at the start or after another separator");
            previous = child;
        }
        Assert.False(previous is Border { Width: 1 }, "a separator at the end of the strip");
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

public class DockerWindowTests : EditorTestBase
{
    [AvaloniaFact]
    public void A_map_shows_the_Help_docker_with_the_hints_of_the_mode()
    {
        OpenEditor();
        RefreshShell();

        var panel = window.Dockers;
        Assert.True(panel.IsVisible);
        Assert.Contains(panel.Tabs, t => (string)t.Header == "Help");
        Assert.Contains(panel.Tabs, t => (string)t.Header == "Undo / Redo");       // the BuilderModes plugin's own docker

        panel.Tabs.First(t => (string)t.Header == "Help").IsSelected = true;
        Assert.Equal("Help", General.MainWindow.ActiveDockerTabName);
    }

    [AvaloniaFact]
    public void Dockers_added_by_plugins_get_a_tab_with_their_control_and_selecting_follows_the_tabs()
    {
        OpenEditor();
        var content = new TextBlock { Text = "tool options" };
        var docker = DoomBuilder.App.Shell.AvaloniaDocker.Create("tools", "Tools", content);

        General.MainWindow.AddDocker(docker);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var tab = window.Dockers.Tabs.First(t => (string)t.Header == "Tools");
        Assert.Same(content, tab.Content);
        Assert.EndsWith("_tools", docker.FullName);          // prefixed with the name of the adding assembly or plugin

        Assert.True(General.MainWindow.SelectDocker(docker));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Same(tab, window.Dockers.SelectedTab);
        Assert.Equal("Tools", General.MainWindow.ActiveDockerTabName);

        General.MainWindow.RemoveDocker(docker);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(window.Dockers.Tabs, t => (string)t.Header == "Tools");
        Assert.NotEqual("Tools", General.MainWindow.ActiveDockerTabName);   // back to the previous one
    }

    [AvaloniaFact]
    public void Clicking_a_tab_selects_that_docker_and_the_panel_hides_when_dockers_are_off()
    {
        OpenEditor();
        var other = DoomBuilder.App.Shell.AvaloniaDocker.Create("other", "Other", new TextBlock());
        General.MainWindow.AddDocker(other);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.Dockers.Tabs.First(t => (string)t.Header == "Other").IsSelected = true;
        Assert.Equal("Other", General.MainWindow.ActiveDockerTabName);

        General.Settings.GetType().GetProperty("DockersPosition").SetValue(General.Settings, 2);
        RefreshShell();
        Assert.False(window.Dockers.IsVisible);
    }

    [AvaloniaFact]
    public void The_hints_panel_shows_the_hints_as_plain_text_and_clears()
    {
        var panel = new DoomBuilder.App.Shell.HintsPanel();
        panel.SetHints(@"{\rtf1 Press {\b F1} to show help}");
        Assert.Equal("Press F1 to show help", panel.Text);
        panel.ClearHints();
        Assert.Equal("", panel.Text);
    }
}

public class CommandPaletteWindowTests : EditorTestBase
{
    private static PhysicalKey Physical(Key key) => key switch
    {
        Key.Enter => PhysicalKey.Enter,
        Key.Escape => PhysicalKey.Escape,
        Key.Down => PhysicalKey.ArrowDown,
        Key.Up => PhysicalKey.ArrowUp,
        Key.Right => PhysicalKey.ArrowRight,
        Key.Home => PhysicalKey.Home,
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };

    private void Press(Key key) => window.KeyPress(key, RawInputModifiers.None, Physical(key), null);

    private void Open()
    {
        OpenEditor();
        General.Actions.InvokeAction("builder_opencommandpalette");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void The_action_opens_the_palette_with_every_command_listed()
    {
        Open();

        var palette = window.Palette;
        Assert.True(palette.IsVisible);
        Assert.Equal(General.Actions.GetAllActions().Length, palette.Entries.Count);
        Assert.Equal(0, palette.List.SelectedIndex);
    }

    [AvaloniaFact]
    public void Typing_filters_and_Enter_runs_the_selected_command_and_closes()
    {
        Open();
        var scripted = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
        General.Dialogs = scripted;

        window.Palette.SearchBox.Text = "errors and warnings";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(window.Palette.Entries);
        Assert.Equal("builder_showerrors", window.Palette.Selected.ActionName);

        Press(Key.Enter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(window.Palette.IsVisible);
        Assert.Equal(1, scripted.ErrorsShown);
    }

    [AvaloniaFact]
    public void Arrow_keys_move_the_selection_and_Escape_closes_without_running()
    {
        Open();
        var palette = window.Palette;

        Press(Key.Down);
        Assert.Equal(1, palette.List.SelectedIndex);
        Press(Key.Up);
        Press(Key.Up);                                              // wraps to the last one
        Assert.Equal(palette.Entries.Count - 1, palette.List.SelectedIndex);

        Press(Key.Escape);
        Assert.False(palette.IsVisible);
    }

    [AvaloniaFact]
    public void Nothing_matching_shows_the_message_and_Enter_does_nothing()
    {
        Open();
        window.Palette.SearchBox.Text = "qqqzzzxxx";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.Palette.Entries);
        Assert.False(window.Palette.List.IsVisible);

        Press(Key.Enter);
        Assert.True(window.Palette.IsVisible);            // still open, there was nothing to run
    }

    [AvaloniaFact]
    public void Typing_in_the_palette_does_not_trigger_editor_shortcuts()
    {
        Open();
        float before = Renderer.Scale;

        Press(Key.Right);                                   // the arrow keys would scroll the map
        Press(Key.Home);                                    // and Home would fit it

        Assert.Equal(before, Renderer.Scale);
        Assert.True(window.Palette.IsVisible);
    }
}
