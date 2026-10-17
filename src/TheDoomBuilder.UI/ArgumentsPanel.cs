using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// The five arguments of an action (UDB's ArgumentsControl): a caption and a box each, the captions following the action (or the
/// thing type); for script actions the first argument is a script number, a script name or a string. The logic is <see cref="ArgumentsModel"/>.
/// </summary>
public sealed class ArgumentsPanel : UserControl
{
    private readonly ArgumentsModel model = new ArgumentsModel();
    private readonly TextBlock[] labels = new TextBlock[5];
    private readonly ArgumentBox[] boxes = new ArgumentBox[5];
    private readonly ComboBox scriptNumber = new ComboBox { IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox scriptName = new ComboBox { IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox scriptString = new TextBox();
    private readonly CheckBox useString = new CheckBox { Content = "Use string", Margin = new Thickness(8, 0, 0, 0) };
    private readonly Panel zeroHost = new Panel();
    private bool updating;

    /// <summary>An argument changed.</summary>
    public event EventHandler ValueChanged;

    public ArgumentsModel Model => model;
    public ArgumentBox Arg(int index) => boxes[index];
    public TextBlock Label(int index) => labels[index];
    public IReadOnlyList<ArgumentBox> Boxes => boxes;

    public ArgumentsPanel()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto") };
        for (int i = 0; i < 5; i++)
        {
            labels[i] = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
            boxes[i] = new ArgumentBox(model.Args[i]) { Margin = new Thickness(0, 2) };
            boxes[i].ValueChanged += (s, e) => { if (!updating) ValueChanged?.Invoke(this, EventArgs.Empty); };
            Grid.SetRow(labels[i], i);
            Grid.SetRow(boxes[i], i);
            Grid.SetColumn(boxes[i], 1);
            grid.Children.Add(labels[i]);
            if (i == 0) { zeroHost.Children.Add(boxes[0]); Grid.SetRow(zeroHost, 0); Grid.SetColumn(zeroHost, 1); grid.Children.Add(zeroHost); }
            else grid.Children.Add(boxes[i]);
        }
        zeroHost.Children.Add(scriptNumber);
        zeroHost.Children.Add(scriptName);
        zeroHost.Children.Add(scriptString);
        Grid.SetRow(useString, 0);
        Grid.SetColumn(useString, 2);
        grid.Children.Add(useString);
        Content = grid;

        scriptNumber.PropertyChanged += (s, e) => { if (!updating && e.Property == ComboBox.TextProperty) { model.ScriptNumberText = scriptNumber.Text ?? ""; model.ScriptChanged(model.ScriptNumberText, false); ValueChanged?.Invoke(this, EventArgs.Empty); } };
        scriptName.PropertyChanged += (s, e) => { if (!updating && e.Property == ComboBox.TextProperty) { model.ScriptNameText = scriptName.Text ?? ""; model.ScriptChanged(model.ScriptNameText, true); ValueChanged?.Invoke(this, EventArgs.Empty); } };
        scriptString.PropertyChanged += (s, e) => { if (!updating && e.Property == TextBox.TextProperty) { model.ScriptStringText = scriptString.Text ?? ""; ValueChanged?.Invoke(this, EventArgs.Empty); } };
        useString.IsCheckedChanged += (s, e) => { if (!updating) model.SetUseArgString(useString.IsChecked == true); };
        model.Changed += Refresh;
        Refresh();
    }

    public void Reset() { }

    public void SetValue(Linedef l, bool first) { model.SetValue(l, first); Show(); }
    public void SetValue(Thing t, bool first) { model.SetValue(t, first); Show(); }
    public void Apply(Linedef l, int step) => model.Apply(l, step);
    public void Apply(Thing t, int step) => model.Apply(t, step);

    /// <summary>The action (or thing type) changed: captions, boxes and defaults follow.</summary>
    public void UpdateAction(int action, bool setuponly, ThingTypeInfo info = null)
    {
        updating = true;
        model.UpdateAction(action, setuponly, info);
        foreach (ArgumentBox b in boxes) b.Rebuild();
        updating = false;
        Refresh();
    }

    public void UpdateScriptControls() { model.UpdateScriptControls(); Refresh(); }

    private void Show() { foreach (ArgumentBox b in boxes) b.Show(); }

    // Captions (greyed when unused, tooltip when the action describes the argument) and the first argument's controls
    private void Refresh()
    {
        updating = true;
        for (int i = 0; i < 5; i++)
        {
            ArgumentLabel l = model.Labels[i];
            labels[i].Text = l.Text ?? "";
            labels[i].IsEnabled = l.Enabled;
            boxes[i].Opacity = l.Enabled ? 1 : 0.55;
            ToolTip.SetTip(labels[i], l.ToolTip);
            labels[i].TextDecorations = l.ToolTip != null ? TextDecorations.Underline : null;
        }

        bool udmf = General.Map != null && General.Map.UDMF;
        useString.IsVisible = udmf && model.Arg0Mode != ArgZeroMode.Default || (udmf && model.UseArgString);
        useString.IsChecked = model.UseArgString;
        scriptNumber.ItemsSource = model.NumberedScripts().Select(s => s.Text).ToList();
        scriptName.ItemsSource = model.NamedScripts().Select(s => s.Text).ToList();
        scriptNumber.Text = model.ScriptNumberText;
        scriptName.Text = model.ScriptNameText;
        scriptString.Text = model.ScriptStringText;

        bool isacs = model.NumberedScripts().Any();
        scriptNumber.IsVisible = model.Arg0Mode == ArgZeroMode.Int && isacs;
        scriptName.IsVisible = model.Arg0Mode == ArgZeroMode.String && isacs;
        scriptString.IsVisible = model.Arg0Mode == ArgZeroMode.String && !isacs;
        boxes[0].IsVisible = !(scriptNumber.IsVisible || scriptName.IsVisible || scriptString.IsVisible);
        updating = false;
    }
}
