using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.GZBuilder.Data;
using Loc = CodeImp.DoomBuilder.Localization.Localizer;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The two toolbar drop-downs that list live data of the open map: the things filter ("Any action"... is a linedef color
/// preset, "(show all)" a things filter) and the linedef color presets (MainForm.UpdateThingsFilters / UpdateLinedefColorPresets).
/// </summary>
internal sealed class FilterDropdowns
{
    private sealed class Dropdown
    {
        public Button Button;
        public TextBlock Caption;
        public MenuFlyout Flyout;
        public string Signature;
    }

    private readonly Dropdown filters = new Dropdown();
    private readonly Dropdown presets = new Dropdown();

    public Control CreateThingsFilters() => Build(filters, Loc.T("Things filter"));
    public Control CreateColorPresets() => Build(presets, Loc.T("Linedef color presets"));

    private static Control Build(Dropdown d, string tooltip)
    {
        d.Caption = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 150 };
        var arrow = new TextBlock { Text = "▾", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(d.Caption);
        content.Children.Add(arrow);

        d.Flyout = new MenuFlyout();
        d.Button = new Button { Content = content, Flyout = d.Flyout, Padding = new Thickness(6, 2), MinWidth = 100 };
        d.Button.Click += (s, e) => d.Flyout.ShowAt(d.Button);
        ToolTip.SetTip(d.Button, tooltip);
        return d.Button;
    }

    /// <summary>Re-reads the map's filters and presets; the lists are rebuilt only when something in them changed.</summary>
    public void Refresh()
    {
        RefreshFilters();
        RefreshPresets();
    }

    // ---- things filters

    private void RefreshFilters()
    {
        var map = General.Map;
        if (map == null)
        {
            filters.Caption.Text = Loc.T("(show all)");
            if (filters.Signature != null) { filters.Flyout.Items.Clear(); filters.Signature = null; }
            return;
        }

        ThingsFilter current = map.ThingsFilter;
        var list = new List<ThingsFilter>();
        list.Add(current is NullThingsFilter ? current : new NullThingsFilter());
        list.AddRange(map.ConfigSettings.ThingsFilters);

        string signature = string.Join("\n", list.Select(f => Decorated(f) + "|" + f.IsValid() + "|" + ReferenceEquals(f, current)));
        filters.Caption.Text = current != null ? Loc.T(current.Name) : Loc.T("(show all)");
        if (signature == filters.Signature) return;
        filters.Signature = signature;

        filters.Flyout.Items.Clear();
        foreach (ThingsFilter f in list)
        {
            ThingsFilter filter = f;
            var item = new MenuItem { Header = Loc.T(Decorated(filter)), ToggleType = MenuItemToggleType.Radio, IsChecked = ReferenceEquals(filter, current), GroupName = "thingsfilter" };
            if (!(filter is NullThingsFilter) && !filter.IsValid() && ImageCache.Get("Warning") is { } warning)
                item.Icon = new Image { Source = warning, Width = 16, Height = 16 };
            item.Click += (s, e) =>
            {
                if (General.Map == null) return;
                General.Map.ChangeThingFilter(filter);
                RefreshFilters();
            };
            filters.Flyout.Items.Add(item);
        }
    }

    // "name", "!name" for an inverted filter and a [2D]/[3D] suffix for the ones that only apply to one kind of mode
    private static string Decorated(ThingsFilter f)
    {
        string name = f.Name;
        if (f.Invert) name = "!" + name;
        switch (f.DisplayMode)
        {
            case ThingsFilterDisplayMode.CLASSIC_MODES_ONLY: name += " [2D]"; break;
            case ThingsFilterDisplayMode.VISUAL_MODES_ONLY: name += " [3D]"; break;
        }
        return name;
    }

    // ---- linedef color presets

    private void RefreshPresets()
    {
        var map = General.Map;
        if (map == null)
        {
            presets.Caption.Text = Loc.T("No active presets");
            if (presets.Signature != null) { presets.Flyout.Items.Clear(); presets.Signature = null; }
            return;
        }

        LinedefColorPreset[] all = map.ConfigSettings.LinedefColorPresets ?? Array.Empty<LinedefColorPreset>();
        presets.Caption.Text = ActiveNames(all);

        string signature = string.Join("\n", all.Select(p => p.Name + "|" + p.Color.ToInt() + "|" + p.Enabled + "|" + p.IsValid()));
        if (signature == presets.Signature) return;
        presets.Signature = signature;

        presets.Flyout.Items.Clear();
        foreach (LinedefColorPreset p in all)
        {
            LinedefColorPreset preset = p;
            var item = new MenuItem { Header = Loc.T(preset.Name), ToggleType = MenuItemToggleType.CheckBox, IsChecked = preset.Enabled };
            ToolTip.SetTip(item, Loc.T("Hold Shift to toggle several items at once"));
            item.Icon = preset.IsValid()
                ? new Border
                {
                    Width = 12, Height = 10,
                    Background = new SolidColorBrush(Color.FromRgb(preset.Color.r, preset.Color.g, preset.Color.b)),
                    BorderBrush = Brushes.Black, BorderThickness = new Thickness(1),
                }
                : (ImageCache.Get("Warning") is { } warning ? new Image { Source = warning, Width = 16, Height = 16 } : null);
            item.Click += (s, e) => TogglePreset(preset);
            presets.Flyout.Items.Add(item);
        }
    }

    private void TogglePreset(LinedefColorPreset preset)
    {
        var map = General.Map;
        if (map == null) return;

        preset.Enabled = !preset.Enabled;
        map.Map.UpdateCustomLinedefColors();
        map.ConfigSettings.Changed = true;
        if (General.Editing.Mode is ClassicMode) General.Interface.RedrawDisplay();

        presets.Signature = null;        // the check marks follow the settings
        RefreshPresets();
    }

    // The names of the active presets, or how many there are when they do not fit
    private static string ActiveNames(LinedefColorPreset[] all)
    {
        List<string> names = all.Where(p => p.Enabled).Select(p => Loc.T(p.Name)).ToList();
        if (names.Count == 0) return Loc.T("No active presets");
        string text = string.Join(", ", names);
        if (text.Length <= 18) return text;
        return names.Count + (names.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).EndsWith("1") ? " preset" : " presets") + " active";
    }
}
