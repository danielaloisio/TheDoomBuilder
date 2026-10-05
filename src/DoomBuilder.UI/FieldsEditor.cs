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
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// The custom (UDMF) fields of the elements being edited: name, type and value per row, undefined fixed fields greyed out,
/// and a line at the bottom to add a field of your own. The logic is <see cref="FieldsEditorModel"/>.
/// </summary>
public sealed class FieldsEditor : UserControl
{
    private readonly StackPanel list = new StackPanel { Spacing = 1 };
    private readonly TextBox newField = new TextBox { Watermark = "(type a name to add a custom field)" };
    private readonly TextBlock message = new TextBlock { Foreground = Brushes.OrangeRed, IsVisible = false, TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<FieldRow, RowView> views = new Dictionary<FieldRow, RowView>();

    public FieldsEditorModel Model { get; } = new FieldsEditorModel();

    /// <summary>Raised whenever the user changes a value, type, name or removes a field.</summary>
    public event EventHandler Edited;

    public bool AllowInsert { get => Model.AllowInsert; set { Model.AllowInsert = value; newField.IsVisible = value; } }

    public FieldsEditor()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("140,110,*"), Margin = new Thickness(4, 0) };
        foreach (var (text, col) in new[] { ("Property", 0), ("Type", 1), ("Value", 2) })
        {
            var t = new TextBlock { Text = text, FontWeight = FontWeight.SemiBold };
            Grid.SetColumn(t, col);
            header.Children.Add(t);
        }

        newField.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter) return;
            AddField();
            e.Handled = true;
        };
        newField.LostFocus += (s, e) => AddField();

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(newField, Dock.Bottom);
        DockPanel.SetDock(message, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(message);
        layout.Children.Add(newField);
        layout.Children.Add(new ScrollViewer { Content = list });
        Content = layout;

        Model.RowsChanged += Rebuild;
    }

    /// <summary>Prepares the editor for an element type ("thing", "linedef", "sidedef", "sector" or "vertex").</summary>
    public void Setup(string elementname, bool showManaged = false) => Model.Setup(elementname, showManaged);

    public void ListFixedFields(IEnumerable<UniversalFieldInfo> fields) => Model.ListFixedFields(fields);
    public void SetValues(UniFields fields, bool first) { Model.SetValues(fields, first); Rebuild(); }
    public void Apply(UniFields fields) => Model.Apply(fields);

    private void AddField()
    {
        string text = newField.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        string error = Model.AddField(text.Trim(), out FieldRow added);
        newField.Text = "";
        Show(error);
        if (added != null)
        {
            Edited?.Invoke(this, EventArgs.Empty);
            if (views.TryGetValue(added, out RowView view)) view.FocusValue();
        }
    }

    private void Show(string error)
    {
        message.Text = error ?? "";
        message.IsVisible = !string.IsNullOrEmpty(error);
    }

    private void Rebuild()
    {
        list.Children.Clear();
        views.Clear();
        foreach (FieldRow row in Model.VisibleRows)
        {
            var view = new RowView(this, row);
            views[row] = view;
            list.Children.Add(view);
        }
    }

    private void RaiseEdited() => Edited?.Invoke(this, EventArgs.Empty);

    private sealed class RowView : Grid
    {
        private readonly FieldsEditor owner;
        private readonly FieldRow row;
        private readonly TextBlock name = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBox nameEdit = new TextBox { IsVisible = false };
        private readonly Control typeControl;
        private readonly Control valueControl;
        private bool updating;

        public RowView(FieldsEditor owner, FieldRow row)
        {
            this.owner = owner;
            this.row = row;
            ColumnDefinitions = new ColumnDefinitions("140,110,*,Auto");
            Margin = new Thickness(4, 1);

            name.Text = row.Name;
            var nameHost = new Grid();
            nameHost.Children.Add(name);
            nameHost.Children.Add(nameEdit);
            if (row.NameCanChange)
            {
                name.DoubleTapped += (s, e) => { nameEdit.Text = row.Name; nameEdit.IsVisible = true; name.IsVisible = false; nameEdit.Focus(); nameEdit.SelectAll(); };
                nameEdit.KeyDown += (s, e) => { if (e.Key == Key.Enter) { CommitName(); e.Handled = true; } else if (e.Key == Key.Escape) { EndNameEdit(); e.Handled = true; } };
                nameEdit.LostFocus += (s, e) => CommitName();
            }
            Children.Add(nameHost);

            if (row.TypeCanChange)
            {
                var combo = new ComboBox { ItemsSource = owner.Model.CustomTypes.Select(t => t.ToString()).ToList(), SelectedItem = row.TypeName, HorizontalAlignment = HorizontalAlignment.Stretch };
                combo.SelectionChanged += (s, e) =>
                {
                    if (updating || !(combo.SelectedItem is string chosen)) return;
                    owner.Model.SetType(row, chosen);
                    Refresh();
                    owner.RaiseEdited();
                };
                typeControl = combo;
            }
            else typeControl = new TextBlock { Text = row.TypeName, VerticalAlignment = VerticalAlignment.Center };
            SetColumn(typeControl, 1);
            Children.Add(typeControl);

            if (row.IsEnumerable)
            {
                var combo = new ComboBox { IsEditable = !row.IsLimitedToEnums, HorizontalAlignment = HorizontalAlignment.Stretch };
                combo.ItemsSource = row.EnumItems.Select(i => i.Title).ToList();
                combo.Text = row.Text;
                combo.SelectionChanged += (s, e) => { if (!updating && combo.SelectedItem is string chosen) Commit(chosen); };
                if (!row.IsLimitedToEnums) combo.LostFocus += (s, e) => { if (!updating) Commit(combo.Text ?? ""); };
                valueControl = combo;
            }
            else
            {
                var box = new TextBox { Text = row.Text, HorizontalAlignment = HorizontalAlignment.Stretch };
                box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { Commit(box.Text ?? ""); e.Handled = true; } };
                box.LostFocus += (s, e) => { if (!updating) Commit(box.Text ?? ""); };
                valueControl = box;
            }
            SetColumn(valueControl, 2);
            Children.Add(valueControl);

            var tail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            if (row.IsBrowseable)
            {
                var browse = new Button { Content = "…", Padding = new Thickness(6, 0) };
                browse.Click += (s, e) => { row.Browse(null); owner.Model.SetValue(row, row.Text); Refresh(); owner.RaiseEdited(); };
                tail.Children.Add(browse);
            }
            var remove = new Button { Content = row.TypeCanChange && row.NameCanChange ? "✕" : "↺", Padding = new Thickness(6, 0) };
            ToolTip.SetTip(remove, row.NameCanChange ? "Remove this field" : "Reset to the default");
            remove.Click += (s, e) => { owner.Model.Delete(row); Refresh(); owner.RaiseEdited(); };
            tail.Children.Add(remove);
            SetColumn(tail, 3);
            Children.Add(tail);

            Refresh();
        }

        public void FocusValue() => valueControl.Focus();

        private void Commit(string value)
        {
            if (value == row.Text) return;
            owner.Model.SetValue(row, value);
            Refresh();
            owner.RaiseEdited();
        }

        private void CommitName()
        {
            if (!nameEdit.IsVisible) return;
            string error = owner.Model.Rename(row, nameEdit.Text ?? "");
            owner.Show(error);
            EndNameEdit();
            name.Text = row.Name;
            owner.RaiseEdited();
        }

        private void EndNameEdit() { nameEdit.IsVisible = false; name.IsVisible = true; }

        // Undefined fields are greyed out; a cleared value (elements disagree) shows empty
        private void Refresh()
        {
            updating = true;
            name.Opacity = row.IsDefined ? 1 : 0.55;
            valueControl.Opacity = row.IsDefined ? 1 : 0.55;
            if (valueControl is TextBox box) box.Text = row.Text;
            else if (valueControl is ComboBox combo) combo.Text = row.Text;
            if (typeControl is ComboBox type) type.SelectedItem = row.TypeName;
            updating = false;
        }
    }
}
