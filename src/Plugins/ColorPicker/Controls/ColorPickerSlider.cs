// A number with a label, and either a slider (with its limits) or four buttons for the usual values (64, 128, 256, 512).
// UDB's ColorPickerSlider as an Avalonia control.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.ColorPicker.Controls
{
	public class ColorPickerSlider : UserControl
	{
		private readonly TextBlock label1 = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 70 };
		private readonly NumericUpDown numericUpDown1 = new NumericUpDown { Minimum = 0, Maximum = 16384, Value = 0, Increment = 1, FormatString = "0", MinWidth = 100, ParsingNumberStyle = System.Globalization.NumberStyles.Integer };
		private readonly Slider trackBar1 = new Slider { Minimum = 0, Maximum = 512, MinWidth = 160, IsVisible = false, TickFrequency = 1, IsSnapToTickEnabled = true };
		private readonly TextBlock labelMin = new TextBlock { IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
		private readonly TextBlock labelMax = new TextBlock { IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
		private readonly AvButton[] presets = new AvButton[4];
		private bool blockEvents;
		private bool showLimits;

		public event EventHandler<ColorPickerSliderEventArgs> OnValueChanged;

		public int Value
		{
			get { return (int)(numericUpDown1.Value ?? 0); }
			set
			{
				blockEvents = true;
				numericUpDown1.Value = General.Clamp(value, (int)numericUpDown1.Minimum, (int)numericUpDown1.Maximum);
				blockEvents = false;
			}
		}

		public bool ShowLimits
		{
			get { return showLimits; }
			set
			{
				showLimits = value;
				labelMin.IsVisible = showLimits;
				labelMax.IsVisible = showLimits;
			}
		}

		public string Label { set { label1.Text = value; } }

		// For the tests
		internal NumericUpDown Number { get { return numericUpDown1; } }
		internal Slider Slider { get { return trackBar1; } }
		internal AvButton[] Presets { get { return presets; } }
		internal TextBlock MinLabel { get { return labelMin; } }
		internal TextBlock MaxLabel { get { return labelMax; } }

		public ColorPickerSlider()
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2) };
			row.Children.Add(label1);
			row.Children.Add(numericUpDown1);
			row.Children.Add(labelMin);
			row.Children.Add(trackBar1);
			row.Children.Add(labelMax);
			int[] values = { 64, 128, 256, 512 };
			for(int i = 0; i < 4; i++)
			{
				int v = values[i];
				presets[i] = new AvButton { Content = v.ToString(), MinWidth = 40, HorizontalContentAlignment = HorizontalAlignment.Center };
				presets[i].Click += (s, e) => numericUpDown1.Value = v;
				row.Children.Add(presets[i]);
			}
			Content = row;

			numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
			trackBar1.PropertyChanged += (s, e) =>
			{
				// The slider moves the number
				if(e.Property == Slider.ValueProperty && !blockEvents) numericUpDown1.Value = (decimal)trackBar1.Value;
			};
			ShowLimits = false;
		}

		public void SetLimits(int tbMin, int tbMax, int nudMin, int nudMax)
		{
			bool blockEventsStatus = blockEvents;
			blockEvents = true;
			trackBar1.Minimum = tbMin;
			trackBar1.Maximum = tbMax;
			trackBar1.Value = General.Clamp((int)trackBar1.Value, tbMin, tbMax);
			labelMin.Text = tbMin.ToString();
			labelMax.Text = tbMax.ToString();
			int value = General.Clamp((int)(numericUpDown1.Value ?? 0), nudMin, nudMax);
			numericUpDown1.Minimum = nudMin;
			numericUpDown1.Maximum = nudMax;
			numericUpDown1.Value = value;
			blockEvents = blockEventsStatus;
		}

		public void UseSlider(bool use)
		{
			ShowLimits = use;
			trackBar1.IsVisible = use;
			foreach(AvButton b in presets) b.IsVisible = !use;
		}

		private void numericUpDown1_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
		{
			bool blockEventsStatus = blockEvents;
			int val = (int)(numericUpDown1.Value ?? 0);

			if(!blockEventsStatus)
			{
				if(OnValueChanged != null) OnValueChanged(this, new ColorPickerSliderEventArgs(val));
			}

			blockEvents = true;
			trackBar1.Value = General.Clamp(val, (int)trackBar1.Minimum, (int)trackBar1.Maximum);
			blockEvents = blockEventsStatus;
		}
	}
}
