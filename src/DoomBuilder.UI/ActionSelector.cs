using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Controls;

namespace DoomBuilder.UI;

/// <summary>
/// A number (linedef action, sector effect...) with the list of the known ones: typing a number selects its entry, picking an entry
/// fills the number. A number without an entry shows "None", "Generalized (...)" or "Unknown" in grey, as UDB's ActionSelectorControl.
/// </summary>
public sealed class ActionSelector : UserControl
{
    private readonly TextBox number = new TextBox { Width = 70, MaxLength = 10 };
    private readonly ComboBox list = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock unknown = new TextBlock { IsHitTestVisible = false, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private List<INumberedTitle> items = new List<INumberedTitle>();
    private bool updating;

    /// <summary>The number changed (typed or picked).</summary>
    public event EventHandler ValueChanges;

    public List<GeneralizedCategory> GeneralizedCategories { get; set; }
    public List<GeneralizedOption> GeneralizedOptions { get; set; }

    /// <summary>True when the box is empty (the elements being edited have different values).</summary>
    public bool Empty { get => (number.Text ?? "").Length == 0; set { if (value) number.Text = ""; } }

    public int Value { get => GetValue(); set => number.Text = value.ToString(); }

    public ActionSelector()
    {
        var listHost = new Grid();
        listHost.Children.Add(list);
        listHost.Children.Add(unknown);

        var row = new DockPanel();
        DockPanel.SetDock(number, Dock.Left);
        row.Children.Add(number);
        row.Children.Add(listHost);
        Content = row;

        number.PropertyChanged += (s, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            string text = number.Text ?? "";
            // Digits only
            string digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits != text) { number.Text = digits; return; }
            OnNumberChanged();
        };
        number.KeyDown += (s, e) =>
        {
            // Up/down walk the list, as in UDB
            if (e.Key == Key.Down && list.SelectedIndex > 0) { list.SelectedIndex--; e.Handled = true; }
            else if (e.Key == Key.Up && list.SelectedIndex < items.Count - 1) { list.SelectedIndex++; e.Handled = true; }
        };
        list.SelectionChanged += (s, e) =>
        {
            if (updating || !(list.SelectedItem is ListEntry entry)) return;
            number.Text = entry.Item.Index.ToString();
        };
    }

    public int GetValue()
    {
        return int.TryParse(number.Text, out int v) ? v : 0;
    }

    public void ClearInfo() { items = new List<INumberedTitle>(); Refill(); }

    public void AddInfo(INumberedTitle[] infolist) { items.AddRange(infolist); Refill(); }

    private sealed class ListEntry
    {
        public INumberedTitle Item;
        public override string ToString() => Item.Index + "\t" + Item.Title;
    }

    private void Refill()
    {
        updating = true;
        list.ItemsSource = items.Select(i => new ListEntry { Item = i }).ToList();
        updating = false;
        OnNumberChanged();
    }

    private void OnNumberChanged()
    {
        string text = number.Text ?? "";
        int index = -1;
        if (text.Length > 0)
            for (int i = 0; i < items.Count; i++)
                if (items[i].Index.ToString() == text) { index = i; break; }

        updating = true;
        list.SelectedIndex = index;
        updating = false;
        unknown.Text = index >= 0 ? "" : ActionSelectorLogic.DescribeUnknown(text, GeneralizedCategories, GeneralizedOptions);
        ValueChanges?.Invoke(this, EventArgs.Empty);
    }
}
