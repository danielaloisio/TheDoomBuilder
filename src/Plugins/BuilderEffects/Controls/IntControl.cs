// A whole number with a label, a slider and the largest value: UDB's IntControl. OnValueChanging comes with every change of the number (the
// forms apply the effect right away), OnValueChanged when the user lets go of the slider or leaves the number (what UDB did on MouseLeave).
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodeImp.DoomBuilder.BuilderEffects
{
	internal class IntControl : UserControl
	{
		public event EventHandler OnValueChanging;
		public event EventHandler OnValueChanged;

		private readonly TextBlock label1 = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 60 };
		private readonly NumericUpDown numericUpDown1 = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Increment = 1, FormatString = "0", MinWidth = 100, ParsingNumberStyle = System.Globalization.NumberStyles.Integer };
		private readonly Slider trackBar1 = new Slider { Minimum = 0, Maximum = 100, MinWidth = 120, TickFrequency = 1, IsSnapToTickEnabled = true };
		private readonly TextBlock labelMaximum = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = "0" };

		private int previousValue;
		private int delta;
		private int minimum;
		private int maximum = 100;
		private bool allowNegative;
		private bool extendedLimits;
		private bool blockEvents;
		private bool valueChanged;

		public bool Enabled { get { return IsEnabled; } set { IsEnabled = value; } }

		// For the tests
		internal NumericUpDown Number { get { return numericUpDown1; } }
		internal Slider TrackBar { get { return trackBar1; } }
		internal void CommitChange() { Commit(); }

		public int Value
		{
			get { return (int)(numericUpDown1.Value ?? 0); }
			set
			{
				blockEvents = true;
				previousValue = General.Clamp(value, (int)numericUpDown1.Minimum, (int)numericUpDown1.Maximum);
				numericUpDown1.Value = previousValue;
				trackBar1.Value = previousValue;
				valueChanged = false;
				blockEvents = false;
			}
		}

		internal int Delta { get { return delta; } }

		public string Label { get { return label1.Text; } set { label1.Text = value; } }

		public bool ExtendedLimits
		{
			get { return extendedLimits; }
			set { extendedLimits = value; UpdateLimits(); }
		}

		public bool AllowNegative
		{
			get { return allowNegative; }
			set
			{
				allowNegative = value;
				if(!allowNegative)
				{
					if(minimum < 0 && maximum < 0)
					{
						int diff = Math.Abs(maximum - minimum);
						minimum = 0;
						maximum = diff;
					}
					else
					{
						if(minimum < 0) minimum = 0;
						if(maximum < 0) maximum = 0;
					}
				}
				UpdateLimits();
			}
		}

		public int Minimum { get { return minimum; } set { minimum = value; UpdateLimits(); } }
		public int Maximum { get { return maximum; } set { maximum = value; UpdateLimits(); } }

		public IntControl()
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			row.Children.Add(label1);
			row.Children.Add(numericUpDown1);
			row.Children.Add(trackBar1);
			row.Children.Add(labelMaximum);
			Content = row;

			numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
			trackBar1.PropertyChanged += (s, e) => { if(e.Property == Slider.ValueProperty && !blockEvents) numericUpDown1.Value = (decimal)Math.Round(trackBar1.Value); };
			trackBar1.PointerCaptureLost += (s, e) => Commit();
			numericUpDown1.LostFocus += (s, e) => Commit();
			numericUpDown1.PointerExited += (s, e) => Commit();
			UpdateLimits();
		}

		private void UpdateLimits()
		{
			blockEvents = true;
			trackBar1.Value = General.Clamp((int)trackBar1.Value, minimum, maximum);
			trackBar1.Minimum = minimum;
			trackBar1.Maximum = Math.Max(maximum, minimum + 1);
			labelMaximum.Text = maximum.ToString();
			int value = General.Clamp((int)(numericUpDown1.Value ?? 0), minimum, maximum);
			if(extendedLimits)
			{
				numericUpDown1.Minimum = minimum * 32;
				numericUpDown1.Maximum = maximum * 32;
			}
			else
			{
				numericUpDown1.Minimum = minimum;
				numericUpDown1.Maximum = maximum;
			}
			numericUpDown1.Value = value;
			blockEvents = false;
		}

		private void numericUpDown1_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
		{
			int value = (int)(numericUpDown1.Value ?? 0);
			if(value == previousValue) return;

			valueChanged = true;
			delta = value - previousValue;
			previousValue = value;

			if(!blockEvents && OnValueChanging != null)
				OnValueChanging(this, EventArgs.Empty);

			blockEvents = true;
			trackBar1.Value = General.Clamp(value, (int)trackBar1.Minimum, (int)trackBar1.Maximum);
			blockEvents = false;
		}

		private void Commit()
		{
			if(valueChanged && OnValueChanged != null)
			{
				valueChanged = false;
				OnValueChanged(this, EventArgs.Empty);
			}
		}
	}
}
