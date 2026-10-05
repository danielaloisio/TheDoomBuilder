using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The strip under the map that describes the element under the mouse (or aimed at, in 3D): UDB's linedef, sector, thing and
/// vertex info panels. It draws an <see cref="ElementInfo"/>: a framed group per part (the element, a sidedef, the floor...),
/// texture thumbnails with their names, and the flags that are set. Thumbnails load in the background, so it refreshes itself
/// until they are ready.
/// </summary>
public sealed class InfoPanel : UserControl
{
    public const double PanelHeight = 150;

    private readonly StackPanel cards = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(6, 4) };
    private readonly TextBlock idle = new TextBlock { Opacity = 0.6, Margin = new Thickness(10, 8), VerticalAlignment = VerticalAlignment.Top };
    private readonly DispatcherTimer reload = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    private ElementInfo shown;

    /// <summary>The info being shown, or null.</summary>
    public ElementInfo Shown => shown;

    /// <summary>The groups drawn now (tests read these).</summary>
    public IReadOnlyList<Control> Cards => cards.Children;

    public InfoPanel()
    {
        Height = PanelHeight;
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = cards,
        };
        var layer = new Grid();
        layer.Children.Add(idle);
        layer.Children.Add(scroll);
        Content = new Border { BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Brushes.Gray, Child = layer };

        reload.Tick += (s, e) => { if (shown != null && !shown.IsComplete()) Show(shown); else reload.Stop(); };
        DetachedFromVisualTree += (s, e) => reload.Stop();
    }

    /// <summary>What to say while no element is highlighted (the name of the mode).</summary>
    public string IdleText { get => idle.Text ?? ""; set { idle.Text = value; } }

    /// <summary>Shows <paramref name="info"/>; null clears the panel.</summary>
    public void Show(ElementInfo info)
    {
        shown = info;
        cards.Children.Clear();
        idle.IsVisible = info == null;
        if (info == null) { reload.Stop(); return; }

        foreach (InfoGroup group in info.Groups) cards.Children.Add(Card(group, group == info.Groups[0] ? info.Angle : -1));
        if (info.Sprite != null) cards.Children.Add(SpriteCard(info));
        if (info.Flags.Count > 0) cards.Children.Add(FlagsCard(info));

        if (!info.IsComplete()) reload.Start(); else reload.Stop();
    }

    #region ================== Cards

    // (a Foreground set to null would hide the text: it is only set when there is a color to give)
    private static TextBlock Text(string text, double size, IBrush foreground = null, FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight };
        if (foreground != null) block.Foreground = foreground;
        return block;
    }

    private static Control Frame(string title, bool highlight, Control content)
    {
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(Text(title, 14, highlight ? Brushes.DodgerBlue : null, FontWeight.SemiBold));
        stack.Children.Add(content);
        return new Border { BorderThickness = new Thickness(1), BorderBrush = highlight ? Brushes.DodgerBlue : Brushes.Gray, CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 3), Child = stack };
    }

    private static Control Card(InfoGroup group, int angle)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (group.Fields.Count > 0) row.Children.Add(Fields(group.Fields));
        if (angle >= 0) row.Children.Add(AngleDial(angle));
        foreach (InfoTexture texture in group.Textures) row.Children.Add(Texture(texture));
        return Frame(group.Title, group.Highlight, row);
    }

    private static Control Fields(List<InfoField> fields)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 8 };
        int row = 0;
        foreach (InfoField f in fields)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var brush = f.Error ? Brushes.DarkRed : (f.Highlight ? Brushes.DodgerBlue : null);
            TextBlock label = Text(f.Label, 12, brush);
            label.Opacity = f.Enabled ? 0.75 : 0.35;
            label.HorizontalAlignment = HorizontalAlignment.Right;
            TextBlock valuetext = Text(f.Value, 12, brush);
            valuetext.Opacity = f.Enabled ? 1.0 : 0.35;
            valuetext.TextTrimming = TextTrimming.CharacterEllipsis;
            valuetext.MaxWidth = 260;
            Control value = valuetext;
            if (f.Color.HasValue)
            {
                var swatch = new Border { Width = 22, Height = 12, Background = new SolidColorBrush(Color.FromUInt32((uint)f.Color.Value)), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
                value = swatch;
            }
            Grid.SetRow(label, row);
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
            row++;
        }
        return grid;
    }

    private static Control Texture(InfoTexture t)
    {
        var picture = new Grid { Width = 64, Height = 64 };
        picture.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) });

        System.Drawing.Image image = null;
        if (t.Missing) image = CodeImp.DoomBuilder.Properties.Resources.MissingTexture;
        else if (t.Image != null) image = t.Image.GetPreview();
        if (image != null) picture.Children.Add(new Image { Source = ImageConvert.ToAvalonia(image), Stretch = Stretch.Uniform });

        if (t.SizeText.Length > 0)
            picture.Children.Add(new TextBlock { Text = t.SizeText, FontSize = 9, Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

        var stack = new StackPanel { Spacing = 2, Width = Math.Max(64, 0) };
        stack.Children.Add(new TextBlock { Text = t.Caption, FontSize = 10, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(picture);
        TextBlock name = Text(t.Name, 11, t.Highlight ? Brushes.DodgerBlue : null);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(name);
        if (t.Fields.Count > 0) stack.Children.Add(Fields(t.Fields));
        return stack;
    }

    /// <summary>A small dial showing a thing's angle (Doom angles run counter-clockwise from east).</summary>
    private static Control AngleDial(int degrees)
    {
        const double size = 44, radius = 18;
        double radians = degrees * Math.PI / 180.0;
        var canvas = new Canvas { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Top };
        canvas.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Width = radius * 2, Height = radius * 2, Stroke = Brushes.Gray, StrokeThickness = 1, [Canvas.LeftProperty] = size / 2 - radius, [Canvas.TopProperty] = size / 2 - radius });
        canvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Point(size / 2, size / 2),
            EndPoint = new Point(size / 2 + radius * Math.Cos(radians), size / 2 - radius * Math.Sin(radians)),
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 2,
        });
        return canvas;
    }

    private static Control SpriteCard(ElementInfo info)
    {
        var stack = new StackPanel { Spacing = 2 };
        System.Drawing.Image image = info.SpriteIsInternal ? info.Sprite.GetSpritePreview() : info.Sprite.GetPreview();
        var picture = new Grid { Width = 80, Height = 80 };
        picture.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) });
        if (image != null) picture.Children.Add(new Image { Source = ImageConvert.ToAvalonia(image), Stretch = Stretch.Uniform });
        stack.Children.Add(picture);
        if (info.SpriteName.Length > 0) stack.Children.Add(new TextBlock { Text = info.SpriteName, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
        return new Border { Padding = new Thickness(2), Child = stack };
    }

    private static Control FlagsCard(ElementInfo info)
    {
        // The flags run down columns, as many as fit the panel's height
        var panel = new WrapPanel { Orientation = Orientation.Vertical, MaxHeight = PanelHeight - 44 };
        foreach (string flag in info.Flags)
            panel.Children.Add(new TextBlock { Text = "☑ " + flag, FontSize = 12, Margin = new Thickness(0, 0, 14, 0) });
        return Frame("Flags", false, panel);
    }

    #endregion
}
