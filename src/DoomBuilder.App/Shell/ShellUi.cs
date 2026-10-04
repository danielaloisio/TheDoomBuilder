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
    private bool refreshing;

    public Menu Menu { get; }
    public Control Toolbar { get; }

    public ShellUi(ShellCommands commands)
    {
        this.commands = commands;

        var model = UiModel.Load();
        Menu = BuildMenu(model["menumain"]);
        Toolbar = BuildToolbar(model["toolbar"]);
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
            menu.Items.Add(BuildMenuItem(item));
        }
        return menu;
    }

    private Control BuildMenuItem(UiItem item)
    {
        if (item.IsSeparator)
        {
            var separator = new Separator();
            bound.Add(new Bound { Item = item, Control = separator });
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

        foreach (UiItem child in item.Items) menuitem.Items.Add(BuildMenuItem(child));

        if (item.Items.Count == 0)
        {
            // Checks are shown but driven by the editor's settings, never toggled by the click itself (see Refresh)
            menuitem.Click += (s, e) => Run(item);
        }

        bound.Add(new Bound { Item = item, Control = menuitem, MenuItem = menuitem, Caption = caption, ShortcutText = shortcut });
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
        foreach (UiItem item in strip.Items)
            if (BuildToolbarItem(item) is { } control) panel.Children.Add(control);

        return new Border { Child = panel, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray, Padding = new Thickness(0, 0, 0, 2) };
    }

    private Control BuildToolbarItem(UiItem item)
    {
        if (item.IsSeparator)
        {
            var line = new Border { Width = 1, Height = 20, Margin = new Thickness(5, 2), Background = Brushes.Gray, Opacity = 0.5 };
            bound.Add(new Bound { Item = item, Control = line });
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
            bound.Add(new Bound { Item = item, Control = drop, Button = drop });
            return drop;
        }

        var button = new ToggleButton { Content = ContentOf(item), Padding = new Thickness(4), Margin = new Thickness(1) };
        ToolTip.SetTip(button, item.Tooltip ?? item.PlainText);
        button.Click += (s, e) => Run(item);
        bound.Add(new Bound { Item = item, Control = button, Button = button });
        return button;
    }

    private static object ContentOf(UiItem item)
    {
        if (ImageCache.Get(item.Image) is { } bitmap) return new Image { Source = bitmap, Width = 16, Height = 16 };
        return item.PlainText;
    }

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
