using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder;
using DoomBuilder.App.Dialogs;
using Xunit;
using Button = Avalonia.Controls.Button;
using ListBox = Avalonia.Controls.ListBox;
using TextBox = Avalonia.Controls.TextBox;
using ComboBox = Avalonia.Controls.ComboBox;

namespace DoomBuilder.App.Tests;

/// <summary>
/// Drives the real dialogs: a nested dispatcher loop shows each one, and a timer clicks through it the way a user would,
/// while the editor underneath reacts exactly as it does in the application.
/// </summary>
public class DialogTests : EditorTestBase
{
    // the level name box (the game configuration combo has a text box of its own inside)
    private static TextBox LevelName(Window dialog) => dialog.GetVisualDescendants().OfType<TextBox>().First(t => t.MaxLength == 8);

    private static string SampleWad => FindRepoFile("assets", "samples", "sample.wad");

    [AvaloniaFact]
    public void A_message_box_returns_the_button_the_user_pressed()
    {
        OpenEditor(withMap: false);
        WhenShown<MessageBoxWindow>(box => Click(box.ButtonFor(System.Windows.Forms.DialogResult.No)));

        var answer = General.Dialogs.ShowMessage("Save changes?", "Question", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

        Assert.Equal(System.Windows.Forms.DialogResult.No, answer);
    }

    [AvaloniaFact]
    public void Closing_a_message_box_with_the_window_means_cancel_or_no()
    {
        OpenEditor(withMap: false);
        WhenShown<MessageBoxWindow>(box => box.Close());

        var answer = General.Dialogs.ShowMessage("Sure?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

        Assert.Equal(System.Windows.Forms.DialogResult.No, answer);
    }

    [AvaloniaFact]
    public void Open_map_shows_the_wad_maps_and_the_matching_game_configuration()
    {
        OpenEditor(withMap: false);
        string configname = null;
        List<string> maps = null;
        WhenShown<MapOptionsWindow>(dialog =>
        {
            configname = (dialog.ConfigBox.SelectedItem as CodeImp.DoomBuilder.Config.ConfigurationInfo)?.Name;
            maps = dialog.GetVisualDescendants().OfType<ListBox>().First().ItemsSource.Cast<string>().ToList();
            Click(dialog.CancelButton);
        });

        General.OpenMapFile(SampleWad, null);

        Assert.Equal(new[] { "MAP01" }, maps);
        Assert.Contains("Doom", configname);
        Assert.Null(General.Map);                       // cancelled: nothing opened
    }

    [AvaloniaFact]
    public void Open_map_through_the_dialog_opens_it_after_confirming_the_missing_resources()
    {
        OpenEditor(withMap: false);
        WhenShown<MapOptionsWindow>(dialog => Click(dialog.OkButton));
        // no IWAD configured and no resources: the editor asks whether to go on
        WhenShown<MessageBoxWindow>(box => Click(box.ButtonFor(System.Windows.Forms.DialogResult.Yes)));

        General.OpenMapFile(SampleWad, null);

        Assert.NotNull(General.Map);
        Assert.Equal(16, General.Map.Map.Vertices.Count);
        Assert.Equal("MAP01", General.Map.Options.CurrentName);
    }

    [AvaloniaFact]
    public void Declining_the_missing_resources_question_keeps_the_dialog_open_and_opens_nothing()
    {
        OpenEditor(withMap: false);
        WhenShown<MapOptionsWindow>(dialog =>
        {
            // Clicking OK shows the question and does not return until it is answered, so the answer is arranged first
            WhenShown<MessageBoxWindow>(box =>
            {
                Click(box.ButtonFor(System.Windows.Forms.DialogResult.No));
                Dispatcher.UIThread.Post(() => Click(dialog.CancelButton));                // then give up on the dialog
            });
            Click(dialog.OkButton);
        });

        General.OpenMapFile(SampleWad, null);

        Assert.Null(General.Map);
    }

    [AvaloniaFact]
    public void New_map_asks_for_the_name_and_creates_an_empty_map()
    {
        OpenEditor(withMap: false);
        string suggested = null;
        WhenShown<MapOptionsWindow>(dialog =>
        {
            suggested = LevelName(dialog).Text;
            Click(dialog.OkButton);
        });
        WhenShown<MessageBoxWindow>(box => Click(box.ButtonFor(System.Windows.Forms.DialogResult.Yes)));   // no resources

        General.Actions.InvokeAction("builder_newmap");

        Assert.False(string.IsNullOrEmpty(suggested));                 // the configuration's default name (MAP01, E1M1...)
        Assert.NotNull(General.Map);
        Assert.Equal(suggested, General.Map.Options.CurrentName);
        Assert.Empty(General.Map.Map.Linedefs);
    }

    [AvaloniaFact]
    public void A_level_name_the_configuration_rejects_is_refused_with_a_message()
    {
        OpenEditor(withMap: false);
        string message = null;
        WhenShown<MapOptionsWindow>(dialog =>
        {
            LevelName(dialog).Text = "";
            WhenShown<MessageBoxWindow>(box =>
            {
                message = box.Message;
                Click(box.ButtonFor(System.Windows.Forms.DialogResult.OK));
                Dispatcher.UIThread.Post(() => Click(dialog.CancelButton), DispatcherPriority.Background);
            });
            Click(dialog.OkButton);
        });

        General.Actions.InvokeAction("builder_newmap");

        Assert.Contains("level name", message);
        Assert.Null(General.Map);
    }

    private Menu MainMenu => (Menu)window.FindControl<ContentControl>("MenuHost").Content;

    [AvaloniaFact]
    public void Closing_with_unsaved_changes_asks_and_Cancel_keeps_the_editor_open()
    {
        OpenEditor();
        General.Map.IsChanged = true;
        string asked = null;
        WhenShown<MessageBoxWindow>(box => { asked = box.Message; Click(box.ButtonFor(System.Windows.Forms.DialogResult.Cancel)); });

        window.Close();
        Dispatcher.UIThread.RunJobs();                  // the question is asked from the dispatcher

        Assert.Contains("save changes", asked);
        Assert.True(window.IsVisible);
        Assert.NotNull(General.Map);
    }

    [AvaloniaFact]
    public void Closing_and_choosing_No_discards_the_changes_and_ends_the_editor()
    {
        OpenEditor();
        General.Map.IsChanged = true;
        WhenShown<MessageBoxWindow>(box => Click(box.ButtonFor(System.Windows.Forms.DialogResult.No)));

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.IsVisible);
        Assert.Null(General.Map);                       // Terminate(true) ran
    }

    [AvaloniaFact]
    public void Closing_without_changes_does_not_ask()
    {
        OpenEditor();
        bool asked = false;
        WhenShown<MessageBoxWindow>(box => { asked = true; Click(box.ButtonFor(System.Windows.Forms.DialogResult.Cancel)); });

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.False(asked);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void An_opened_map_is_listed_in_the_recent_files_and_clicking_it_offers_to_open_it_again()
    {
        OpenEditor();
        RefreshShell();

        var file = MainMenu.Items.OfType<MenuItem>().First(m => m.Header is DockPanel d && d.Children.OfType<Avalonia.Controls.Primitives.AccessText>().Any(a => a.Text.Replace("_", "") == "File"));
        var recent = file.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag is string path && path.EndsWith("sample.wad"));
        Assert.NotNull(recent);

        string title = null;
        WhenShown<MapOptionsWindow>(dialog => { title = dialog.Title; Click(dialog.CancelButton); });
        recent.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Contains("sample.wad", title);
    }
}

public class ErrorsAndAboutTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_errors_window_lists_what_the_logger_has_and_clears_it()
    {
        OpenEditor();
        General.ErrorLogger.Clear();
        General.ErrorLogger.Add(CodeImp.DoomBuilder.ErrorType.Error, "first problem");
        General.ErrorLogger.Add(CodeImp.DoomBuilder.ErrorType.Warning, "second problem");
        ErrorsWindow seen = null;
        WhenShown<ErrorsWindow>(w =>
        {
            seen = w;
            Assert.Equal(new[] { "first problem", "second problem" }, w.Items.Select(i => i.Description));
            Assert.True(w.ClearButton.IsEnabled);
            Assert.False(w.CopyButton.IsEnabled);

            w.List.SelectedItems.Add(w.List.Items[1]);
            Assert.True(w.CopyButton.IsEnabled);
            Assert.Equal("second problem", w.SelectedText());
            Assert.False(w.ShowSourceButton.IsEnabled);       // plain items have no source to show

            Click(w.ClearButton);
            Assert.Empty(w.Items);
            Assert.False(w.ClearButton.IsEnabled);
            w.Close();
        });

        General.MainWindow.ShowErrors();

        Assert.NotNull(seen);
        Assert.False(General.ErrorLogger.HasErrors);
    }

    [AvaloniaFact]
    public void The_errors_window_keeps_the_show_on_errors_setting()
    {
        OpenEditor();
        General.Settings.ShowErrorsWindow = true;
        WhenShown<ErrorsWindow>(w => { w.ShowOnErrors.IsChecked = false; w.Close(); });

        General.MainWindow.ShowErrors();

        Assert.False(General.Settings.ShowErrorsWindow);
    }

    [AvaloniaFact]
    public void The_errors_window_picks_up_errors_added_while_it_is_open()
    {
        OpenEditor();
        General.ErrorLogger.Clear();
        int rows = -1;
        WhenShown<ErrorsWindow>(w =>
        {
            General.ErrorLogger.Add(CodeImp.DoomBuilder.ErrorType.Warning, "late arrival");
            int waited = 0;
            var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            poll.Tick += (s, e) =>
            {
                if (w.Items.Count == 0 && ++waited < 100) return;
                poll.Stop();
                rows = w.Items.Count;
                w.Close();
            };
            poll.Start();
        });

        General.MainWindow.ShowErrors();

        Assert.Equal(1, rows);
    }

    [AvaloniaFact]
    public void Help_About_and_the_warnings_indicator_open_their_windows()
    {
        OpenEditor();
        bool about = false, errors = false;
        WhenShown<AboutWindow>(w =>
        {
            about = w.VersionBlock.Text.StartsWith("TheDoomBuilder v");
            w.Close();
        });
        var model = DoomBuilder.App.Shell.UiModel.Load();
        var item = model["menumain"].AllItems().First(i => i.Handler == "itemhelpabout_Click");
        new DoomBuilder.App.Shell.ShellCommands(() => { }, _ => { }).Invoke(item);
        Assert.True(about);

        WhenShown<ErrorsWindow>(w => { errors = true; w.Close(); });
        General.Actions.InvokeAction("builder_showerrors");     // the action MainForm bound
        Assert.True(errors);
    }
}

public class ConfigWindowTests : EditorTestBase
{
    [AvaloniaFact]
    public void The_game_configurations_dialog_applies_what_the_user_changed_on_OK()
    {
        OpenEditor();
        var current = General.Configs.First(c => c.Filename == General.Map.ConfigSettings.Filename);
        string original = current.TestProgram;
        bool seenselected = false;
        WhenShown<ConfigWindow>(w =>
        {
            seenselected = w.ConfigList.SelectedIndex == General.Configs.IndexOf(current);   // opens on the map's configuration
            w.Tabs.SelectedIndex = ConfigWindow.TestingPage;
            w.ProgramBox.Text = "/opt/test/engine/run";
            Click(w.OkButton);
        });

        General.MainWindow.ShowConfiguration();

        Assert.True(seenselected);
        Assert.Equal("/opt/test/engine/run", current.TestProgram);
        Assert.Equal("engine", current.TestProgramName);
        current.TestProgram = original;
    }

    [AvaloniaFact]
    public void Cancel_throws_the_changes_away()
    {
        OpenEditor();
        var current = General.Configs.First(c => c.Filename == General.Map.ConfigSettings.Filename);
        string original = current.TestProgram;
        WhenShown<ConfigWindow>(w =>
        {
            w.ProgramBox.Text = "/somewhere/else";
            Click(w.CancelButton);
        });

        General.MainWindow.ShowConfiguration();

        Assert.Equal(original, current.TestProgram);
    }

    [AvaloniaFact]
    public void A_missing_resource_keeps_the_dialog_open_and_shows_the_warning()
    {
        OpenEditor();
        string asked = null;
        WhenShown<ConfigWindow>(w =>
        {
            w.ResourcesBox.Add("/definitely/not/here.wad");
            w.Model.Entries[w.ConfigList.SelectedIndex].Enabled = true;      // only enabled configurations are checked
            General.Dialogs = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
            Click(w.OkButton);
            asked = ((CodeImp.DoomBuilder.Windows.ScriptedDialogs)General.Dialogs).Messages.FirstOrDefault();
            Assert.True(w.IsVisible);                     // still open
            w.Close(false);
        });

        General.MainWindow.ShowConfiguration();

        Assert.Contains("doesn't exist", asked);
    }

    [AvaloniaFact]
    public void The_page_asked_for_is_the_one_shown()
    {
        OpenEditor();
        int page = -1;
        WhenShown<ConfigWindow>(w => { page = w.Tabs.SelectedIndex; Click(w.CancelButton); });

        General.MainWindow.ShowConfigurationPage(ConfigWindow.NodebuildersPage);

        Assert.Equal(ConfigWindow.NodebuildersPage, page);
    }
}

public class PreferencesWindowTests : EditorTestBase
{
    [Theory]
    [InlineData("#FF8000", true, unchecked((int)0xFFFF8000))]
    [InlineData("00ff00", true, unchecked((int)0xFF00FF00))]
    [InlineData("#FF80", false, 0)]
    [InlineData("zzzzzz", false, 0)]
    [InlineData("", false, 0)]
    public void Hex_color_codes_are_parsed_as_opaque_colors(string text, bool ok, int expected)
    {
        Assert.Equal(ok, PreferencesWindow.TryParseHex(text, out int argb));
        if (ok) Assert.Equal(expected, argb);
    }

    [AvaloniaFact]
    public void OK_applies_what_was_edited_to_the_settings_and_the_interface_follows()
    {
        OpenEditor();
        RefreshShell();
        Assert.True(window.Dockers.IsVisible);
        WhenShown<PreferencesWindow>(w =>
        {
            ((CheckBox)w.EditorOf("showfps")).IsChecked = !General.Settings.ShowFPS;
            ((Slider)w.EditorOf("fieldofview")).Value = 12;
            ((ComboBox)w.EditorOf("dockersposition")).SelectedIndex = 2;            // None
            ((TextBox)w.EditorOf("colorgrid")).Text = "#336699";
            Click(w.OkButton);
        });
        bool fps = General.Settings.ShowFPS;

        General.Actions.InvokeAction("builder_preferences");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(!fps, General.Settings.ShowFPS);
        Assert.Equal(120, General.Settings.VisualFOV);
        Assert.Equal(2, General.Settings.DockersPosition);
        Assert.Equal(unchecked((int)0xFF336699), General.Colors.Grid.ToInt());
        Assert.False(window.Dockers.IsVisible);                                      // the interface was refreshed
    }

    [AvaloniaFact]
    public void The_BuilderModes_plugin_adds_its_Editing_tab_and_saves_its_settings_on_OK()
    {
        OpenEditor();
        int before = General.Settings.ReadPluginSetting("buildermodes", "stitchrange", 20);
        bool seen = false;
        WhenShown<PreferencesWindow>(w =>
        {
            var tab = w.Tabs.Items.OfType<TabItem>().First(t => (string)t.Header == "Editing");
            seen = true;
            var row = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants((Avalonia.Controls.Control)tab.Content).OfType<StackPanel>()
                .First(p => p.Children.OfType<TextBlock>().Any(t => t.Text == "Stitch geometry within:"));
            var box = row.Children.OfType<DoomBuilder.UI.NumberBox>().Single();
            Assert.Equal(before.ToString(), box.Text);
            box.Text = "33";
            Click(w.OkButton);
        });

        General.Actions.InvokeAction("builder_preferences");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(seen);
        Assert.Equal(33, General.Settings.ReadPluginSetting("buildermodes", "stitchrange", 20));
        Assert.Equal(33f, CodeImp.DoomBuilder.BuilderModes.BuilderPlug.Me.StitchRange);   // the plugin reloaded its settings when the dialog closed
    }

    [AvaloniaFact]
    public void Cancel_changes_nothing()
    {
        OpenEditor();
        int fov = General.Settings.VisualFOV;
        WhenShown<PreferencesWindow>(w =>
        {
            ((Slider)w.EditorOf("fieldofview")).Value = (fov / 10 == 5) ? 6 : 5;
            Click(w.CancelButton);
        });

        General.Actions.InvokeAction("builder_preferences");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(fov, General.Settings.VisualFOV);
    }

    [AvaloniaFact]
    public void A_screenshots_folder_that_does_not_exist_keeps_the_window_open_with_a_message()
    {
        OpenEditor();
        string asked = null;
        bool stillopen = false;
        WhenShown<PreferencesWindow>(w =>
        {
            General.Dialogs = new CodeImp.DoomBuilder.Windows.ScriptedDialogs();
            ((TextBox)w.EditorOf("screenshotspath")).Text = "/definitely/not/a/folder";
            Click(w.OkButton);
            asked = ((CodeImp.DoomBuilder.Windows.ScriptedDialogs)General.Dialogs).Messages.FirstOrDefault();
            stillopen = w.IsVisible;
            w.Close(false);
        });

        General.Actions.InvokeAction("builder_preferences");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains("does not exist", asked);
        Assert.True(stillopen);
    }

    [AvaloniaFact]
    public void Every_tab_has_its_controls()
    {
        OpenEditor();
        int tabs = 0, editors = 0;
        WhenShown<PreferencesWindow>(w =>
        {
            tabs = w.Tabs.ItemCount;
            editors = w.Model.Items.Count(i => w.EditorOf(i.Key) != null);
            Assert.Equal(new[] { "Interface", "Display", "Recovery", "Colors", "Script editor", "Editing", "3D Floor Plugin", "UDBScript" }, w.Tabs.Items.OfType<TabItem>().Select(t => (string)t.Header));
            Click(w.CancelButton);
        });

        General.Actions.InvokeAction("builder_preferences");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(8, tabs);                    // the program's five (with the script editor), then the plugins' (BuilderModes, ThreeDFloorMode, UDBScript)
        Assert.True(editors > 40);
    }
}
