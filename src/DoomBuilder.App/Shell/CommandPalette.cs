using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The command palette (UDB's CommandPaletteControl): a search box over the display with the actions that match, to run one
/// by name. The matching and the recent list are in <see cref="CommandPaletteModel"/>.
/// </summary>
public sealed class CommandPalette : Border
{
    private const int PageSize = 19;

    private readonly CommandPaletteModel model = new CommandPaletteModel();
    private readonly TextBox search = new TextBox { PlaceholderText = "Type the name of a command" };
    private readonly ListBox list = new ListBox { MaxHeight = 420, Focusable = false };
    private readonly TextBlock noresults = new TextBlock { Text = "No matching commands", Opacity = 0.6, Margin = new Thickness(8, 6) };
    private List<PaletteEntry> entries = new List<PaletteEntry>();

    /// <summary>The palette was closed (with or without running something): the window gives the keyboard back to the editor.</summary>
    public event Action Closed;

    public CommandPalette()
    {
        IsVisible = false;
        Width = 560;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 5, 0, 0);
        Padding = new Thickness(8);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BorderBrush = Brushes.Gray;
        Background = new SolidColorBrush(Color.FromArgb(240, 40, 40, 44));

        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(search);
        panel.Children.Add(list);
        panel.Children.Add(noresults);
        Child = panel;

        search.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) Fill(); };
        search.AddHandler(KeyDownEvent, OnSearchKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        list.DoubleTapped += (s, e) => RunSelected();
        list.Tapped += (s, e) => { if (list.SelectedItem != null && e.Source is Control c && c.FindAncestorOfType<ListBoxItem>() != null) RunSelected(); };
    }

    public TextBox SearchBox => search;
    public ListBox List => list;

    /// <summary>The lines now listed.</summary>
    public IReadOnlyList<PaletteEntry> Entries => entries;

    public PaletteEntry Selected => list.SelectedIndex >= 0 && list.SelectedIndex < entries.Count ? entries[list.SelectedIndex] : null;

    public void Open()
    {
        search.Text = string.Empty;
        Fill();
        IsVisible = true;
        search.Focus();
    }

    public void Close()
    {
        if (!IsVisible) return;
        IsVisible = false;
        Closed?.Invoke();
    }

    private void Fill()
    {
        entries = model.Search(search.Text);
        list.ItemsSource = entries.Select(Row).ToList();
        noresults.IsVisible = entries.Count == 0;
        list.IsVisible = entries.Count > 0;
        if (entries.Count > 0) list.SelectedIndex = 0;
    }

    private static Control Row(PaletteEntry entry)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 14, Opacity = entry.Usable ? 1 : 0.5 };
        var title = new TextBlock { Text = (entry.Group == PaletteGroup.Recent ? "↺ " : "") + entry.Title, TextTrimming = TextTrimming.CharacterEllipsis };
        var category = new TextBlock { Text = entry.Category, Opacity = 0.6 };
        var shortcut = new TextBlock { Text = entry.Shortcut, Opacity = 0.8, MinWidth = 70, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(category, 1);
        Grid.SetColumn(shortcut, 2);
        row.Children.Add(title);
        row.Children.Add(category);
        row.Children.Add(shortcut);
        return row;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Down: Move(1, true); break;
            case Key.Up: Move(-1, true); break;
            case Key.PageDown: Move(PageSize, false); break;
            case Key.PageUp: Move(-PageSize, false); break;
            case Key.Enter: RunSelected(); break;
            default: return;
        }
        e.Handled = true;      // the box must not also use these keys (cursor movement, a beep)
    }

    private void Move(int by, bool wrap)
    {
        if (entries.Count < 2) return;
        int next = list.SelectedIndex + by;
        if (next >= entries.Count) next = wrap ? 0 : entries.Count - 1;
        else if (next < 0) next = wrap ? entries.Count - 1 : 0;
        list.SelectedIndex = next;
        list.ScrollIntoView(next);
    }

    private void RunSelected()
    {
        PaletteEntry entry = Selected;
        if (entry == null) return;
        Close();                       // first: the action may open a dialog, which must not find the palette in the way
        model.Run(entry);
    }
}
