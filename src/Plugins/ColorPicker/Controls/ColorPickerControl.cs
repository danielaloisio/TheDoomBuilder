// The color picker: the wheel, the numbers (RGB, hex or float) and the OK / Cancel buttons that show the new and the first color.
// UDB's ColorPickerControl as an Avalonia control; the logic of the numbers is the original's.
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvButton = Avalonia.Controls.Button;
using AvComboBox = Avalonia.Controls.ComboBox;

namespace CodeImp.DoomBuilder.ColorPicker.Controls
{
	public class ColorPickerControl : UserControl
	{
		private const string COLOR_INFO_RGB = "RGB";
		private const string COLOR_INFO_HEX = "Hex";
		private const string COLOR_INFO_FLOAT = "Float";
		private readonly object[] COLOR_INFO = new object[] { COLOR_INFO_RGB, COLOR_INFO_HEX, COLOR_INFO_FLOAT };

		private static int colorInfoMode;

		private readonly ColorWheelControl wheel = new ColorWheelControl();
		private readonly NumericUpDown nudRed = Nud(), nudGreen = Nud(), nudBlue = Nud();
		private readonly AvComboBox cbColorInfo = new AvComboBox { MinWidth = 94 };
		private readonly TextBox tbFloatVals = new TextBox { MinWidth = 94, IsVisible = false };
		private readonly Grid pRGB = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 3, ColumnSpacing = 6 };
		private readonly AvButton btnOK = new AvButton { Content = "OK", MinWidth = 94, MinHeight = 42, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsDefault = true };
		private readonly AvButton btnCancel = new AvButton { Content = "Cancel", MinWidth = 94, MinHeight = 42, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, IsCancel = true };

		private ColorHandler.RGB RGB;
		private bool isInUpdate;
		private System.Drawing.Color startColor;
		private bool announced;
		private bool announcing;
		private bool changed;           // the color was picked (or typed) already

		/// <summary>The color changed (from the wheel or from the numbers).</summary>
		public event EventHandler<ColorChangedEventArgs> OnColorChanged;
		public event EventHandler OnOkPressed;
		public event EventHandler OnCancelPressed;

		public ColorHandler.RGB CurrentColor { get { return RGB; } }
		public AvButton OkButton { get { return btnOK; } }
		public AvButton CancelButton { get { return btnCancel; } }

		// For the tests
		internal ColorWheelControl Wheel { get { return wheel; } }
		internal NumericUpDown RedBox { get { return nudRed; } }
		internal NumericUpDown GreenBox { get { return nudGreen; } }
		internal NumericUpDown BlueBox { get { return nudBlue; } }
		internal AvComboBox InfoBox { get { return cbColorInfo; } }
		internal TextBox TextValues { get { return tbFloatVals; } }

		private static NumericUpDown Nud()
		{
			return new NumericUpDown { Minimum = 0, Maximum = 255, Value = 0, Increment = 1, FormatString = "0", MinWidth = 80, ParsingNumberStyle = NumberStyles.Integer };
		}

		public ColorPickerControl()
		{
			string[] names = { "Red:", "Green:", "Blue:" };
			NumericUpDown[] boxes = { nudRed, nudGreen, nudBlue };
			for(int i = 0; i < 3; i++)
			{
				pRGB.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				var label = new TextBlock { Text = names[i], VerticalAlignment = VerticalAlignment.Center };
				Grid.SetRow(label, i);
				Grid.SetRow(boxes[i], i);
				Grid.SetColumn(boxes[i], 1);
				pRGB.Children.Add(label);
				pRGB.Children.Add(boxes[i]);
				boxes[i].ValueChanged += NudValueChanged;
			}

			var right = new StackPanel { Spacing = 4, Margin = new Thickness(6, 0, 0, 0) };
			right.Children.Add(btnOK);
			right.Children.Add(btnCancel);
			right.Children.Add(cbColorInfo);
			right.Children.Add(pRGB);
			right.Children.Add(tbFloatVals);
			var root = new DockPanel();
			DockPanel.SetDock(right, Dock.Right);
			root.Children.Add(right);
			root.Children.Add(wheel);
			Content = root;

			cbColorInfo.ItemsSource = COLOR_INFO;
			wheel.ColorChanged += (s, e) => SetRGB(e.RGB);
			btnOK.Click += (s, e) => { if(OnOkPressed != null) OnOkPressed(this, e); };
			btnCancel.Click += (s, e) => { if(OnCancelPressed != null) OnCancelPressed(this, e); };
			cbColorInfo.SelectionChanged += cbColorInfo_SelectedIndexChanged;
			tbFloatVals.PropertyChanged += (s, e) => { if(e.Property == TextBox.TextProperty) tbFloatVals_TextChanged(s, EventArgs.Empty); };

			// Like UDB's Load event: the color is shown (and announced) when the control shows up
			AttachedToVisualTree += (s, e) =>
			{
				if(announced) return;
				announced = true;
				if(!changed)            // (a color that was picked before the control showed up stays)
				{
					announcing = true;
					SetCurrentColor(startColor);
					announcing = false;
				}
				UpdateCancelButton(startColor.IsEmpty ? RGB : new ColorHandler.RGB(startColor.R, startColor.G, startColor.B));
			};
		}

		public void Initialize(System.Drawing.Color startColor)
		{
			this.startColor = startColor;
			isInUpdate = true;
			cbColorInfo.SelectedIndex = colorInfoMode;
			isInUpdate = false;
			ShowInfoMode();
			SetRGBQuietly(new ColorHandler.RGB(startColor.R, startColor.G, startColor.B));
			UpdateCancelButton(RGB);
		}

		// The numbers of the picked color, or the other way around
		private void SetRGBQuietly(ColorHandler.RGB color)
		{
			isInUpdate = true;
			RGB = color;
			wheel.SetColor(color);
			ShowColor(color);
			isInUpdate = false;
		}

		private void NudValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
		{
			if(!isInUpdate)
			{
				RGB = new ColorHandler.RGB((int)(nudRed.Value ?? 0), (int)(nudGreen.Value ?? 0), (int)(nudBlue.Value ?? 0));
				wheel.SetColor(RGB);
				UpdateColorInfo(RGB);
			}
		}

		// The wheel picked a color
		private void SetRGB(ColorHandler.RGB color)
		{
			isInUpdate = true;
			UpdateColorInfo(color);
			isInUpdate = false;
		}

		private static IBrush BrushOf(ColorHandler.RGB c) { return new SolidColorBrush(Color.FromRgb((byte)c.Red, (byte)c.Green, (byte)c.Blue)); }
		private static IBrush ForegroundFor(ColorHandler.RGB c) { return (c.Red < 180 && c.Green < 180) ? Brushes.White : Brushes.Black; }

		// Shows a color in the buttons and in the numbers
		private void ShowColor(ColorHandler.RGB color)
		{
			btnOK.Background = BrushOf(color);
			btnOK.Foreground = ForegroundFor(color);

			switch(cbColorInfo.SelectedItem == null ? COLOR_INFO_RGB : cbColorInfo.SelectedItem.ToString())
			{
				case COLOR_INFO_RGB:
					RefreshNudValue(nudRed, color.Red);
					RefreshNudValue(nudBlue, color.Blue);
					RefreshNudValue(nudGreen, color.Green);
					break;

				case COLOR_INFO_HEX:
					tbFloatVals.Text = color.Red.ToString("X02") + color.Green.ToString("X02") + color.Blue.ToString("X02");
					break;

				case COLOR_INFO_FLOAT:
					string r2 = ((float)Math.Round(color.Red / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture);
					string g2 = ((float)Math.Round(color.Green / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture);
					string b2 = ((float)Math.Round(color.Blue / 255f, 2)).ToString("F02", CultureInfo.InvariantCulture);
					tbFloatVals.Text = r2 + " " + g2 + " " + b2;
					break;
			}
		}

		// Shows the color and tells that it changed
		private void UpdateColorInfo(ColorHandler.RGB color)
		{
			this.RGB = color;
			if(!announcing) changed = true;
			bool before = isInUpdate;
			isInUpdate = true;
			ShowColor(color);
			isInUpdate = before;

			if(OnColorChanged != null) OnColorChanged(this, new ColorChangedEventArgs(color, ColorHandler.RGBtoHSV(color)));
		}

		private void UpdateCancelButton(ColorHandler.RGB color)
		{
			btnCancel.Background = BrushOf(color);
			btnCancel.Foreground = ForegroundFor(color);
		}

		private static void RefreshNudValue(NumericUpDown nud, int value)
		{
			if((int)(nud.Value ?? 0) != value) nud.Value = value;
		}

		public void SetCurrentColor(System.Drawing.Color c)
		{
			isInUpdate = true;
			RGB = new ColorHandler.RGB(c.R, c.G, c.B);
			wheel.SetColor(RGB);
			UpdateColorInfo(RGB);
			isInUpdate = false;
		}

		public void SetInitialColor(System.Drawing.Color c)
		{
			UpdateCancelButton(new ColorHandler.RGB(c.R, c.G, c.B));
		}

		private void ShowInfoMode()
		{
			bool rgbmode = cbColorInfo.SelectedItem != null && cbColorInfo.SelectedItem.ToString() == COLOR_INFO_RGB;
			pRGB.IsVisible = rgbmode;
			tbFloatVals.IsVisible = !rgbmode;
		}

		private void cbColorInfo_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
		{
			if(cbColorInfo.SelectedItem == null) return;
			ShowInfoMode();
			colorInfoMode = cbColorInfo.SelectedIndex;
			if(!isInUpdate) UpdateColorInfo(RGB);
		}

		private void tbFloatVals_TextChanged(object sender, EventArgs e)
		{
			if(isInUpdate) return;
			string text = tbFloatVals.Text ?? "";

			if(COLOR_INFO[colorInfoMode].ToString() == COLOR_INFO_FLOAT)
			{
				string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if(parts.Length != 3) return;

				ColorHandler.RGB rgb = new ColorHandler.RGB();
				float c;
				if(!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out c)) return;
				rgb.Red = (int)(General.Clamp(Math.Abs(c), 0.0f, 1.0f) * 255);
				if(!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out c)) return;
				rgb.Green = (int)(General.Clamp(Math.Abs(c), 0.0f, 1.0f) * 255);
				if(!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out c)) return;
				rgb.Blue = (int)(General.Clamp(Math.Abs(c), 0.0f, 1.0f) * 255);

				wheel.SetColor(rgb);
				UpdateColorInfo(rgb);
			}
			else if(COLOR_INFO[colorInfoMode].ToString() == COLOR_INFO_HEX)
			{
				string hexColor = text.Trim().Replace("-", "");
				if(hexColor.Length != 6) return;

				ColorHandler.RGB rgb = new ColorHandler.RGB();
				int color;
				if(!int.TryParse(hexColor.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color)) return;
				rgb.Red = color;
				if(!int.TryParse(hexColor.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color)) return;
				rgb.Green = color;
				if(!int.TryParse(hexColor.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color)) return;
				rgb.Blue = color;

				wheel.SetColor(rgb);
				UpdateColorInfo(rgb);
			}
		}
	}
}
