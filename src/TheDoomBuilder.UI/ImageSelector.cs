using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Data;

namespace DoomBuilder.UI;

/// <summary>
/// A preview of a texture or flat with its name underneath (UDB's ImageSelectorControl): left click browses, right click clears
/// to "-", and the name can be typed. <see cref="TextureSelector"/> and <see cref="FlatSelector"/> pick the kind.
/// </summary>
public abstract class ImageSelector : UserControl
{
    private readonly Image preview = new Image { Stretch = Stretch.Uniform };
    private readonly Border frame = new Border { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), Cursor = new Cursor(StandardCursorType.Hand) };
    private readonly TextBox name = new TextBox { MinHeight = 24, Padding = new Thickness(4, 2) };
    private readonly TextBlock size = new TextBlock { FontSize = 10, Foreground = Brushes.White, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2) };
    private readonly Button toggleFullName = new Button { Content = "↔", FontSize = 10, Padding = new Thickness(2, 0), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    private ImageData image;
    private string previousname;
    private bool pressedRight;
    private bool upper;     // Avalonia's TextBox has no character casing: names are upper-cased as they change

    /// <summary>Raised when the name changes (typed, browsed or cleared).</summary>
    public event EventHandler ValueChanged;

    /// <summary>True for the flats browser/list, false for wall textures.</summary>
    protected abstract bool BrowseFlats { get; }

    /// <summary>The image behind a name (never null: unknown names give the "unknown" image).</summary>
    protected abstract ImageData GetImageData(string imagename);

    /// <summary>Several things are selected with different values: shows the stack icon while empty.</summary>
    public bool MultipleTextures { get; set; }

    /// <summary>When set, an empty name shows the "missing texture" icon.</summary>
    public bool Required { get; set; }

    public string TextureName { get => name.Text ?? ""; set => name.Text = value; }

    protected ImageSelector()
    {
        var layers = new Grid();
        layers.Children.Add(preview);
        layers.Children.Add(size);
        layers.Children.Add(toggleFullName);
        frame.Child = layers;

        var stack = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(name, Dock.Bottom);
        stack.Children.Add(name);
        stack.Children.Add(frame);
        Content = stack;
        MinWidth = 64;
        MinHeight = 80;

        name.PropertyChanged += (s, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            string text = name.Text ?? "";
            if (upper && text != text.ToUpperInvariant()) { name.Text = text.ToUpperInvariant(); return; }
            Refresh();
        };
        frame.PointerPressed += (s, e) =>
        {
            PointerPointProperties p = e.GetCurrentPoint(frame).Properties;
            pressedRight = p.IsRightButtonPressed;
            if (!p.IsLeftButtonPressed && !pressedRight) return;
            if (pressedRight) name.Text = "-";
            else
            {
                string chosen = Browse(TextureName);
                upper = !IsLongName(chosen);
                name.Text = chosen;
            }
            e.Handled = true;
        };
        toggleFullName.Click += (s, e) => ToggleFullName();
        timer.Tick += (s, e) => Refresh();
        DetachedFromVisualTree += (s, e) => timer.Stop();
    }

    /// <summary>Call after the map is open: limits the name length and (for textures) fills in the preview.</summary>
    public virtual void Initialize()
    {
        name.MaxLength = General.Map.Config.MaxTextureNameLength;
        upper = !General.Map.Options.UseLongTextureNames;
        Refresh();
    }

    /// <summary>Stops the background refresh (when the dialog closes).</summary>
    public void StopUpdate() => timer.Stop();

    /// <summary>The name to apply: what was typed, or <paramref name="original"/> when it is empty.</summary>
    public string GetResult(string original) => name.Text != null && name.Text.Trim().Length > 0 ? name.Text : original;

    private string Browse(string current)
    {
        return ImageBrowserWindow.Browse(TopLevel.GetTopLevel(this) as Window, current, BrowseFlats) ?? current;
    }

    public void Refresh()
    {
        if (General.Map == null) return;
        timer.Stop();

        string imagename = TextureName;
        System.Drawing.Image shown = FindImage(imagename);
        preview.Source = ImageConvert.ToAvalonia(shown);
        ToolTip.SetTip(frame, imagename);

        if (ValueChanged != null && previousname != imagename)
        {
            previousname = imagename;
            ValueChanged(this, EventArgs.Empty);
        }
    }

    // Decides the image to show, the size label and the full-name toggle
    private System.Drawing.Image FindImage(string imagename)
    {
        if (string.IsNullOrEmpty(imagename) || imagename == "-")
        {
            DisplaySize(0, 0);
            UpdateToggle(null);
            if (string.IsNullOrEmpty(imagename) && MultipleTextures) return CodeImp.DoomBuilder.Properties.Resources.ImageStack;
            return (Required || BrowseFlats) ? CodeImp.DoomBuilder.Properties.Resources.MissingTexture : null;
        }

        ImageData texture = GetImageData(imagename);
        UpdateToggle(texture);
        if (string.IsNullOrEmpty(texture.FilePathName) || texture is UnknownImage) DisplaySize(0, 0);
        else DisplaySize(texture.ScaledWidth, texture.ScaledHeight);

        if (!texture.IsPreviewLoaded) timer.Start();
        return texture.GetPreview();
    }

    // A long name keeps its case; everything else is upper-case
    private bool IsLongName(string imagename)
    {
        if (!General.Map.Config.UseLongTextureNames || string.IsNullOrEmpty(imagename) || imagename == "-") return false;
        ImageData data = GetImageData(imagename);
        return data != null && data.HasLongName && string.Compare(imagename, data.ShortName, StringComparison.OrdinalIgnoreCase) != 0;
    }

    private void DisplaySize(float width, float height)
    {
        width = Math.Abs(width);
        height = Math.Abs(height);
        size.Text = width > 0 && height > 0 ? width + "x" + height : "";
        size.IsVisible = General.Settings.ShowTextureSizes && IsEnabled && size.Text.Length > 0;
    }

    private void UpdateToggle(ImageData data)
    {
        image = data;
        toggleFullName.IsVisible = General.Map.Config.UseLongTextureNames && data != null && data.HasLongName;
        if (toggleFullName.IsVisible)
        {
            bool isShort = string.Compare(data.ShortName, name.Text, StringComparison.OrdinalIgnoreCase) == 0;
            ToolTip.SetTip(toggleFullName, isShort ? "Switch to full name" : "Switch to short name");
        }
    }

    private void ToggleFullName()
    {
        if (image == null) return;
        bool isShort = string.Compare(name.Text, image.ShortName, StringComparison.OrdinalIgnoreCase) == 0;
        upper = !isShort;
        name.Text = isShort ? image.Name : image.ShortName;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty && General.Map != null)
            size.IsVisible = General.Settings.ShowTextureSizes && IsEnabled && !string.IsNullOrEmpty(size.Text);
    }
}

/// <summary>Selector of a wall texture.</summary>
public sealed class TextureSelector : ImageSelector
{
    protected override bool BrowseFlats => false;
    protected override ImageData GetImageData(string imagename) => General.Map.Data.GetTextureImage(imagename);
}

/// <summary>Selector of a flat.</summary>
public sealed class FlatSelector : ImageSelector
{
    protected override bool BrowseFlats => true;
    protected override ImageData GetImageData(string imagename) => General.Map.Data.GetFlatImage(imagename);
}
