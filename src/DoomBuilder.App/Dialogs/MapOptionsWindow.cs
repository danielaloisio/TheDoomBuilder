using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Windows;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// The dialog of the open map, new map, change map and map options commands: one layout over <see cref="IMapOptionsChoices"/>,
/// with a page-specific control on top (the maps in the WAD, or the level name).
/// </summary>
public sealed class MapOptionsWindow : Window
{
    private readonly IMapOptionsChoices choices;
    private readonly ComboBox config = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox compiler = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox strict = new CheckBox { Content = "Strict patches" };
    private readonly CheckBox longnames = new CheckBox { Content = "Use long texture names" };
    private readonly ResourcesEditor resources = new ResourcesEditor();
    private readonly Func<(string error, List<string> confirmations)> validate;
    private readonly Action apply;
    private bool loading;

    public Button OkButton { get; } = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
    public Button CancelButton { get; } = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };

    /// <summary>The game configuration selector (tests drive it).</summary>
    public ComboBox ConfigBox { get { return config; } }

    /// <summary>The resources editor (tests drive it).</summary>
    public ResourcesEditor ResourcesBox { get { return resources; } }

    /// <param name="pagecontrol">The control on top: map list, or level name.</param>
    /// <param name="validate">The reason the choices are not acceptable (null when fine) and the questions to ask first.</param>
    /// <param name="apply">Writes the choices into the options.</param>
    /// <param name="onconfigchanged">Called after the configuration changed, so the page can refresh (map list, name).</param>
    /// <param name="configchangeable">False when the game configuration cannot be changed (change map).</param>
    public MapOptionsWindow(string title, IMapOptionsChoices choices, Control pagecontrol, string startpath,
                            Func<(string error, List<string> confirmations)> validate, Action apply,
                            Action onconfigchanged, bool configchangeable = true, bool showresources = true)
    {
        this.choices = choices;
        this.validate = validate;
        this.apply = apply;

        Title = title;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16) };

        panel.Children.Add(Labeled("Game configuration", config));
        config.ItemsSource = choices.Configs.ToList();
        config.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ConfigurationInfo>((c, _) => new TextBlock { Text = c?.Name });
        config.SelectedItem = choices.SelectedConfig;
        config.IsEnabled = configchangeable;

        panel.Children.Add(pagecontrol);

        compiler.ItemsSource = choices.ScriptCompilers.Keys.ToList();
        compiler.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((key, _) => new TextBlock { Text = key != null && choices.ScriptCompilers.TryGetValue(key, out ScriptConfiguration sc) ? sc.Description : key });
        panel.Children.Add(Labeled("Script type", compiler));

        panel.Children.Add(strict);
        panel.Children.Add(longnames);

        if (showresources)
        {
            resources.StartPath = startpath;
            panel.Children.Add(resources);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(OkButton);
        buttons.Children.Add(CancelButton);
        panel.Children.Add(buttons);
        Content = panel;

        config.SelectionChanged += (s, e) =>
        {
            if (loading || config.SelectedItem is not ConfigurationInfo info) return;
            choices.SelectedConfig = info;
            onconfigchanged();
            LoadFromChoices();
        };
        compiler.SelectionChanged += (s, e) => { if (!loading && compiler.SelectedItem is string key) choices.SelectedScriptCompiler = key; };
        strict.IsCheckedChanged += (s, e) => { if (!loading) choices.StrictPatches = strict.IsChecked == true; };
        longnames.IsCheckedChanged += (s, e) => { if (!loading) choices.UseLongTextureNames = longnames.IsChecked == true; };

        OkButton.Click += (s, e) => Accept();
        CancelButton.Click += (s, e) => Close(false);

        LoadFromChoices();
    }

    // Brings every control in line with the model (after the model changed on its own)
    public void LoadFromChoices()
    {
        loading = true;
        try
        {
            compiler.IsEnabled = choices.ScriptCompilerAvailable;
            compiler.SelectedItem = choices.ScriptCompilerAvailable ? choices.SelectedScriptCompiler : null;
            strict.IsChecked = choices.StrictPatches;
            longnames.IsEnabled = choices.LongTextureNamesAvailable;
            longnames.IsChecked = choices.LongTextureNamesAvailable && choices.UseLongTextureNames;
            resources.Locations = choices.Resources;
            resources.SetFixed(choices.FixedResources);
        }
        finally { loading = false; }
    }

    private static Control Labeled(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(control);
        return panel;
    }

    private void Accept()
    {
        var (error, confirmations) = validate();
        if (error != null)
        {
            General.Dialogs.ShowMessage(error, "TheDoomBuilder", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            return;
        }

        foreach (string question in confirmations)
        {
            if (General.Dialogs.ShowMessage(question, "TheDoomBuilder", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
        }

        apply();
        Close(true);
    }
}
