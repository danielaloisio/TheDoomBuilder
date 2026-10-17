using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// One argument of an action (UDB's ArgumentBox): a list of choices when the type has them, a browse button when it can be browsed,
/// else a number with up and down buttons. The text is kept in the <see cref="ArgumentModel"/>.
/// </summary>
public sealed class ArgumentBox : UserControl
{
    private readonly ArgumentModel model;
    private TextBox text;
    private ComboBox combo;
    private bool updating;

    /// <summary>The text changed (typed, picked or by the buttons).</summary>
    public event EventHandler ValueChanged;

    public ArgumentBox(ArgumentModel model)
    {
        this.model = model;
        Rebuild();
    }

    public ArgumentModel Model => model;
    public string Text => model.Text;

    /// <summary>Call after the model's argument was set up again (the type may need other controls).</summary>
    public void Rebuild()
    {
        var row = new DockPanel();

        if (model.IsEnumerable)
        {
            combo = new ComboBox { IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = model.EnumItems.Select(i => i.Title).ToList() };
            combo.PropertyChanged += (s, e) =>
            {
                if (updating || e.Property != ComboBox.TextProperty) return;
                model.Text = combo.Text ?? "";
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            combo.LostFocus += (s, e) => Commit();
            combo.SelectionChanged += (s, e) => { if (!updating && combo.SelectedItem is string chosen) { model.Text = chosen; Commit(); } };
            row.Children.Add(combo);
            text = null;
        }
        else
        {
            combo = null;
            text = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            text.PropertyChanged += (s, e) =>
            {
                if (updating || e.Property != TextBox.TextProperty) return;
                model.Text = text.Text ?? "";
                ValueChanged?.Invoke(this, EventArgs.Empty);
            };
            text.LostFocus += (s, e) => Commit();
            text.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
            text.PointerWheelChanged += (s, e) => { if (text.IsFocused && model.HasSteps) { Step(e.Delta.Y > 0 ? 1 : -1); e.Handled = true; } };

            if (model.IsBrowseable)
            {
                var browse = new Button { Content = "…", Padding = new Thickness(8, 0) };
                browse.Click += (s, e) => { model.Browse(null); Show(); ValueChanged?.Invoke(this, EventArgs.Empty); };
                DockPanel.SetDock(browse, Dock.Right);
                row.Children.Add(browse);
            }
            else if (model.HasSteps)
            {
                var up = new RepeatButton { Content = "▲", FontSize = 7, Padding = new Thickness(4, 0), Focusable = false };
                var down = new RepeatButton { Content = "▼", FontSize = 7, Padding = new Thickness(4, 0), Focusable = false };
                up.Click += (s, e) => Step(1);
                down.Click += (s, e) => Step(-1);
                var buttons = new StackPanel();
                buttons.Children.Add(up);
                buttons.Children.Add(down);
                DockPanel.SetDock(buttons, Dock.Right);
                row.Children.Add(buttons);
            }
            row.Children.Add(text);
        }

        Content = row;
        Show();
    }

    private void Step(int direction)
    {
        model.Step(direction);
        Show();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    // The model validates what the user typed when leaving the box
    private void Commit()
    {
        string before = model.Text;
        model.Commit();
        if (model.Text != before) { Show(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Shows the model's text in the box.</summary>
    public void Show()
    {
        updating = true;
        if (text != null) text.Text = model.Text;
        if (combo != null) combo.Text = model.Text;
        updating = false;
    }
}
