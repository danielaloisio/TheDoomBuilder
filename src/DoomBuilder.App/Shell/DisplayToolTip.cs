using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DoomBuilder.App.Shell;

/// <summary>
/// The tooltip a mode asks for over the map display (UDB's RenderTargetControl.ShowToolTip: a title and a text next to the mouse).
/// It floats over the display's input surface, never takes the mouse, and is moved to stay inside that surface.
/// </summary>
internal sealed class DisplayToolTip : Border
{
    private readonly TextBlock title = new TextBlock { FontWeight = FontWeight.SemiBold, Foreground = Brushes.White };
    private readonly TextBlock text = new TextBlock { Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };

    public string Title => title.Text;
    public string Text => text.Text;

    public DisplayToolTip()
    {
        IsHitTestVisible = false;
        IsVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x26, 0x26, 0x2B));
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x88));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(3);
        Padding = new Thickness(8, 5);
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(title);
        stack.Children.Add(text);
        Child = stack;
    }

    /// <summary>Shows the tip with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>) device pixels of the display.</summary>
    /// <param name="scale">Device pixels per layout unit (the window's render scaling).</param>
    /// <param name="area">The size of the display in layout units; the tip is kept inside it.</param>
    public void ShowAt(string titletext, string bodytext, int x, int y, double scale, Size area)
    {
        title.Text = titletext ?? "";
        title.IsVisible = title.Text.Length > 0;
        text.Text = bodytext ?? "";
        text.IsVisible = text.Text.Length > 0;
        if (!title.IsVisible && !text.IsVisible) { Hide(); return; }

        if (scale <= 0) scale = 1.0;
        Margin = default;                  // the margin is the position: it must not count in the size being measured
        Measure(new Size(Math.Max(1, area.Width), Math.Max(1, area.Height)));
        double left = x / scale, top = y / scale;
        if (area.Width > 0) left = Math.Max(0, Math.Min(left, area.Width - DesiredSize.Width));
        if (area.Height > 0) top = Math.Max(0, Math.Min(top, area.Height - DesiredSize.Height));
        Margin = new Thickness(left, top, 0, 0);
        IsVisible = true;
    }

    public void Hide() => IsVisible = false;
}
