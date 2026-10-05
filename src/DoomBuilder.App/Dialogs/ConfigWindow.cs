using DoomBuilder.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// The game configurations (UDB's ConfigForm): the list of configurations on the left, and for the selected one its resources,
/// nodebuilders, test program and editing modes. All the logic is in <see cref="ConfigModel"/>.
/// </summary>
public sealed class ConfigWindow : Window
{
    public const int ResourcesPage = 0, NodebuildersPage = 1, TestingPage = 2, ModesPage = 3;

    private readonly ConfigModel model = new ConfigModel();
    private readonly ListBox configs = new ListBox { Width = 220 };
    private readonly TabControl tabs = new TabControl();
    private readonly ResourcesEditor resources = new ResourcesEditor();
    private readonly ComboBox nodesave = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox nodetest = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox program = new TextBox();
    private readonly TextBlock engine = new TextBlock { Opacity = 0.7 };
    private readonly ComboBox skill = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox custom = new CheckBox { Content = "Customize parameters" };
    private readonly TextBox parameters = new TextBox();
    private readonly CheckBox shortpaths = new CheckBox { Content = "Use short paths" };
    private readonly CheckBox linuxpaths = new CheckBox { Content = "Use Linux-style paths" };
    private readonly TextBlock example = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.7 };
    private readonly StackPanel customarea = new StackPanel { Spacing = 4 };
    private readonly StackPanel modeslist = new StackPanel { Spacing = 2 };
    private readonly ComboBox startmode = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Dictionary<CheckBox, ModeChoice> modeboxes = new Dictionary<CheckBox, ModeChoice>();
    private bool loading;

    public Button OkButton { get; } = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
    public Button CancelButton { get; } = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };

    /// <summary>True when OK was chosen and the resources changed.</summary>
    public bool ReloadResources { get { return model.ReloadResources; } }

    internal ConfigModel Model { get { return model; } }
    public ListBox ConfigList { get { return configs; } }
    public TabControl Tabs { get { return tabs; } }
    public ResourcesEditor ResourcesBox { get { return resources; } }
    public ComboBox NodebuilderSaveBox { get { return nodesave; } }
    public ComboBox NodebuilderTestBox { get { return nodetest; } }
    public TextBox ProgramBox { get { return program; } }
    public TextBox ParametersBox { get { return parameters; } }
    public CheckBox CustomBox { get { return custom; } }
    public CheckBox ShortPathsBox { get { return shortpaths; } }
    public CheckBox LinuxPathsBox { get { return linuxpaths; } }
    public ComboBox StartModeBox { get { return startmode; } }
    public IEnumerable<CheckBox> ModeBoxes { get { return modeboxes.Keys; } }

    public ConfigWindow(int page = -1)
    {
        Title = "Game Configurations";
        Width = 880;
        Height = 560;
        MinWidth = 700;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // ---- the list of configurations, each with a box to enable it
        configs.ItemsSource = model.Entries.Select(Row).ToList();
        configs.SelectionChanged += (s, e) => SelectEntry();

        // ---- pages
        tabs.Items.Add(new TabItem { Header = "Resources", Content = Padded(resources) });
        tabs.Items.Add(new TabItem { Header = "Nodebuilders", Content = Padded(NodebuildersPage_()) });
        tabs.Items.Add(new TabItem { Header = "Testing", Content = Padded(TestingPage_()) });
        tabs.Items.Add(new TabItem { Header = "Editing modes", Content = Padded(ModesPage_()) });
        if (page >= 0 && page < tabs.ItemCount) tabs.SelectedIndex = page;

        resources.Changed += () => { if (!loading) model.SetResources(resources.Locations); };

        OkButton.Click += (s, e) => Accept();
        CancelButton.Click += (s, e) => Close(false);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(OkButton);
        buttons.Children.Add(CancelButton);

        var left = new DockPanel { Margin = new Thickness(0, 0, 12, 0) };
        left.Children.Add(configs);

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(buttons);
        root.Children.Add(left);
        root.Children.Add(tabs);
        Content = root;

        Opened += (s, e) =>
        {
            ConfigEntry current = model.CurrentMapEntry ?? model.Entries.FirstOrDefault();
            configs.SelectedIndex = current == null ? -1 : model.Entries.ToList().IndexOf(current);
            if (configs.SelectedIndex < 0) tabs.IsEnabled = false;
        };
        Closing += (s, e) => { };
    }

    // ---- pages

    private Control NodebuildersPage_()
    {
        nodesave.ItemsSource = model.Nodebuilders;
        nodetest.ItemsSource = model.Nodebuilders;
        nodesave.SelectionChanged += (s, e) => { if (!loading) model.SetNodebuilderSave(nodesave.SelectedItem as NodebuilderInfo); };
        nodetest.SelectionChanged += (s, e) => { if (!loading) model.SetNodebuilderTest(nodetest.SelectedItem as NodebuilderInfo); };
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Nodebuilder when saving the map" });
        panel.Children.Add(nodesave);
        panel.Children.Add(new TextBlock { Text = "Nodebuilder when testing the map", Margin = new Thickness(0, 12, 0, 0) });
        panel.Children.Add(nodetest);
        return panel;
    }

    private Control TestingPage_()
    {
        var browse = new Button { Content = "Browse..." };
        browse.Click += (s, e) => BrowseProgram();
        // Watch the property itself so programmatic changes count too, not only typing
        program.PropertyChanged += (s, e) =>
        {
            if (e.Property != TextBox.TextProperty || loading) return;
            model.SetTestProgram(program.Text);
            engine.Text = model.TestEngineName;
        };
        skill.SelectionChanged += (s, e) => { if (!loading && skill.SelectedItem is SkillInfo info) { model.SetSkill(info.Index); ShowExample(); } };
        custom.IsCheckedChanged += (s, e) => { if (loading) return; model.SetCustomParameters(custom.IsChecked == true); customarea.IsVisible = custom.IsChecked == true; ShowExample(); };
        parameters.PropertyChanged += (s, e) =>
        {
            if (e.Property != TextBox.TextProperty || loading) return;
            model.SetTestParameters(parameters.Text);
            ShowExample();
        };
        shortpaths.IsCheckedChanged += (s, e) => { if (loading) return; model.SetShortPaths(shortpaths.IsChecked == true); LoadPathBoxes(); ShowExample(); };
        linuxpaths.IsCheckedChanged += (s, e) => { if (loading) return; model.SetLinuxPaths(linuxpaths.IsChecked == true); LoadPathBoxes(); ShowExample(); };

        customarea.Children.Add(new TextBlock { Text = "Parameters ( %F file, %M map, %S skill, ... )" });
        customarea.Children.Add(parameters);
        customarea.Children.Add(shortpaths);
        customarea.Children.Add(linuxpaths);
        customarea.Children.Add(example);

        var row = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        row.Children.Add(browse);
        row.Children.Add(program);

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Test program" });
        panel.Children.Add(row);
        panel.Children.Add(engine);
        panel.Children.Add(new TextBlock { Text = "Skill", Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(skill);
        panel.Children.Add(new Border { Height = 8 });
        panel.Children.Add(custom);
        panel.Children.Add(customarea);
        return panel;
    }

    private Control ModesPage_()
    {
        startmode.SelectionChanged += (s, e) => { if (!loading && startmode.SelectedItem is ModeChoice m) model.SetStartMode(m); };
        var panel = new DockPanel();
        var start = new StackPanel { Spacing = 4, Margin = new Thickness(0, 10, 0, 0) };
        start.Children.Add(new TextBlock { Text = "Start in mode" });
        start.Children.Add(startmode);
        DockPanel.SetDock(start, Dock.Bottom);
        panel.Children.Add(start);
        panel.Children.Add(new ScrollViewer { Content = modeslist });
        return panel;
    }

    private static Control Padded(Control c) => new ScrollViewer { Content = new Border { Padding = new Thickness(12), Child = c } };

    private Control Row(ConfigEntry entry)
    {
        var box = new CheckBox { Content = entry.Name, IsChecked = entry.Enabled };
        box.IsCheckedChanged += (s, e) => { entry.Enabled = box.IsChecked == true; box.Opacity = entry.Enabled ? 1 : 0.5; };
        box.Opacity = entry.Enabled ? 1 : 0.5;
        box.Tag = entry;
        return box;
    }

    // ---- selection

    private void SelectEntry()
    {
        int index = configs.SelectedIndex;
        ConfigEntry entry = index >= 0 && index < model.Entries.Count ? model.Entries[index] : null;

        loading = true;
        try
        {
            model.Select(entry);
            tabs.IsEnabled = entry != null;
            if (entry == null) return;

            resources.StartPath = General.Map?.FilePathName;
            resources.Locations = model.Resources;

            nodesave.SelectedItem = model.NodebuilderSave;
            nodetest.SelectedItem = model.NodebuilderTest;

            program.Text = model.TestProgram;
            engine.Text = model.TestEngineName;
            skill.ItemsSource = model.Game.Skills.ToList();
            skill.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SkillInfo>((i, _) => new TextBlock { Text = i == null ? "" : i.Index + " - " + i.Title });
            skill.SelectedItem = model.Game.Skills.FirstOrDefault(k => k.Index == model.Skill) ?? model.Game.Skills.FirstOrDefault();
            custom.IsChecked = model.CustomParameters;
            customarea.IsVisible = model.CustomParameters;
            parameters.Text = model.TestParameters;
            LoadPathBoxes();
            ShowExample();

            FillModes();
        }
        finally { loading = false; }
    }

    private void LoadPathBoxes()
    {
        bool before = loading;
        loading = true;
        shortpaths.IsChecked = model.ShortPaths;
        linuxpaths.IsChecked = model.LinuxPaths;
        loading = before;
    }

    private void ShowExample()
    {
        string text = model.ParametersExample();
        example.Text = text == null ? "(open a map to see the result)" : "Result: " + text;
    }

    private void FillModes()
    {
        modeslist.Children.Clear();
        modeboxes.Clear();
        foreach (ModeChoice mode in model.Modes)
        {
            var box = new CheckBox { Content = mode.Text + "   [" + mode.Info.Plugin.Plug.Name + "]", IsChecked = mode.Enabled, IsEnabled = mode.Supported };
            box.IsCheckedChanged += (s, e) =>
            {
                if (loading) return;
                model.SetModeEnabled(mode, box.IsChecked == true);
                loading = true; FillStartModes(); loading = false;
            };
            modeboxes[box] = mode;
            modeslist.Children.Add(box);
        }
        FillStartModes();
    }

    private void FillStartModes()
    {
        startmode.ItemsSource = model.StartModes;
        startmode.SelectedItem = model.StartMode;
    }

    // ---- actions

    private void BrowseProgram()
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = DialogPump.Run(() => storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select test program",
            AllowMultiple = false,
            FileTypeFilter = OperatingSystem.IsWindows()
                ? new[] { new FilePickerFileType("Programs") { Patterns = new[] { "*.exe", "*.bat", "*.cmd" } }, FilePickerFileTypes.All }
                : new[] { FilePickerFileTypes.All },
        }));
        if (files != null && files.Count > 0 && files[0].TryGetLocalPath() is string path) program.Text = path;
    }

    private async void Accept()
    {
        ConfigEntry bad = model.FindInvalidResources();
        if (bad != null)
        {
            General.Dialogs.ShowMessage("At least one resource doesn't exist in \"" + bad.Name + "\" game configuration!", "TheDoomBuilder",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1);
            tabs.SelectedIndex = ResourcesPage;
            configs.SelectedIndex = model.Entries.ToList().IndexOf(bad);
            return;
        }

        model.Apply();
        Close(true);
        await System.Threading.Tasks.Task.CompletedTask;
    }
}
