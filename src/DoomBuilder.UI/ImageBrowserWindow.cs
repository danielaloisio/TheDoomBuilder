using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Windows;

namespace DoomBuilder.UI;

/// <summary>
/// UDB's texture/flat browser: the tree of texture sets on the left, the thumbnails of the selected set on the right, filters by
/// name and size on top. The logic is <see cref="ImageBrowserModel"/>; this only draws it and answers with the chosen name.
/// </summary>
public sealed class ImageBrowserWindow : Window
{
    private const int TileSize = 96;

    private readonly ImageBrowserModel model;
    private readonly TreeView tree = new TreeView();
    private readonly TextBox filter = new TextBox { Watermark = "Filter by name", MinWidth = 180 };
    private readonly TextBox filterWidth = new TextBox { Watermark = "W", Width = 56 };
    private readonly TextBox filterHeight = new TextBox { Watermark = "H", Width = 56 };
    private readonly CheckBox usedFirst = new CheckBox { Content = "Used textures first" };
    private readonly WrapPanel tiles = new WrapPanel();
    private readonly ScrollViewer scroller;
    private readonly Button apply = new Button { Content = "OK", MinWidth = 80, IsEnabled = false, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button cancel = new Button { Content = "Cancel", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer refresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly List<Tile> imageTiles = new List<Tile>();
    private Tile selected;
    private bool filling;

    /// <summary>The name chosen, or null when the user cancelled.</summary>
    public string SelectedName { get; private set; }

    internal ImageBrowserWindow(string select, bool browseflats)
    {
        model = new ImageBrowserModel(select, browseflats);
        Title = "Browse " + (browseflats ? "flats" : "textures");
        Width = 900;
        Height = 640;
        MinWidth = 520;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        tree.ItemsSource = model.Roots.Select(MakeItem).ToList();
        tree.SelectionChanged += (s, e) =>
        {
            if (filling || !(tree.SelectedItem is TreeViewItem item)) return;
            model.Selected = (ImageBrowserNode)item.Tag;
            Fill(false);
        };

        filter.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) Fill(false); };
        filterWidth.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) Fill(false); };
        filterHeight.PropertyChanged += (s, e) => { if (e.Property == TextBox.TextProperty) Fill(false); };
        usedFirst.IsCheckedChanged += (s, e) => Fill(false);

        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(filter);
        top.Children.Add(new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(filterWidth);
        top.Children.Add(new TextBlock { Text = "x", VerticalAlignment = VerticalAlignment.Center });
        top.Children.Add(filterHeight);
        top.Children.Add(usedFirst);

        scroller = new ScrollViewer { Content = tiles, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroller.ScrollChanged += (s, e) => LoadVisiblePreviews();

        apply.Click += (s, e) => Accept();
        cancel.Click += (s, e) => Close(null);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);

        var right = new DockPanel { Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        right.Children.Add(top);
        right.Children.Add(buttons);
        right.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = scroller });

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*"), Margin = new Thickness(12) };
        body.Children.Add(tree);
        Grid.SetColumn(right, 1);
        body.Children.Add(right);
        Content = body;

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
            else if (e.Key == Key.Enter && apply.IsEnabled) { Accept(); e.Handled = true; }
        };
        Closing += (s, e) => refresh.Stop();
        refresh.Tick += (s, e) => LoadVisiblePreviews();
        Opened += (s, e) =>
        {
            SelectNode(model.Selected);
            Fill(true);
            refresh.Start();
        };
    }

    /// <summary>Asks the user for a texture (or flat). Returns <paramref name="select"/> unchanged when cancelled.</summary>
    public static string Browse(Window owner, string select, bool browseflats)
    {
        var window = new ImageBrowserWindow(select, browseflats);
        string result = DialogPump.Run(() => owner != null ? window.ShowDialog<string>(owner) : ShowAlone(window));
        if (result == null) return select;
        window.model.RememberSelection();
        return result;
    }

    private static Task<string> ShowAlone(ImageBrowserWindow window)
    {
        var done = new TaskCompletionSource<string>();
        window.Closed += (s, e) => done.TrySetResult(window.SelectedName);
        window.Show();
        return done.Task;
    }

    #region ================== Tree

    private TreeViewItem MakeItem(ImageBrowserNode node)
    {
        var item = new TreeViewItem { Header = node.Text, Tag = node, IsExpanded = node.Parent == null && node.Children.Count > 0 };
        item.ItemsSource = node.Children.Select(MakeItem).ToList();
        return item;
    }

    private TreeViewItem FindItem(IEnumerable<object> items, ImageBrowserNode node)
    {
        foreach (TreeViewItem item in items.OfType<TreeViewItem>())
        {
            if (item.Tag == node) return item;
            TreeViewItem child = FindItem(item.Items.Cast<object>(), node);
            if (child != null) { item.IsExpanded = true; return child; }
        }
        return null;
    }

    private void SelectNode(ImageBrowserNode node)
    {
        filling = true;
        tree.SelectedItem = node == null ? null : FindItem(tree.Items.Cast<object>(), node);
        filling = false;
    }

    #endregion

    #region ================== List

    private void Fill(bool selectinitial)
    {
        string previous = selected?.Image == null ? null : ImageBrowserModel.DisplayName(selected.Image);
        int.TryParse(filterWidth.Text, out int w);
        int.TryParse(filterHeight.Text, out int h);
        ImageBrowserListing listing = model.List(filter.Text, w > 0 ? w : -1, h > 0 ? h : -1, usedFirst.IsChecked == true);

        tiles.Children.Clear();
        imageTiles.Clear();
        selected = null;

        if (model.Selected != null) AddFolderTile(listing.UpCaption, true);
        foreach (ImageBrowserNode folder in listing.Folders) AddFolderTile(folder.FolderName, false);

        if (usedFirst.IsChecked == true)
            foreach (ImageData img in listing.Used) AddImageTile(img);
        foreach (ImageData img in listing.Images) AddImageTile(img);

        // Keep the selection when possible, else the image the dialog was opened on, else the first image
        Tile pick = null;
        if (previous != null) pick = imageTiles.FirstOrDefault(t => ImageBrowserModel.DisplayName(t.Image) == previous);
        if (pick == null && selectinitial) pick = imageTiles.FirstOrDefault(t => t.Image.LongName == model.SelectLongName);
        if (pick == null && selectinitial) pick = imageTiles.FirstOrDefault();
        Select(pick);
        if (pick != null) Dispatcher.UIThread.Post(() => pick.BringIntoView(), DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(LoadVisiblePreviews, DispatcherPriority.Loaded);
    }

    private void AddFolderTile(string caption, bool up)
    {
        var tile = new Tile(null, up ? "[..] " + caption : caption, true);
        tile.PointerPressed += (s, e) => { if (e.ClickCount >= 2) OpenFolder(caption, up); };
        tiles.Children.Add(tile);
    }

    private void AddImageTile(ImageData image)
    {
        var tile = new Tile(image, ImageBrowserModel.DisplayName(image), false) { Used = image.UsedInMap };
        tile.PointerPressed += (s, e) =>
        {
            Select(tile);
            if (e.ClickCount >= 2) Accept();
        };
        tiles.Children.Add(tile);
        imageTiles.Add(tile);
    }

    private void OpenFolder(string caption, bool up)
    {
        if (up) model.Up(); else if (!model.Open(caption)) return;
        SelectNode(model.Selected);
        Fill(false);
    }

    private void Select(Tile tile)
    {
        if (selected != null) selected.Selected = false;
        selected = tile;
        if (selected != null) selected.Selected = true;
        apply.IsEnabled = selected != null;
    }

    // Previews load in the background: draw the ones in view as they become ready
    private void LoadVisiblePreviews()
    {
        double top = scroller.Offset.Y, bottom = top + Math.Max(scroller.Viewport.Height, scroller.Bounds.Height);
        foreach (Tile tile in imageTiles)
        {
            if (tile.PreviewReady) continue;
            Rect r = tile.Bounds;
            if (r.Bottom < top || r.Top > bottom) continue;
            tile.UpdatePreview();
        }
    }

    private void Accept()
    {
        if (selected?.Image == null) return;
        SelectedName = ImageBrowserModel.DisplayName(selected.Image);
        Close(SelectedName);
    }

    #endregion

    private sealed class Tile : Border
    {
        private readonly Image picture = new Image { Width = TileSize, Height = TileSize, Stretch = Stretch.Uniform };
        private readonly TextBlock label;
        private bool selected;

        public ImageData Image { get; }
        public bool PreviewReady { get; private set; }
        public bool Used { get; set; }

        public Tile(ImageData image, string text, bool folder)
        {
            Image = image;
            Width = TileSize + 12;
            Margin = new Thickness(2);
            Padding = new Thickness(4);
            BorderThickness = new Thickness(2);
            BorderBrush = Brushes.Transparent;
            Cursor = new Cursor(StandardCursorType.Hand);
            label = new TextBlock { Text = text, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center };
            ToolTip.SetTip(this, text);

            var stack = new StackPanel();
            if (folder)
            {
                stack.Children.Add(new TextBlock { Text = "📁", FontSize = 48, Height = TileSize, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                PreviewReady = true;
            }
            else stack.Children.Add(picture);
            stack.Children.Add(label);
            Child = stack;
        }

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                BorderBrush = value ? Brushes.DodgerBlue : Brushes.Transparent;
                Background = value ? new SolidColorBrush(Colors.DodgerBlue, 0.25) : null;
            }
        }

        public void UpdatePreview()
        {
            if (Image.IsPreviewLoaded)
            {
                picture.Source = ImageConvert.ToAvalonia(Image.GetPreview());
                PreviewReady = true;
            }
            else if (picture.Source == null)
            {
                picture.Source = ImageConvert.ToAvalonia(Image.GetPreview()); // The "loading" placeholder
            }
            label.FontWeight = Used ? FontWeight.Bold : FontWeight.Normal;
        }
    }
}
