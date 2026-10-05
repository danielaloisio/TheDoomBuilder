using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's thing type browser: the tree of categories and things with a filter, the type number, and what is known about the
/// selected type (size, position, blocking, class, sprite). The logic is <see cref="ThingBrowserModel"/>.
/// </summary>
public sealed class ThingBrowser : UserControl
{
    private readonly ThingBrowserModel model = new ThingBrowserModel();
    private readonly TreeView tree = new TreeView();
    private readonly TextBox filter = new TextBox { Watermark = "Filter", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox typeId = new TextBox { Width = 90 };
    private readonly TextBlock size = new TextBlock(), position = new TextBlock(), blocking = new TextBlock();
    private readonly TextBlock className = new TextBlock();
    private readonly Image sprite = new Image { Width = 80, Height = 80, Stretch = Stretch.Uniform };
    private readonly DispatcherTimer spriteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    private bool updating;

    /// <summary>The selected type changed (null when empty or unknown).</summary>
    public event Action<ThingTypeInfo> TypeChanged;

    /// <summary>A thing (not a category) was double-clicked.</summary>
    public event Action TypeDoubleClicked;

    public ThingBrowserModel Model => model;

    public string TypeStringValue => model.TypeText;

    public bool UseMultiSelection
    {
        get => model.UseMultiSelection;
        set { model.UseMultiSelection = value; tree.SelectionMode = value ? SelectionMode.Multiple : SelectionMode.Single; }
    }

    public ThingBrowser()
    {
        var clear = new Button { Content = "✕", Padding = new Thickness(6, 0) };
        clear.Click += (s, e) => filter.Text = "";
        var filterRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(clear, Dock.Right);
        filterRow.Children.Add(clear);
        filterRow.Children.Add(filter);

        var info = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        void Row(int row, string label, Control value)
        {
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 1, 12, 1) };
            Grid.SetRow(l, row);
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            info.Children.Add(l);
            info.Children.Add(value);
        }
        var typeRow = new DockPanel();
        typeRow.Children.Add(typeId);
        Row(0, "Type:", typeRow);
        Row(1, "Size:", size);
        Row(2, "Position:", position);
        Row(3, "Blocking:", blocking);
        Row(4, "Class:", className);
        Grid.SetColumn(sprite, 2);
        Grid.SetRowSpan(sprite, 5);
        info.Children.Add(sprite);

        var layout = new DockPanel();
        DockPanel.SetDock(filterRow, Dock.Top);
        DockPanel.SetDock(info, Dock.Bottom);
        layout.Children.Add(filterRow);
        layout.Children.Add(info);
        layout.Children.Add(tree);
        Content = layout;

        filter.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) { model.Filter = filter.Text ?? ""; FillTree(); } };
        filter.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Down || tree.ItemCount == 0) return;
            tree.SelectedItem = tree.Items.Cast<object>().First();
            tree.Focus();
            e.Handled = true;
        };

        tree.SelectionChanged += (s, e) =>
        {
            if (updating) return;
            updating = true;
            model.Select(tree.SelectedItems.OfType<TreeViewItem>().Select(i => (ThingBrowserNode)i.Tag));
            typeId.Text = model.TypeText;
            updating = false;
            ShowInfo();
        };
        tree.DoubleTapped += (s, e) =>
        {
            if (tree.SelectedItem is TreeViewItem item && ((ThingBrowserNode)item.Tag).Info != null && model.TypeText.Length > 0)
                TypeDoubleClicked?.Invoke();
        };

        typeId.PropertyChanged += (s, e) =>
        {
            if (updating || e.Property != TextBox.TextProperty) return;
            updating = true;
            model.SetTypeText(typeId.Text ?? "");
            SelectInTree();
            updating = false;
            ShowInfo();
        };

        spriteTimer.Tick += (s, e) => { spriteTimer.Stop(); ShowSprite(); };
        DetachedFromVisualTree += (s, e) => spriteTimer.Stop();
        FillTree();
    }

    /// <summary>Selects a type (call after the map is open).</summary>
    public void SelectType(int type)
    {
        updating = true;
        model.SelectType(type);
        typeId.Text = model.TypeText;
        SelectInTree();
        updating = false;
        ShowInfo();
    }

    public void SelectMultipleTypes(int[] types)
    {
        updating = true;
        model.SelectMultipleTypes(types);
        typeId.Text = model.TypeText;
        SelectInTree();
        updating = false;
        ShowInfo();
    }

    public void ClearSelectedType()
    {
        updating = true;
        model.ClearSelectedType();
        typeId.Text = "";
        tree.SelectedItems?.Clear();
        updating = false;
        ShowInfo();
    }

    public ThingTypeInfo GetSelectedInfo() => model.Info;
    public int GetResult(int original) => model.GetResult(original);
    public void FocusFilter() => filter.Focus();

    private void FillTree()
    {
        updating = true;
        tree.ItemsSource = model.Roots.Select(MakeItem).ToList();
        updating = false;
        SelectInTree();
    }

    private TreeViewItem MakeItem(ThingBrowserNode node)
    {
        var item = new TreeViewItem { Header = node.Title, Tag = node };
        if (node.IsObsolete)
        {
            item.Background = new SolidColorBrush(Colors.MistyRose, 0.35);
            if (node.Info != null) ToolTip.SetTip(item, node.Info.ObsoleteMessage);
        }
        if (node.Children.Count > 0) item.ItemsSource = node.Children.Select(MakeItem).ToList();
        return item;
    }

    // Shows the model's selection in the tree, opening the categories that hold it
    private void SelectInTree()
    {
        if (model.Selected.Count == 0) { tree.SelectedItems?.Clear(); return; }
        var wanted = new HashSet<ThingBrowserNode>(model.Selected);
        var found = new List<TreeViewItem>();
        Walk(tree.Items.Cast<object>().OfType<TreeViewItem>(), wanted, found);
        if (tree.SelectionMode == SelectionMode.Single && found.Count > 0) tree.SelectedItem = found[0];
        else { tree.SelectedItems?.Clear(); foreach (TreeViewItem i in found) tree.SelectedItems?.Add(i); }
        if (found.Count > 0) found[0].BringIntoView();
    }

    private static bool Walk(IEnumerable<TreeViewItem> items, HashSet<ThingBrowserNode> wanted, List<TreeViewItem> found)
    {
        bool any = false;
        foreach (TreeViewItem item in items)
        {
            if (wanted.Contains((ThingBrowserNode)item.Tag)) { found.Add(item); any = true; }
            if (Walk(item.Items.Cast<object>().OfType<TreeViewItem>(), wanted, found)) { item.IsExpanded = true; any = true; }
        }
        return any;
    }

    private void ShowInfo()
    {
        size.Text = model.SizeText;
        position.Text = model.PositionText;
        blocking.Text = model.BlockingText;
        className.Text = model.ClassNameText;
        className.Opacity = model.HasClassName ? 1 : 0.5;
        ShowSprite();
        TypeChanged?.Invoke(model.Info);
    }

    private void ShowSprite()
    {
        if (General.Map == null) return;
        ThingTypeInfo info = model.Info;
        if (info != null && !string.IsNullOrEmpty(info.Sprite))
        {
            string name = info.Sprite;
            if (name.ToLowerInvariant().StartsWith(DataManager.INTERNAL_PREFIX) && name.Length > DataManager.INTERNAL_PREFIX.Length)
            {
                sprite.Source = ImageConvert.ToAvalonia(General.Map.Data.GetSpriteImage(name).GetSpritePreview());
                return;
            }
            if (name.Length < 9)
            {
                ImageData image = General.Map.Data.GetSpriteImage(name);
                sprite.Source = ImageConvert.ToAvalonia(image.GetPreview());
                if (!image.IsPreviewLoaded) spriteTimer.Start();
                return;
            }
        }
        sprite.Source = model.HasMixedSelection ? ImageConvert.ToAvalonia(CodeImp.DoomBuilder.Properties.Resources.MixedThings) : null;
    }
}
