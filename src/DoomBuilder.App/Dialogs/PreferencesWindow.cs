using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.App.Dialogs;

/// <summary>
/// The program preferences (UDB's PreferencesForm). The controls are built from the <see cref="Preference"/> items of the
/// <see cref="PreferencesModel"/>: a check box, a slider, a list, a folder or a color for each.
/// </summary>
public sealed class PreferencesWindow : Window
{
    private readonly PreferencesModel model = new PreferencesModel();
    private readonly TabControl tabs = new TabControl();
    private readonly Dictionary<string, Control> editors = new Dictionary<string, Control>();

    public Button OkButton { get; } = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
    public Button CancelButton { get; } = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };

    public bool ReloadResources { get; private set; }
    internal PreferencesModel Model { get { return model; } }
    public TabControl Tabs { get { return tabs; } }

    /// <summary>The control that edits a setting, by its key (tests drive these).</summary>
    public Control EditorOf(string key) => editors[key];

    public PreferencesWindow()
    {
        Title = "Preferences";
        Width = 720;
        Height = 620;
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        foreach (string tab in model.Tabs)
            tabs.Items.Add(new TabItem { Header = tab, Content = new ScrollViewer { Content = PageOf(tab) } });

        OkButton.Click += (s, e) => Accept();
        CancelButton.Click += (s, e) => Close(false);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(OkButton);
        buttons.Children.Add(CancelButton);

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(tabs);
        Content = root;
    }

    private Control PageOf(string tab)
    {
        var page = new StackPanel { Spacing = 6, Margin = new Thickness(14) };
        string group = null;
        foreach (Preference item in model.InTab(tab))
        {
            if (item.Group != group)
            {
                group = item.Group;
                page.Children.Add(new TextBlock { Text = group, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, page.Children.Count == 0 ? 0 : 12, 0, 2) });
            }
            page.Children.Add(RowOf(item));
        }
        return page;
    }

    private Control RowOf(Preference item)
    {
        switch (item.Kind)
        {
            case PreferenceKind.Bool:
            {
                var box = new CheckBox { Content = item.Label, IsChecked = (bool)item.Value };
                box.IsCheckedChanged += (s, e) => item.Value = box.IsChecked == true;
                editors[item.Key] = box;
                return box;
            }
            case PreferenceKind.Int:
            {
                var slider = new Slider { Minimum = item.Min, Maximum = item.Max, Value = (int)item.Value, TickFrequency = 1, IsSnapToTickEnabled = true, MinWidth = 220 };
                var value = new TextBlock { Text = item.ValueText, MinWidth = 120, VerticalAlignment = VerticalAlignment.Center };
                slider.PropertyChanged += (s, e) =>
                {
                    if (e.Property != Slider.ValueProperty) return;
                    item.Value = (int)Math.Round(slider.Value);
                    value.Text = item.ValueText;
                };
                editors[item.Key] = slider;
                return Labeled(item.Label, slider, value);
            }
            case PreferenceKind.Choice:
            {
                var combo = new ComboBox { ItemsSource = item.Choices, SelectedIndex = (int)item.Value, MinWidth = 220 };
                combo.SelectionChanged += (s, e) => { if (combo.SelectedIndex >= 0) item.Value = combo.SelectedIndex; };
                editors[item.Key] = combo;
                return Labeled(item.Label, combo);
            }
            case PreferenceKind.Path:
            {
                var text = new TextBox { Text = (string)item.Value, MinWidth = 320 };
                text.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) item.Value = text.Text ?? ""; };
                var browse = new Button { Content = "Browse..." };
                browse.Click += (s, e) => BrowseFolder(text);
                editors[item.Key] = text;
                return Labeled(item.Label, text, browse);
            }
            default:     // color: a swatch and the hex code (#RRGGBB)
            {
                var swatch = new Border { Width = 28, Height = 22, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Background = new SolidColorBrush(ColorOf((int)item.Value)) };
                var hex = new TextBox { Text = HexOf((int)item.Value), Width = 100, MaxLength = 7 };
                hex.PropertyChanged += (s, e) =>
                {
                    if (e.Property != TextBox.TextProperty || !TryParseHex(hex.Text, out int argb)) return;     // half typed codes are ignored
                    item.Value = argb;
                    swatch.Background = new SolidColorBrush(ColorOf(argb));
                };
                editors[item.Key] = hex;
                return Labeled(item.Label, swatch, hex);
            }
        }
    }

    /// <summary>"#RRGGBB" (the # is optional) as an opaque ARGB value.</summary>
    internal static bool TryParseHex(string text, out int argb)
    {
        argb = 0;
        string digits = (text ?? "").Trim().TrimStart('#');
        if (digits.Length != 6 || !int.TryParse(digits, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int rgb)) return false;
        argb = unchecked((int)0xFF000000) | rgb;
        return true;
    }

    internal static string HexOf(int argb) => "#" + (argb & 0xFFFFFF).ToString("X6");

    private static Color ColorOf(int argb) => Color.FromUInt32(unchecked((uint)argb));

    private static Control Labeled(string label, params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(new TextBlock { Text = label, MinWidth = 260, VerticalAlignment = VerticalAlignment.Center });
        foreach (Control c in controls) row.Children.Add(c);
        return row;
    }

    private void BrowseFolder(TextBox target)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;
        var folders = DialogPump.Run(() => storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select a folder" }));
        if (folders != null && folders.Count > 0 && folders[0].TryGetLocalPath() is string path) target.Text = path;
    }

    private void Accept()
    {
        string problem = model.Validate();
        if (problem != null)
        {
            General.Dialogs.ShowMessage(problem, "TheDoomBuilder", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error, System.Windows.Forms.MessageBoxDefaultButton.Button1);
            return;
        }

        ReloadResources = model.ReloadResources;
        model.Apply();
        Close(true);
    }
}
