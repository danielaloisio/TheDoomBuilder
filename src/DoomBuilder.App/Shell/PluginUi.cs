using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia.Controls;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Windows;
using AvControl = Avalonia.Controls.Control;
using AvMenu = Avalonia.Controls.MenuItem;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The menus and toolbar buttons that plugins add while they run (General.Interface.AddMenu, AddButton, AddEditModeButton...):
/// where they go (the sections UDB's MainForm defined), what they look like (<see cref="ToolStripBinder"/>) and when they show.
/// </summary>
internal sealed class PluginUi
{
    private sealed class Placement
    {
        public ToolStripItem Item;
        public AvControl Control;
        public bool InMenu;
        public ToolbarSection Section;
        public bool InModesBar, InControlsBar, InMainBar;
    }

    private readonly ShellUi ui;
    private readonly ToolStripBinder binder = new ToolStripBinder();
    private readonly List<Placement> placements = new List<Placement>();
    private readonly List<ToolStripItem> editmodeitems = new List<ToolStripItem>();
    private readonly Dictionary<string, AvControl> modeMenuGroups = new Dictionary<string, AvControl>();
    private readonly Dictionary<string, AvControl> modeBarGroups = new Dictionary<string, AvControl>();

    public PluginUi(ShellUi ui)
    {
        this.ui = ui;
    }

    /// <summary>The items now on the toolbars and menus, for tests.</summary>
    public IEnumerable<ToolStripItem> Items => placements.Select(p => p.Item).Concat(editmodeitems);

    /// <summary>The control that shows an item.</summary>
    public AvControl ControlOf(ToolStripItem item) => binder.ControlOf(item);

    // ---- tags: short action names become full ones (plugin_action), as UDB did on the way in

    private static void RenameTags(ToolStripItem item, string plugin)
    {
        if (item.Tag is string tag && !tag.StartsWith(plugin + "_", StringComparison.OrdinalIgnoreCase))
            item.Tag = plugin.ToLowerInvariant() + "_" + tag;

        if (item is ToolStripMenuItem menu)
            foreach (ToolStripItem child in menu.DropDownItems) RenameTags(child, plugin);
    }

    // ---- menus

    public void AddMenu(ToolStripItem item, MenuSection section, string plugin)
    {
        RenameTags(item, plugin);
        AvControl control = binder.CreateMenuControl(item);
        if (ui.InsertInMenu(section, control)) placements.Add(new Placement { Item = item, Control = control, InMenu = true });
        else binder.Unbind(item);
        ApplyShortcutKeys();
    }

    /// <summary>An entry of the Mode menu, below the separator of a group of modes.</summary>
    public void AddModesMenu(ToolStripItem item, string group, string plugin)
    {
        RenameTags(item, plugin);
        AvControl control = binder.CreateMenuControl(item);
        AvMenu menu = ui.FindMenu("menumode");
        if (menu == null) { binder.Unbind(item); return; }

        int at = modeMenuGroups.TryGetValue(group, out AvControl separator) ? menu.Items.IndexOf(separator) + 1 : menu.Items.Count;
        ui.AddToMenu("menumode", control, at);
        placements.Add(new Placement { Item = item, Control = control, InMenu = true });
        ApplyShortcutKeys();
    }

    public void RemoveMenu(ToolStripItem item)
    {
        foreach (Placement p in placements.Where(p => p.Item == item && p.InMenu).ToList())
        {
            ui.RemoveFromMenus(p.Control);
            placements.Remove(p);
        }
        binder.Unbind(item);
    }

    // ---- toolbar buttons

    public void AddButton(ToolStripItem item, ToolbarSection section, string plugin)
    {
        RenameTags(item, plugin);
        AvControl control = binder.CreateToolbarControl(item, () => SectionShown(section));
        var placement = new Placement { Item = item, Control = control, Section = section };

        switch (section)
        {
            case ToolbarSection.Modes: ui.ModesPanel.Children.Add(control); placement.InModesBar = true; break;
            case ToolbarSection.Custom: ui.ModeControlsPanel.Children.Add(control); placement.InControlsBar = true; break;
            default:
                if (!ui.InsertInToolbar(section, control)) { binder.Unbind(item); return; }
                placement.InMainBar = true;
                break;
        }
        placements.Add(placement);
        Refresh();
    }

    /// <summary>A button of the modes bar, below the separator of a group of modes.</summary>
    public void AddModesButton(ToolStripItem item, string group, string plugin)
    {
        RenameTags(item, plugin);
        AvControl control = binder.CreateToolbarControl(item);
        int at = modeBarGroups.TryGetValue(group, out AvControl separator) ? ui.ModesPanel.Children.IndexOf(separator) + 1 : ui.ModesPanel.Children.Count;
        ui.ModesPanel.Children.Insert(at, control);
        placements.Add(new Placement { Item = item, Control = control, Section = ToolbarSection.Modes, InModesBar = true });
        Refresh();
    }

    public void RemoveButton(ToolStripItem item)
    {
        foreach (Placement p in placements.Where(p => p.Item == item && !p.InMenu).ToList())
        {
            if (p.InModesBar) ui.ModesPanel.Children.Remove(p.Control);
            else if (p.InControlsBar) ui.ModeControlsPanel.Children.Remove(p.Control);
            else ui.RemoveFromToolbar(p.Control);
            placements.Remove(p);
        }
        binder.Unbind(item);
        Refresh();
    }

    // Whether the user's settings show the toolbar section
    private static bool SectionShown(ToolbarSection section)
    {
        var s = General.Settings;
        switch (section)
        {
            case ToolbarSection.File: return s.ToolbarFile;
            case ToolbarSection.Script: return s.ToolbarScript;
            case ToolbarSection.UndoRedo: return s.ToolbarUndo;
            case ToolbarSection.CopyPaste: return s.ToolbarCopy;
            case ToolbarSection.Prefabs: return s.ToolbarPrefabs;
            case ToolbarSection.Things: return s.ToolbarFilter;
            case ToolbarSection.Views: return s.ToolbarViewModes;
            case ToolbarSection.Geometry: return s.ToolbarGeometry;
            case ToolbarSection.Testing: return s.ToolbarTesting;
            default: return true;
        }
    }

    // ---- edit modes: one button on the modes bar and one entry in the Mode menu for each

    public void AddEditModeSeparator(string group)
    {
        var bar = new ToolStripSeparator { Text = group };
        var menu = new ToolStripSeparator { Text = group };
        AvControl barControl = binder.CreateToolbarControl(bar);
        AvControl menuControl = binder.CreateMenuControl(menu);
        ui.ModesPanel.Children.Add(barControl);
        ui.AddToMenu("menumode", menuControl);
        modeBarGroups[group] = barControl;
        modeMenuGroups[group] = menuControl;
        editmodeitems.Add(bar);
        editmodeitems.Add(menu);
        Refresh();
    }

    public void AddEditModeButton(EditModeInfo modeinfo)
    {
        var button = new ToolStripActionButton(modeinfo.ButtonDesc, modeinfo.ButtonImage, EditModeButtonHandler) { DisplayStyle = ToolStripItemDisplayStyle.Image, Tag = modeinfo };
        button.UpdateToolTip();
        ui.ModesPanel.Children.Add(binder.CreateToolbarControl(button));
        editmodeitems.Add(button);

        var menu = new ToolStripMenuItem(modeinfo.ButtonDesc.Replace("&", "&&"), modeinfo.ButtonImage, EditModeButtonHandler) { Tag = modeinfo };
        ui.AddToMenu("menumode", binder.CreateMenuControl(menu));
        editmodeitems.Add(menu);

        ApplyShortcutKeys();
        Refresh();
    }

    private static void EditModeButtonHandler(object sender, EventArgs e)
    {
        if (sender is ToolStripItem { Tag: EditModeInfo mode })
            General.Actions.InvokeAction(mode.SwitchAction.GetFullActionName(mode.Plugin.Assembly));
    }

    public void RemoveEditModeButtons()
    {
        foreach (ToolStripItem item in editmodeitems)
        {
            AvControl control = binder.ControlOf(item);
            if (control != null)
            {
                ui.ModesPanel.Children.Remove(control);
                ui.RemoveFromMenus(control);
            }
            binder.Unbind(item);
        }
        editmodeitems.Clear();
        modeMenuGroups.Clear();
        modeBarGroups.Clear();
        Refresh();
    }

    /// <summary>Checks the button and the menu entry of the mode, and unchecks the others.</summary>
    public void CheckEditModeButton(string modeclassname)
    {
        foreach (ToolStripItem item in editmodeitems)
            if (item is ToolStripButton { Tag: EditModeInfo mode } button) button.Checked = mode.Type.Name == modeclassname;
    }

    // ---- shortcut texts and visibility

    /// <summary>Puts the keys of the actions on the menu entries (the ones the plugins added and the modes).</summary>
    public void ApplyShortcutKeys()
    {
        foreach (ToolStripItem item in Items.ToList()) ApplyShortcut(item);
    }

    private static void ApplyShortcut(ToolStripItem item)
    {
        if (item is ToolStripMenuItem menu)
        {
            string action = menu.Tag as string;
            if (action == null && menu.Tag is EditModeInfo mode) action = mode.SwitchAction.GetFullActionName(mode.Plugin.Assembly);
            if (action != null && General.Actions != null && General.Actions.Exists(action))
                menu.ShortcutKeyDisplayString = CodeImp.DoomBuilder.Actions.Action.GetShortcutKeyDesc(General.Actions[action].ShortcutKey);
            foreach (ToolStripItem child in menu.DropDownItems) ApplyShortcut(child);
        }
    }

    /// <summary>Re-evaluates what shows: the sections' settings, the bars (only with a map open), the separators.</summary>
    public void Refresh()
    {
        binder.Refresh();

        bool map = General.Map != null;
        ui.ModesBar.IsVisible = map && ui.ModesPanel.Children.Count > 0;
        ui.ModeControlsBar.IsVisible = map && ui.ModeControlsPanel.Children.Count > 0;

        // The edit modes bar is vertical (at the left of the display, like in UDB): its separators lie flat
        foreach (AvControl c in ui.ModesPanel.Children)
        {
            // local padding wins over the style of the bar: set it here so that the whole column fits the window
            if (c is Avalonia.Controls.Primitives.ToggleButton toggle && toggle.Padding != new Avalonia.Thickness(2)) toggle.Padding = new Avalonia.Thickness(2);
            if (c is Border b && b.Classes.Contains(ToolStripBinder.SeparatorClass) && b.Height != 1)
            {
                b.Width = 20;
                b.Height = 1;
                b.Margin = new Avalonia.Thickness(0, 4);
            }
        }
        HideRedundantSeparators(ui.ModesPanel.Children);
        HideRedundantSeparators(ui.ModeControlsPanel.Children);
        HideRedundantSeparators(ui.FindMenu("menumode")?.Items.OfType<AvControl>().ToList(), menu: true);
    }

    // A separator shows only between two visible entries: not first, not last, not next to another separator
    private static void HideRedundantSeparators(IEnumerable<AvControl> controls, bool menu = false)
    {
        if (controls == null) return;
        bool previousIsEntry = false;
        AvControl lastSeparator = null;
        foreach (AvControl c in controls.ToList())
        {
            bool separator = menu ? c is Separator : c is Border b && b.Classes.Contains(ToolStripBinder.SeparatorClass);
            if (separator)
            {
                c.IsVisible = previousIsEntry;
                if (previousIsEntry) lastSeparator = c;
                previousIsEntry = false;
            }
            else if (c.IsVisible)
            {
                previousIsEntry = true;
                lastSeparator = null;
            }
        }
        if (lastSeparator != null) lastSeparator.IsVisible = false;
    }
}
