using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CodeImp.DoomBuilder.Rendering;

namespace DoomBuilder.UI;

/// <summary>
/// A color to edit, as UDB's ColorControl: a swatch and the hex code (#RRGGBB). Avalonia has no color picker in its base package, so the code is typed;
/// half typed codes are ignored until they are complete.
/// </summary>
public sealed class ColorField : UserControl
{
    private readonly Border swatch = new Border { Width = 28, Height = 22, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Margin = new Thickness(0, 0, 6, 0) };
    private readonly TextBox hex = new TextBox { MinWidth = 90, MaxLength = 7 };
    private PixelColor color;
    private bool setting;

    /// <summary>The color changed (typed or set).</summary>
    public event EventHandler ColorChanged;

    public TextBox HexBox => hex;

    public PixelColor Color
    {
        get => color;
        set { color = value; Show(); ColorChanged?.Invoke(this, EventArgs.Empty); }
    }

    public ColorField()
    {
        var row = new DockPanel();
        DockPanel.SetDock(swatch, Dock.Left);
        row.Children.Add(swatch);
        row.Children.Add(hex);
        Content = row;
        hex.PropertyChanged += (s, e) =>
        {
            if (setting || e.Property != TextBox.TextProperty || !TryParseHex(hex.Text, out PixelColor typed)) return;
            color = typed;
            swatch.Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(color.r, color.g, color.b));
            ColorChanged?.Invoke(this, EventArgs.Empty);
        };
        Show();
    }

    private void Show()
    {
        setting = true;
        hex.Text = HexOf(color);
        setting = false;
        swatch.Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(color.r, color.g, color.b));
    }

    public static string HexOf(PixelColor c) => "#" + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");

    public static bool TryParseHex(string text, out PixelColor result)
    {
        result = new PixelColor();
        string digits = (text ?? "").Trim().TrimStart('#');
        if (digits.Length != 6 || !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
        result = new PixelColor(255, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        return true;
    }
}
