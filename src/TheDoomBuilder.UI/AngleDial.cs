using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace DoomBuilder.UI;

/// <summary>
/// A dial for an angle in degrees (UDB's AngleControlEx): 0 points east and angles grow counter-clockwise, like in the map.
/// Click or drag to set it; with <see cref="DoomAngleClamping"/> only multiples of 45 are allowed.
/// </summary>
public sealed class AngleDial : Control
{
    /// <summary>Shown when the things being edited have different angles.</summary>
    public const int NoAngle = int.MinValue;

    private int angle = NoAngle;

    public event EventHandler AngleChanged;

    public bool DoomAngleClamping { get; set; }

    public int Angle
    {
        get => angle;
        set
        {
            if (value != NoAngle) value = ((value % 360) + 360) % 360;
            if (angle == value) return;
            angle = value;
            InvalidateVisual();
        }
    }

    public AngleDial()
    {
        Width = 64;
        Height = 64;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        SetFrom(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured == this) SetFrom(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this) e.Pointer.Capture(null);
    }

    private void SetFrom(Point p)
    {
        double dx = p.X - Bounds.Width / 2, dy = -(p.Y - Bounds.Height / 2);
        if (dx == 0 && dy == 0) return;
        int degrees = (int)Math.Round(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        if (DoomAngleClamping) degrees = (int)Math.Round(degrees / 45.0) * 45;
        degrees = ((degrees % 360) + 360) % 360;
        if (degrees == angle) return;
        angle = degrees;
        InvalidateVisual();
        AngleChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(DrawingContext context)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double radius = size / 2 - 3;
        IBrush fore = IsEnabled ? Brushes.Gray : Brushes.DarkGray;

        context.DrawEllipse(null, new Pen(fore, 1.5), center, radius, radius);
        for (int a = 0; a < 360; a += 45)
        {
            double r = a * Math.PI / 180;
            context.DrawLine(new Pen(fore, 1), center + new Vector(Math.Cos(r), -Math.Sin(r)) * (radius - 4), center + new Vector(Math.Cos(r), -Math.Sin(r)) * radius);
        }

        if (angle == NoAngle) return;
        double rad = angle * Math.PI / 180;
        var tip = center + new Vector(Math.Cos(rad), -Math.Sin(rad)) * (radius - 2);
        IBrush hand = IsEnabled ? Brushes.DodgerBlue : Brushes.DarkGray;
        context.DrawLine(new Pen(hand, 2.5), center, tip);
        context.DrawEllipse(hand, null, tip, 3.5, 3.5);
    }
}
