using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;

namespace DoomBuilder.App.Shell;

/// <summary>
/// Builds the main window's menu bar and toolbar from the UDB layout (<see cref="UiModel"/>) and keeps them in step with the
/// editor: visibility, enabled and checked state from <see cref="UiRules"/>, shortcut texts from the action bindings.
/// </summary>
public sealed class ShellUi
{
    private sealed class Bound
    {
        public UiItem Item;
        public Control Control;
        public AccessText Caption;      // the visible text (menus)
        public TextBlock ShortcutText;  // the shortcut next to it (menus)
        public MenuItem MenuItem;
        public ToggleButton Button;
    }

    private readonly ShellCommands commands;
    private readonly List<MenuItem> recentitems = new List<MenuItem>();
    private readonly List<Bound> bound = new List<Bound>();
    private readonly Dictionary<string, Bound> byName = new Dictionary<string, Bound>();
    private readonly Dictionary<Control, ItemsControl> menuOwner = new Dictionary<Control, ItemsControl>();   // separators and items of the menus
    private WrapPanel toolbarPanel;
    private bool refreshing;

    public Menu Menu { get; }
    public Control Toolbar { get; }

    /// <summary>The row of edit mode buttons (filled by the plugins through AddEditModeButton).</summary>
    public WrapPanel ModesPanel { get; } = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 1) };
    public Control ModesBar { get; }

    /// <summary>The row of the controls of the active mode (the options of the drawing modes...).</summary>
    public WrapPanel ModeControlsPanel { get; } = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 1) };
    public Control ModeControlsBar { get; }

    public ShellUi(ShellCommands commands)
    {
        this.commands = commands;

        var model = UiModel.Load();
        Menu = BuildMenu(model["menumain"]);
        Toolbar = BuildToolbar(model["toolbar"]);
        ModesBar = new Border { Child = ModesPanel, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray, IsVisible = false };
        ModeControlsBar = new Border { Child = ModeControlsPanel, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray, IsVisible = false };
        Refresh();
    }

    /// <summary>Every item the shell built, by its designer name (for tests and plugins that add to it).</summary>
    public IEnumerable<(UiItem item, Control control)> Items
    {
        get { foreach (Bound b in bound) yield return (b.Item, b.Control); }
    }

    // ---- menu

    private Menu BuildMenu(UiStrip strip)
    {
        var menu = new Menu();
        foreach (UiItem item in strip.Items)
        {
            if (item.IsSeparator) continue;
            Control built = BuildMenuItem(item);
            menuOwner[built] = menu;
            menu.Items.Add(built);
        }
        return menu;
    }

    private Control BuildMenuItem(UiItem item)
    {
        if (item.IsSeparator)
        {
            var separator = new Separator();
            Register(new Bound { Item = item, Control = separator });
            return separator;
        }

        var caption = new AccessText { Text = AccessKeyText(item.Text), VerticalAlignment = VerticalAlignment.Center };
        var shortcut = new TextBlock { Opacity = 0.6, Margin = new Thickness(28, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(shortcut, Dock.Right);
        header.Children.Add(shortcut);
        header.Children.Add(caption);

        var menuitem = new MenuItem { Header = header };

        if (ImageCache.Get(item.Image) is { } bitmap)
            menuitem.Icon = new Image { Source = bitmap, Width = 16, Height = 16 };

        foreach (UiItem child in item.Items)
        {
            Control built = BuildMenuItem(child);
            menuOwner[built] = menuitem;
            menuitem.Items.Add(built);
        }

        if (item.Items.Count == 0)
        {
            // Checks are shown but driven by the editor's settings, never toggled by the click itself (see Refresh)
            menuitem.Click += (s, e) => Run(item);
        }

        Register(new Bound { Item = item, Control = menuitem, MenuItem = menuitem, Caption = caption, ShortcutText = shortcut });
        return menuitem;
    }

    // "&File" -> "_File", and a literal underscore must be doubled in Avalonia
    internal static string AccessKeyText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string escaped = text.Replace("_", "__");
        int amp = escaped.IndexOf('&');
        return amp >= 0 && amp < escaped.Length - 1 ? escaped.Remove(amp, 1).Insert(amp, "_") : escaped.Replace("&", string.Empty);
    }

    // ---- toolbar

    private Control BuildToolbar(UiStrip strip)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2) };
        toolbarPanel = panel;
        foreach (UiItem item in strip.Items)
            if (BuildToolbarItem(item) is { } control) panel.Children.Add(control);

        return new Border { Child = panel, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray, Padding = new Thickness(0, 0, 0, 2) };
    }

    private Control BuildToolbarItem(UiItem item)
    {
        if (item.IsSeparator)
        {
            var line = new Border { Width = 1, Height = 20, Margin = new Thickness(5, 2), Background = Brushes.Gray, Opacity = 0.5 };
            Register(new Bound { Item = item, Control = line });
            return line;
        }

        // Split/drop-down buttons list their entries in a flyout; the ones fed from live data (things filters, line color
        // presets) are built when those lists are ported.
        if (item.Type == "dropdown" || item.Type == "split")
        {
            if (item.Items.Count == 0) return null;
            var flyout = new MenuFlyout();
            foreach (UiItem child in item.Items)
                if (!child.IsSeparator) flyout.Items.Add(BuildMenuItem(child));

            var drop = new ToggleButton { Content = ContentOf(item), Flyout = flyout, Padding = new Thickness(4) };
            drop.Click += (s, e) => { drop.IsChecked = false; flyout.ShowAt(drop); };
            ToolTip.SetTip(drop, item.Tooltip ?? item.PlainText);
            Register(new Bound { Item = item, Control = drop, Button = drop });
            return drop;
        }

        var button = new ToggleButton { Content = ContentOf(item), Padding = new Thickness(4), Margin = new Thickness(1) };
        ToolTip.SetTip(button, item.Tooltip ?? item.PlainText);
        button.Click += (s, e) => Run(item);
        Register(new Bound { Item = item, Control = button, Button = button });
        return button;
    }

    private static object ContentOf(UiItem item)
    {
        if (ImageCache.Get(item.Image) is { } bitmap) return new Image { Source = bitmap, Width = 16, Height = 16 };
        return item.PlainText;
    }

    private void Register(Bound b)
    {
        bound.Add(b);
        if (!string.IsNullOrEmpty(b.Item.Name)) byName[b.Item.Name] = b;
    }

    // ---- places the plugins add to (UDB's MenuSection / ToolbarSection)

    // Where each menu section is: the menu and the separator that ends the section (null: at the end of the menu)
    private static readonly Dictionary<CodeImp.DoomBuilder.Windows.MenuSection, (string menu, string anchor)> MenuPlaces =
        new Dictionary<CodeImp.DoomBuilder.Windows.MenuSection, (string, string)>
        {
            [CodeImp.DoomBuilder.Windows.MenuSection.FileNewOpenClose] = ("menufile", "seperatorfileopen"),
            [CodeImp.DoomBuilder.Windows.MenuSection.FileSave] = ("menufile", "seperatorfilesave"),
            [CodeImp.DoomBuilder.Windows.MenuSection.FileImport] = ("itemimport", null),
            [CodeImp.DoomBuilder.Windows.MenuSection.FileExport] = ("itemexport", null),
            [CodeImp.DoomBuilder.Windows.MenuSection.FileRecent] = ("menufile", "seperatorfilerecent"),
            [CodeImp.DoomBuilder.Windows.MenuSection.FileExit] = ("menufile", "itemexit"),
            [CodeImp.DoomBuilder.Windows.MenuSection.EditUndoRedo] = ("menuedit", "seperatoreditundo"),
            [CodeImp.DoomBuilder.Windows.MenuSection.EditCopyPaste] = ("menuedit", "seperatoreditcopypaste"),
            [CodeImp.DoomBuilder.Windows.MenuSection.EditGeometry] = ("menuedit", "seperatoreditgeometry"),
            [CodeImp.DoomBuilder.Windows.MenuSection.EditGrid] = ("menuedit", "seperatoreditgrid"),
            [CodeImp.DoomBuilder.Windows.MenuSection.EditMapOptions] = ("menuedit", null),
            [CodeImp.DoomBuilder.Windows.MenuSection.ViewHelpers] = ("menuview", "separatorhelpers"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ViewRendering] = ("menuview", "separatorrendering"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ViewThings] = ("menuview", "seperatorviewthings"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ViewViews] = ("menuview", "seperatorviewviews"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ViewZoom] = ("menuview", "seperatorviewzoom"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ViewScriptEdit] = ("menuview", null),
            [CodeImp.DoomBuilder.Windows.MenuSection.PrefabsInsert] = ("menuprefabs", "seperatorprefabsinsert"),
            [CodeImp.DoomBuilder.Windows.MenuSection.PrefabsCreate] = ("menuprefabs", null),
            [CodeImp.DoomBuilder.Windows.MenuSection.ToolsResources] = ("menutools", "seperatortoolsresources"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ToolsConfiguration] = ("menutools", "seperatortoolsconfig"),
            [CodeImp.DoomBuilder.Windows.MenuSection.ToolsTesting] = ("menutools", null),
            [CodeImp.DoomBuilder.Windows.MenuSection.HelpManual] = ("menuhelp", "seperatorhelpmanual"),
            [CodeImp.DoomBuilder.Windows.MenuSection.HelpAbout] = ("menuhelp", null),
        };

    // The item of the main toolbar that a section's buttons go in front of
    private static readonly Dictionary<CodeImp.DoomBuilder.Windows.ToolbarSection, string> ToolbarAnchors =
        new Dictionary<CodeImp.DoomBuilder.Windows.ToolbarSection, string>
        {
            [CodeImp.DoomBuilder.Windows.ToolbarSection.File] = "seperatorfile",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Script] = "seperatorscript",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.UndoRedo] = "seperatorundo",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.CopyPaste] = "seperatorcopypaste",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Prefabs] = "seperatorprefabs",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Things] = "buttonviewnormal",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Views] = "seperatorviews",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Geometry] = "seperatorgeometry",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Helpers] = "separatorgzmodes",
            [CodeImp.DoomBuilder.Windows.ToolbarSection.Testing] = "seperatortesting",
        };

    /// <summary>A menu of the main menu bar by its designer name (menufile, menuedit, menumode...), or null.</summary>
    public MenuItem FindMenu(string name) => byName.TryGetValue(name, out Bound b) ? b.MenuItem : null;

    /// <summary>Adds an entry to a section of a menu, ahead of the separator that closes the section.</summary>
    public bool InsertInMenu(CodeImp.DoomBuilder.Windows.MenuSection section, Control control)
    {
        if (section == CodeImp.DoomBuilder.Windows.MenuSection.Top)
        {
            int at = byName.TryGetValue("menutools", out Bound tools) ? Menu.Items.IndexOf(tools.Control) : -1;
            Menu.Items.Insert(at >= 0 ? at : Menu.Items.Count, control);
            menuOwner[control] = Menu;
            return true;
        }

        if (!MenuPlaces.TryGetValue(section, out var place) || !byName.TryGetValue(place.menu, out Bound menu) || menu.MenuItem == null) return false;
        var items = menu.MenuItem.Items;
        int index = items.Count;
        if (place.anchor != null && byName.TryGetValue(place.anchor, out Bound anchor) && items.IndexOf(anchor.Control) is int found && found >= 0) index = found;
        items.Insert(index, control);
        menuOwner[control] = menu.MenuItem;
        return true;
    }

    /// <summary>Adds an entry at the end of one of the menus (the Mode menu's own entries), by the menu's name.</summary>
    public void AddToMenu(string menuName, Control control, int index = -1)
    {
        MenuItem menu = FindMenu(menuName);
        if (menu == null) return;
        menu.Items.Insert(index < 0 || index > menu.Items.Count ? menu.Items.Count : index, control);
        menuOwner[control] = menu;
    }

    /// <summary>Takes an entry the plugins added out of whichever menu holds it.</summary>
    public void RemoveFromMenus(Control control)
    {
        if (!menuOwner.TryGetValue(control, out ItemsControl owner)) return;
        (owner as ItemsControl)?.Items.Remove(control);
        menuOwner.Remove(control);
    }

    /// <summary>Adds a button to a section of the main toolbar (ahead of the item that ends the section).</summary>
    public bool InsertInToolbar(CodeImp.DoomBuilder.Windows.ToolbarSection section, Control control)
    {
        if (toolbarPanel == null || !ToolbarAnchors.TryGetValue(section, out string anchorName)) return false;
        int index = byName.TryGetValue(anchorName, out Bound anchor) ? toolbarPanel.Children.IndexOf(anchor.Control) : -1;
        toolbarPanel.Children.Insert(index >= 0 ? index : toolbarPanel.Children.Count, control);
        return true;
    }

    public void RemoveFromToolbar(Control control) => toolbarPanel?.Children.Remove(control);

    // ---- recent files

    /// <summary>Fills the File menu's recent files section (above the "no recent files" placeholder).</summary>
    /// <param name="open">Called with the path of the clicked file.</param>
    public void SetRecentFiles(IReadOnlyList<string> files, Action<string> open)
    {
        Bound placeholder = bound.Find(b => b.Item.Name == "itemnorecent");
        if (placeholder?.MenuItem == null) return;

        var parent = placeholder.MenuItem.Parent as ItemsControl;
        if (parent == null) return;

        foreach (MenuItem old in recentitems) parent.Items.Remove(old);
        recentitems.Clear();

        int at = parent.Items.IndexOf(placeholder.MenuItem);
        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i];
            var item = new MenuItem { Header = new AccessText { Text = "_" + (i + 1) + "  " + CodeImp.DoomBuilder.Windows.RecentFiles.MenuText(path) }, Tag = path };
            ToolTip.SetTip(item, path);
            item.Click += (s, e) => open(path);
            parent.Items.Insert(at + i, item);
            recentitems.Add(item);
        }

        placeholder.Control.IsVisible = files.Count == 0;
    }

    // ---- behavior

    private void Run(UiItem item)
    {
        commands.Invoke(item);
        Refresh();   // the command may have changed what is enabled/checked (and toggles flipped themselves)
    }

    /// <summary>Re-reads the editor's state: call after anything that changes what the menus show.</summary>
    public void Refresh()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            foreach (Bound b in bound)
            {
                ItemState? state = UiRules.For(b.Item);

                if (state is { } s)
                {
                    b.Control.IsVisible = s.Visible;
                    b.Control.IsEnabled = s.Enabled;
                    if (b.MenuItem != null)
                    {
                        // Only items that are checkable get a check mark
                        if (b.Item.CheckOnClick || s.Checked) { b.MenuItem.ToggleType = MenuItemToggleType.CheckBox; b.MenuItem.IsChecked = s.Checked; }
                        if (s.Text != null && b.Caption != null) b.Caption.Text = AccessKeyText(s.Text);
                    }
                    if (b.Button != null)
                    {
                        b.Button.IsChecked = s.Checked;
                        if (s.Text != null) ToolTip.SetTip(b.Button, s.Text);
                    }
                }
                else if (b.Button != null)
                {
                    b.Button.IsChecked = false;   // a ToggleButton flips itself when clicked; without a rule it is a plain button
                }

                if (b.ShortcutText != null) b.ShortcutText.Text = ShortcutOf(b.Item);
            }
        }
        finally { refreshing = false; }
    }

    // The key bound to the item's action, as the user sees it in the shortcuts list
    private static string ShortcutOf(UiItem item)
    {
        if (!item.InvokesAction || General.Actions == null || !General.Actions.Exists(item.Action)) return string.Empty;
        int key = General.Actions.GetActionByName(item.Action).ShortcutKey;
        return key == 0 ? string.Empty : CodeImp.DoomBuilder.Actions.Action.GetShortcutKeyDesc(key);
    }
}
