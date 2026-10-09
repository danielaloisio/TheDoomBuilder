using DoomBuilder.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeImp.DoomBuilder.Controls;
using Loc = CodeImp.DoomBuilder.Localization.Localizer;
using AvControl = Avalonia.Controls.Control;
using AvButton = Avalonia.Controls.Button;
using AvComboBox = Avalonia.Controls.ComboBox;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvMenu = Avalonia.Controls.MenuItem;

namespace DoomBuilder.App.Shell;

/// <summary>
/// Draws the toolbar and menu items that plugins hand over (the Core's <see cref="ToolStripItem"/> objects) as Avalonia
/// controls, and keeps them in step: a change of text, image, enabled, visible, checked or value on the item shows on the
/// control, and a click or an edit on the control reaches the item.
/// </summary>
public sealed class ToolStripBinder
{
    private sealed class Binding
    {
        public ToolStripItem Item;
        public AvControl Control;
        public Func<bool> Gate = () => true;       // an extra condition for being shown (the section's setting)
        public Action Update;
        public EventHandler Handler;
    }

    private readonly List<Binding> bindings = new List<Binding>();

    /// <summary>Runs a click of an item that carries an action name or a mode (what UDB's InvokeTaggedAction did).</summary>
    public Action<ToolStripItem> Clicked { get; set; }

    // ---- toolbar items

    /// <summary>The control for a toolbar item. <paramref name="gate"/> adds a condition for the item to be visible.</summary>
    public AvControl CreateToolbarControl(ToolStripItem item, Func<bool> gate = null)
    {
        AvControl control;
        Action update;

        switch (item)
        {
            case ToolStripSeparator _:
                control = new Border { Width = 1, Height = 20, Margin = new Thickness(5, 2), Background = Brushes.Gray, Opacity = 0.5 };
                update = () => { };
                break;

            case ToolStripLabel label:
            {
                var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) };
                control = text;
                update = () => text.Text = Loc.T(label.Text);
                break;
            }

            case ToolStripNumericUpDown number:
            {
                var box = new NumericUpDown { Minimum = number.Minimum, Maximum = number.Maximum, Increment = number.Increment, Width = 90, FormatString = "0", Margin = new Thickness(2, 0) };
                bool syncing = false;
                box.ValueChanged += (s, e) => { if (!syncing && box.Value.HasValue) number.Value = box.Value.Value; };
                control = box;
                update = () =>
                {
                    syncing = true;
                    box.Minimum = number.Minimum;
                    box.Maximum = number.Maximum;
                    box.Increment = number.Increment;
                    box.Value = number.Value;
                    syncing = false;
                };
                break;
            }

            case ToolStripCheckBox check:
            {
                var box = new AvCheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) };
                bool syncing = false;
                box.IsCheckedChanged += (s, e) => { if (!syncing) check.Checked = box.IsChecked == true; };
                control = box;
                update = () => { syncing = true; box.IsChecked = check.Checked; box.Content = Loc.T(check.Text); syncing = false; };
                break;
            }

            case ToolStripComboBox combo:
            {
                var box = new AvComboBox { MinWidth = 140, Margin = new Thickness(2, 0), VerticalAlignment = VerticalAlignment.Center };
                bool syncing = false;
                box.SelectionChanged += (s, e) => { if (!syncing && box.SelectedIndex >= 0) combo.SelectedIndex = box.SelectedIndex; };
                control = box;
                update = () =>
                {
                    syncing = true;
                    box.ItemsSource = combo.Items.Select(o => o.ToString()).ToList();
                    box.SelectedIndex = combo.SelectedIndex;
                    syncing = false;
                };
                break;
            }

            case ToolStripMenuItem menu when menu.DropDownItems.Count > 0:
            case ToolStripDropDownButton _:     // (split buttons are drop-down buttons)
            {
                var children = item is ToolStripMenuItem mi ? mi.DropDownItems : ((ToolStripDropDownButton)item).DropDownItems;
                var flyout = new MenuFlyout();
                var button = new ToggleButton { Flyout = flyout, Padding = new Thickness(4), Margin = new Thickness(1) };
                button.Click += (s, e) =>
                {
                    button.IsChecked = false;
                    (item as ToolStripMenuItem)?.RaiseDropDownOpening();
                    flyout.Items.Clear();
                    foreach (ToolStripItem child in children) flyout.Items.Add(CreateMenuControl(child));
                    flyout.ShowAt(button);
                };
                control = button;
                update = () => FillButton(button, item);
                break;
            }

            default:     // a plain or checkable button, including the ones tied to an action
            {
                var button = new ToggleButton { Padding = new Thickness(4), Margin = new Thickness(1) };
                button.Click += (s, e) => Click(item, button);
                control = button;
                update = () => FillButton(button, item);
                break;
            }
        }

        return Bind(item, control, update, gate);
    }

    private void Click(ToolStripItem item, ToggleButton button)
    {
        item.PerformClick();
        Clicked?.Invoke(item);
        if (item is ToolStripButton b) button.IsChecked = b.Checked;    // a toggle button flips itself; the item decides
        else button.IsChecked = false;
    }

    private static void FillButton(ToggleButton button, ToolStripItem item)
    {
        Avalonia.Media.Imaging.Bitmap image = ImageConvert.ToAvalonia(item.Image);
        bool showtext = item.DisplayStyle == ToolStripItemDisplayStyle.Text || item.DisplayStyle == ToolStripItemDisplayStyle.ImageAndText || image == null;
        if (image != null && item.DisplayStyle != ToolStripItemDisplayStyle.Text)
        {
            var picture = new Avalonia.Controls.Image { Source = image, Width = 16, Height = 16 };
            if (showtext)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                row.Children.Add(picture);
                row.Children.Add(new TextBlock { Text = Loc.T(item.Text), VerticalAlignment = VerticalAlignment.Center });
                button.Content = row;
            }
            else button.Content = picture;
        }
        else button.Content = Loc.T(item.Text);

        if (item is ToolStripActionButton action) action.UpdateToolTip();
        ToolTip.SetTip(button, Loc.T(string.IsNullOrEmpty(item.ToolTipText) ? item.Text : item.ToolTipText));
        if (item is ToolStripButton b) button.IsChecked = b.Checked;
    }

    // ---- menu items

    /// <summary>The control for a menu entry; menus nest through their DropDownItems.</summary>
    public AvControl CreateMenuControl(ToolStripItem item, Func<bool> gate = null)
    {
        if (item is ToolStripSeparator)
            return Bind(item, new Separator(), () => { }, gate);

        var caption = new AccessText { VerticalAlignment = VerticalAlignment.Center };
        var shortcut = new TextBlock { Opacity = 0.6, Margin = new Thickness(28, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(shortcut, Dock.Right);
        header.Children.Add(shortcut);
        header.Children.Add(caption);

        var menuitem = new AvMenu { Header = header };
        var children = (item as ToolStripMenuItem)?.DropDownItems;
        if (children != null)
        {
            foreach (ToolStripItem child in children) menuitem.Items.Add(CreateMenuControl(child));
            // UDB's menus fill themselves when opened (the things in the map, for example)
            menuitem.SubmenuOpened += (s, e) => (item as ToolStripMenuItem)?.RaiseDropDownOpening();
        }
        if (children == null || children.Count == 0)
            menuitem.Click += (s, e) => { item.PerformClick(); Clicked?.Invoke(item); };

        return Bind(item, menuitem, () =>
        {
            caption.Text = ShellUi.AccessKeyText(Loc.T(item.Text));
            shortcut.Text = item.ShortcutKeyDisplayString ?? string.Empty;
            Avalonia.Media.Imaging.Bitmap image = ImageConvert.ToAvalonia(item.Image);
            menuitem.Icon = image == null ? null : new Avalonia.Controls.Image { Source = image, Width = 16, Height = 16 };
            if (item is ToolStripMenuItem m && (m.CheckOnClick || m.Checked)) { menuitem.ToggleType = MenuItemToggleType.CheckBox; menuitem.IsChecked = m.Checked; }
        }, gate);
    }

    // ---- binding

    private AvControl Bind(ToolStripItem item, AvControl control, Action update, Func<bool> gate)
    {
        var binding = new Binding { Item = item, Control = control, Gate = gate ?? (() => true) };
        binding.Update = () =>
        {
            update();
            control.IsEnabled = item.Enabled;
            control.IsVisible = item.Visible && binding.Gate();
        };
        binding.Handler = (s, e) =>
        {
            if (Dispatcher.UIThread.CheckAccess()) binding.Update(); else Dispatcher.UIThread.Post(binding.Update);
        };
        item.StateChanged += binding.Handler;
        bindings.Add(binding);
        binding.Update();
        return control;
    }

    /// <summary>Stops following an item (when its control is removed).</summary>
    public void Unbind(ToolStripItem item)
    {
        foreach (Binding b in bindings.Where(b => b.Item == item).ToList())
        {
            item.StateChanged -= b.Handler;
            bindings.Remove(b);
        }
    }

    /// <summary>Re-evaluates every item: the extra conditions (settings, map open) may have changed.</summary>
    public void Refresh()
    {
        foreach (Binding b in bindings.ToList()) b.Update();
    }

    /// <summary>The control that shows an item, if it is bound.</summary>
    public AvControl ControlOf(ToolStripItem item) => bindings.FirstOrDefault(b => b.Item == item)?.Control;
}
