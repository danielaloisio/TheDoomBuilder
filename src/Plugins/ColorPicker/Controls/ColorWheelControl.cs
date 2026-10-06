// The color wheel of the color picker: hue and saturation on a disk, brightness on a bar next to it. The picking (what a click or a drag
// means, with HSV values from 0 to 255) is UDB's ColorWheel; the drawing is Avalonia's (a bitmap of the disk, redone when the brightness changes).
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CodeImp.DoomBuilder.ColorPicker.Controls
{
	public sealed class ColorWheelControl : Control
	{
		#region ================== Constants / Variables

		private const float DEGREES_PER_RADIAN = 180.0f / (float)Math.PI;
		public const int WheelSize = 176;
		public const int BarWidth = 16;
		private const int BarLeft = 185;

		private enum MouseState { MouseUp, Color, Brightness, Outside }

		private MouseState state = MouseState.MouseUp;
		private readonly System.Drawing.Point centerPoint = new System.Drawing.Point(WheelSize / 2, WheelSize / 2);
		private readonly int radius = WheelSize / 2;
		private ColorHandler.RGB rgb;
		private ColorHandler.HSV hsv;
		private System.Drawing.Point colorPoint;
		private System.Drawing.Point brightnessPoint;
		private WriteableBitmap disk;
		private int diskvalue = -1;

		public event EventHandler<ColorChangedEventArgs> ColorChanged;

		public ColorHandler.RGB RGB { get { return rgb; } }
		public ColorHandler.HSV HSV { get { return hsv; } }
		internal System.Drawing.Point ColorPoint { get { return colorPoint; } }
		internal System.Drawing.Point BrightnessPoint { get { return brightnessPoint; } }

		#endregion

		public ColorWheelControl()
		{
			Width = BarLeft + BarWidth + 8;
			Height = WheelSize;
			Cursor = new Cursor(StandardCursorType.Cross);
			colorPoint = centerPoint;
			brightnessPoint = new System.Drawing.Point(BarLeft + BarWidth, WheelSize);
		}

		#region ================== Picking

		private static int CalcDegrees(System.Drawing.Point pt)
		{
			int degrees;
			if(pt.X == 0)
			{
				degrees = pt.Y > 0 ? 270 : 90;
			}
			else
			{
				degrees = (int)(-Math.Atan((float)pt.Y / pt.X) * DEGREES_PER_RADIAN);
				if(pt.X < 0) degrees += 180;
				degrees = (degrees + 360) % 360;
			}
			return degrees;
		}

		private static System.Drawing.Point GetPoint(float degrees, float radius, System.Drawing.Point center)
		{
			float radians = degrees / DEGREES_PER_RADIAN;
			return new System.Drawing.Point((int)(center.X + Math.Floor(radius * Math.Cos(radians))), (int)(center.Y - Math.Floor(radius * Math.Sin(radians))));
		}

		// Shows a color (from the numbers, not from the mouse): moves the pointers and does not raise an event
		public void SetColor(ColorHandler.RGB color)
		{
			hsv = ColorHandler.RGBtoHSV(color);
			rgb = color;
			colorPoint = GetPoint((float)hsv.Hue / 255 * 360, (float)hsv.Saturation / 255 * radius, centerPoint);
			brightnessPoint = new System.Drawing.Point(BarLeft + BarWidth, (int)(WheelSize - hsv.value / (255.0f / WheelSize)));
			InvalidateVisual();
		}

		// What is under a point of the control: the disk, the brightness bar (with room for its pointer) or nothing
		private MouseState Region(Point p)
		{
			double dx = p.X - centerPoint.X, dy = p.Y - centerPoint.Y;
			if(dx * dx + dy * dy <= radius * radius) return MouseState.Color;
			if(p.X >= BarLeft && p.X <= BarLeft + BarWidth + 10 && p.Y >= -10 && p.Y <= WheelSize + 10) return MouseState.Brightness;
			return MouseState.Outside;
		}

		/// <summary>The button went down on a point: what is under it is picked and stays the target until the button is released.</summary>
		internal void Press(Point p)
		{
			state = Region(p);
			Apply(p, false);
		}

		/// <summary>The mouse moved with the button down.</summary>
		internal void Drag(Point p) { Apply(p, true); }

		internal void Release() { state = MouseState.MouseUp; }

		private void Apply(Point point, bool drag)
		{
			System.Drawing.Point mouse = new System.Drawing.Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
			switch(state)
			{
				case MouseState.Brightness:
				{
					int y = Math.Max(0, Math.Min(WheelSize, mouse.Y));
					brightnessPoint = new System.Drawing.Point(BarLeft + BarWidth, y);
					hsv.value = (int)((WheelSize - y) * (255.0f / WheelSize));
					rgb = ColorHandler.HSVtoRGB(hsv);
					break;
				}
				case MouseState.Color:
				{
					System.Drawing.Point newcolorpoint = mouse;
					System.Drawing.Point delta = new System.Drawing.Point(mouse.X - centerPoint.X, mouse.Y - centerPoint.Y);
					int degrees = CalcDegrees(delta);
					float distance = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) / radius;

					// Dragging out of the disk: the pointer stays on its edge
					if(drag && distance > 1)
					{
						distance = 1;
						newcolorpoint = GetPoint(degrees, radius, centerPoint);
					}

					colorPoint = newcolorpoint;
					hsv.Hue = degrees * 255 / 360;
					hsv.Saturation = (int)(distance * 255);
					rgb = ColorHandler.HSVtoRGB(hsv);
					break;
				}
				default:
					return;
			}

			InvalidateVisual();
			if(ColorChanged != null) ColorChanged(this, new ColorChangedEventArgs(rgb, hsv));
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
			e.Pointer.Capture(this);
			Press(e.GetPosition(this));
			e.Handled = true;
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			if(state != MouseState.MouseUp) Drag(e.GetPosition(this));
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			e.Pointer.Capture(null);
			Release();
		}

		#endregion

		#region ================== Drawing

		// The disk at a brightness: hue around, saturation outwards
		private WriteableBitmap MakeDisk(int value)
		{
			var bitmap = new WriteableBitmap(new PixelSize(WheelSize, WheelSize), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
			byte[] pixels = new byte[WheelSize * WheelSize * 4];
			for(int y = 0; y < WheelSize; y++)
			{
				for(int x = 0; x < WheelSize; x++)
				{
					System.Drawing.Point delta = new System.Drawing.Point(x - centerPoint.X, y - centerPoint.Y);
					float distance = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) / radius;
					if(distance > 1) continue;            // outside: transparent

					ColorHandler.RGB c = ColorHandler.HSVtoRGB(CalcDegrees(delta) * 255 / 360, (int)(distance * 255), value);
					int o = (y * WheelSize + x) * 4;
					pixels[o] = (byte)c.Blue; pixels[o + 1] = (byte)c.Green; pixels[o + 2] = (byte)c.Red; pixels[o + 3] = 255;
				}
			}
			using(var fb = bitmap.Lock())
				System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
			return bitmap;
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			if(disk == null || diskvalue != hsv.value)
			{
				disk = MakeDisk(hsv.value);
				diskvalue = hsv.value;
			}
			context.DrawImage(disk, new Rect(0, 0, WheelSize, WheelSize));

			// The bar: the color at full brightness down to black
			ColorHandler.RGB full = ColorHandler.HSVtoRGB(hsv.Hue, hsv.Saturation, 255);
			var brush = new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
				EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
				GradientStops = { new GradientStop(Color.FromRgb((byte)full.Red, (byte)full.Green, (byte)full.Blue), 0), new GradientStop(Colors.Black, 1) },
			};
			context.DrawRectangle(brush, null, new Rect(BarLeft, 0, BarWidth, WheelSize));

			// The pointers
			context.DrawRectangle(null, new Pen(Brushes.Black, 1), new Rect(colorPoint.X - 3, colorPoint.Y - 3, 6, 6));
			var triangle = new StreamGeometry();
			using(var g = triangle.Open())
			{
				g.BeginFigure(new Point(brightnessPoint.X, brightnessPoint.Y), true);
				g.LineTo(new Point(brightnessPoint.X + 7, brightnessPoint.Y + 5));
				g.LineTo(new Point(brightnessPoint.X + 7, brightnessPoint.Y - 5));
				g.EndFigure(true);
			}
			context.DrawGeometry(Brushes.Black, null, triangle);
		}

		#endregion
	}
}
