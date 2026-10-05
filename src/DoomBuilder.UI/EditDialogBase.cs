using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using CodeImp.DoomBuilder;

namespace DoomBuilder.UI;

/// <summary>
/// The frame of UDB's edit dialogs (vertex, linedef, sector, thing): tabs, OK and Cancel, and the remembered tab. Closing with
/// the window's close button or Escape means Cancel. Dialogs report the answer through <see cref="Window.Close(object)"/> (true = OK).
/// </summary>
public abstract class EditDialogBase : Window
{
    private readonly string settingname;
    protected readonly TabControl Tabs = new TabControl();
    protected readonly Button OkButton = new Button { Content = "OK", MinWidth = 80, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
    protected readonly Button CancelButton = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
    private bool answered;

    /// <summary>True when the user accepted.</summary>
    public bool Accepted { get; private set; }

    protected EditDialogBase(string settingname, string title, double width, double height)
    {
        this.settingname = settingname;
        Title = title;
        Width = width;
        Height = height;
        MinWidth = 360;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        OkButton.Click += (s, e) => Finish(true);
        CancelButton.Click += (s, e) => Finish(false);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(OkButton);
        buttons.Children.Add(CancelButton);

        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(Tabs);
        Content = layout;

        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Finish(false); e.Handled = true; } };
        Closing += (s, e) =>
        {
            // The window's close button is Cancel
            if (!answered) { answered = true; OnCancel(); }
            SaveActiveTab();
        };
        Opened += (s, e) => RestoreActiveTab();
    }

    /// <summary>Adds a tab; returns the item so a dialog can remove it again (no custom fields in the map format).</summary>
    protected TabItem AddTab(string header, Control content)
    {
        var item = new TabItem { Header = header, Content = content };
        Tabs.Items.Add(item);
        return item;
    }

    /// <summary>OK: the dialog applies its values. Return false to stay open (invalid input).</summary>
    protected abstract bool OnAccept();

    /// <summary>Cancel (or closed): the dialog withdraws what it changed live.</summary>
    protected abstract void OnCancel();

    private void Finish(bool ok)
    {
        if (answered) return;
        if (ok)
        {
            if (!OnAccept()) return;
            answered = true;
            Accepted = true;
        }
        else
        {
            answered = true;
            OnCancel();
        }
        Close(ok);
    }

    private string TabSetting => "windows." + settingname + ".activetab";

    private void RestoreActiveTab()
    {
        if (General.Settings == null || !General.Settings.StoreSelectedEditTab) return;
        int index = General.Settings.ReadSetting(TabSetting, 0);
        if (index >= 0 && index < Tabs.ItemCount) Tabs.SelectedIndex = index;
    }

    private void SaveActiveTab()
    {
        if (General.Settings == null || Tabs.SelectedIndex < 0) return;
        General.Settings.WriteSetting(TabSetting, Tabs.SelectedIndex);
    }

    /// <summary>A labeled row for a form grid.</summary>
    protected static Control Labeled(string label, Control control, double labelwidth = 110)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3) };
        var text = new TextBlock { Text = label, Width = labelwidth, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(text, Dock.Left);
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }
}
