using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using DoomBuilder.App.Shell;

namespace DoomBuilder.App.Dialogs;

/// <summary>The errors and warnings collected by the <see cref="ErrorLogger"/> (UDB's ErrorsForm). New ones appear while it is open.</summary>
public sealed class ErrorsWindow : Window
{
    private readonly ListBox list = new ListBox { SelectionMode = SelectionMode.Multiple };
    private readonly Button copy = new Button { Content = "Copy selected" };
    private readonly Button showsource = new Button { Content = "Show source" };
    private readonly Button clear = new Button { Content = "Clear list" };
    private readonly CheckBox showonerrors = new CheckBox { Content = "Show this window when errors or warnings occur" };
    private readonly DispatcherTimer watcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly List<ErrorItem> items = new List<ErrorItem>();

    public ErrorsWindow()
    {
        Title = "Errors and Warnings";
        Width = 640;
        Height = 360;
        MinWidth = 420;
        MinHeight = 240;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        showonerrors.IsChecked = General.Settings.ShowErrorsWindow;

        var close = new Button { Content = "Close", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        close.Click += (s, e) => Close();
        copy.Click += (s, e) => CopySelected();
        showsource.Click += (s, e) => ShowSource();
        clear.Click += (s, e) => ClearList();
        list.SelectionChanged += (s, e) => UpdateButtons();
        list.DoubleTapped += (s, e) => ShowSource();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(copy);
        buttons.Children.Add(showsource);
        buttons.Children.Add(clear);
        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(close, Dock.Right);
        bottom.Children.Add(close);
        bottom.Children.Add(buttons);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        DockPanel.SetDock(showonerrors, Dock.Bottom);
        showonerrors.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(bottom);
        root.Children.Add(showonerrors);
        root.Children.Add(list);
        Content = root;

        KeyDown += (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closing += (s, e) =>
        {
            watcher.Stop();
            General.Settings.ShowErrorsWindow = showonerrors.IsChecked == true;
        };

        FillList();
        watcher.Tick += (s, e) => { if (General.ErrorLogger.HasChanged) FillList(); };
        watcher.Start();
        Opened += (s, e) => list.Focus();
    }

    /// <summary>The rows shown (for tests).</summary>
    public IReadOnlyList<ErrorItem> Items => items;
    public ListBox List => list;
    public Button ClearButton => clear;
    public Button CopyButton => copy;
    public Button ShowSourceButton => showsource;
    public CheckBox ShowOnErrors => showonerrors;

    // Adds the items the logger has that this window does not show yet
    private void FillList()
    {
        General.ErrorLogger.HasChanged = false;
        foreach (ErrorItem error in General.ErrorLogger.GetErrors(items.Count).ToList())
        {
            items.Add(error);
            list.Items.Add(RowOf(error));
        }
        UpdateButtons();
    }

    private static Control RowOf(ErrorItem error)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var icon = ImageCache.Get(error.Type == ErrorType.Error ? "ErrorLarge" : "WarningLarge");
        if (icon != null)
            row.Children.Add(new Image { Source = icon, Width = 24, Height = 24, Margin = new Thickness(2, 2, 10, 2), VerticalAlignment = VerticalAlignment.Top });
        var text = new TextBlock { Text = error.Description, TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private void UpdateButtons()
    {
        clear.IsEnabled = items.Count > 0;
        copy.IsEnabled = list.SelectedItems?.Count > 0;
        showsource.IsEnabled = SelectedError()?.IsShowable == true;
    }

    // The error of the selected row, when exactly one is selected
    private ErrorItem SelectedError()
    {
        if (list.SelectedItems?.Count != 1) return null;
        int index = list.SelectedIndex;
        return index >= 0 && index < items.Count ? items[index] : null;
    }

    private void ShowSource()
    {
        ErrorItem error = SelectedError();
        if (error is { IsShowable: true }) error.ShowSource();
    }

    private void ClearList()
    {
        General.ErrorLogger.Clear();
        items.Clear();
        list.Items.Clear();
        UpdateButtons();
    }

    /// <summary>The text of the selected rows, one per line.</summary>
    public string SelectedText()
    {
        var indexes = list.Selection.SelectedIndexes.OrderBy(i => i).Where(i => i < items.Count);
        return string.Join("\n", indexes.Select(i => items[i].Description));
    }

    private async void CopySelected()
    {
        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        try { await clipboard.SetTextAsync(SelectedText()); }
        catch (Exception) { General.Interface.DisplayStatus(CodeImp.DoomBuilder.Windows.StatusType.Warning, "Failed to perform a Clipboard operation..."); }
    }
}
