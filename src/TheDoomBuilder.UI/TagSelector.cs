using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Types;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's tag selector: an editable list of the tags in use, with buttons for a new tag, an unused tag, clearing, and stepping.
/// The box also takes ">=N", "&lt;=N", "++N", "--N" and labels (see <see cref="TagSelectorModel"/>).
/// </summary>
public sealed class TagSelector : UserControl
{
    private readonly TagSelectorModel model = new TagSelectorModel();
    private readonly ComboBox picker = new ComboBox { IsEditable = true, HorizontalAlignment = HorizontalAlignment.Stretch };
    private bool updating;

    public event EventHandler ValueChanged;

    public TagSelectorModel Model => model;

    /// <summary>The text of the box (what the user typed or picked).</summary>
    public string Text => picker.Text ?? "";

    public TagSelector()
    {
        var up = new RepeatButton { Content = "▲", FontSize = 7, Padding = new Thickness(4, 0), Focusable = false };
        var down = new RepeatButton { Content = "▼", FontSize = 7, Padding = new Thickness(4, 0), Focusable = false };
        up.Click += (s, e) => { Sync(); model.Step(1); Show(); };
        down.Click += (s, e) => { Sync(); model.Step(-1); Show(); };
        var spinner = new StackPanel();
        spinner.Children.Add(up);
        spinner.Children.Add(down);

        Button Small(string text, string tip, Action act)
        {
            var b = new Button { Content = text, Padding = new Thickness(6, 2) };
            ToolTip.SetTip(b, tip);
            b.Click += (s, e) => { act(); Show(); };
            return b;
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        buttons.Children.Add(spinner);
        buttons.Children.Add(Small("New", "New tag (not used by anything)", () => model.NewTag()));
        buttons.Children.Add(Small("Unused", "A tag not used by this type of element", () => model.UnusedTag()));
        buttons.Children.Add(Small("0", "Clear the tag", () => model.Clear()));

        var row = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(picker);
        Content = row;

        picker.PropertyChanged += (s, e) =>
        {
            if (updating || e.Property != ComboBox.TextProperty) return;
            model.Text = picker.Text ?? "";
            ValueChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Lists the tags in use for the element type (call once the map is open).</summary>
    public void Setup(UniversalType type)
    {
        model.Setup(type);
        picker.ItemsSource = model.Infos.Select(i => i.ToString()).ToList();
    }

    public void SetTag(int tag) { model.SetTag(tag); Show(); }
    public void ClearTag() { model.ClearTag(); Show(); }

    /// <summary>Reads the box (labels, ranges...). Call before <see cref="GetTag"/>/<see cref="GetSmartTag"/>.</summary>
    public void ValidateTag() { Sync(); model.ValidateTag(); }
    public int GetTag(int original) => model.GetTag(original);
    public int GetSmartTag(int original, int offset) => model.GetSmartTag(original, offset);

    // The model holds the text; the box follows it
    private void Show()
    {
        updating = true;
        picker.Text = model.Text;
        updating = false;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Sync() => model.Text = picker.Text ?? "";
}
