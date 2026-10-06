// The options window of the Bridge mode: UDB's BridgeModeForm as a modeless Avalonia window, with the same members.
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.BuilderModes.ClassicModes;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.BuilderModes.Interface
{
	internal struct BridgeInterpolationMode
	{
		public const string BRIGHTNESS_HIGHEST = "Use highest";
		public const string BRIGHTNESS_LOWEST = "Use lowest";
		public const string HIGHEST = "Highest ceiling";
		public const string LOWEST = "Lowest floor";
		public const string LINEAR = "Linear interpolation";
		public const string IN_SINE = "EaseInSine interpolation";
		public const string OUT_SINE = "EaseOutSine interpolation";
		public const string IN_OUT_SINE = "EaseInOutSine interpolation";

		public static readonly string[] CEILING_INTERPOLATION_MODES = { LINEAR, HIGHEST, IN_SINE, OUT_SINE, IN_OUT_SINE };
		public static readonly string[] FLOOR_INTERPOLATION_MODES = { LINEAR, LOWEST, IN_SINE, OUT_SINE, IN_OUT_SINE };
		public static readonly string[] BRIGHTNESS_INTERPOLATION_MODES = { LINEAR, BRIGHTNESS_HIGHEST, BRIGHTNESS_LOWEST };
	}

	internal class BridgeModeForm : IDisposable
	{
		private readonly Window window;
		private readonly NumericUpDown subdivisions = new NumericUpDown { Minimum = BridgeMode.MIN_SUBDIVISIONS, Maximum = BridgeMode.MAX_SUBDIVISIONS, Value = 5, Increment = 1, FormatString = "0", MinWidth = 90 };
		private readonly ComboBox floor = new ComboBox { ItemsSource = BridgeInterpolationMode.FLOOR_INTERPOLATION_MODES, SelectedIndex = 0, MinWidth = 200 };
		private readonly ComboBox ceiling = new ComboBox { ItemsSource = BridgeInterpolationMode.CEILING_INTERPOLATION_MODES, SelectedIndex = 0, MinWidth = 200 };
		private readonly ComboBox brightness = new ComboBox { ItemsSource = BridgeInterpolationMode.BRIGHTNESS_INTERPOLATION_MODES, SelectedIndex = 0, MinWidth = 200 };
		private readonly Avalonia.Controls.CheckBox mirror = new Avalonia.Controls.CheckBox { Content = "Mirror mode" };
		private readonly Avalonia.Controls.CheckBox copy = new Avalonia.Controls.CheckBox { Content = "Copy mode" };
		private bool closing;

		internal int Subdivisions
		{
			get { return (int)(subdivisions.Value ?? subdivisions.Minimum); }
			set { subdivisions.Value = value; }
		}
		internal string FloorAlignMode { get { return (string)floor.SelectedItem; } }
		internal string CeilingAlignMode { get { return (string)ceiling.SelectedItem; } }
		internal string BrightnessMode { get { return (string)brightness.SelectedItem; } }
		internal bool MirrorMode { get { return mirror.IsChecked == true; } }
		internal bool CopyMode { get { return copy.IsChecked == true; } }

		/// <summary>The window (for the tests).</summary>
		internal Window Window { get { return window; } }
		internal NumericUpDown SubdivisionsBox { get { return subdivisions; } }
		internal Avalonia.Controls.CheckBox MirrorBox { get { return mirror; } }
		internal Avalonia.Controls.CheckBox CopyBox { get { return copy; } }
		internal Avalonia.Controls.Button OkButton { get; } = new Avalonia.Controls.Button { Content = "OK", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
		internal Avalonia.Controls.Button CancelButton { get; } = new Avalonia.Controls.Button { Content = "Cancel", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };

		internal event EventHandler OnSubdivisionChanged;
		internal event EventHandler OnOkClick;
		internal event EventHandler OnCancelClick;
		internal event EventHandler OnFlipClick;

		public BridgeModeForm()
		{
			var flip = new Avalonia.Controls.Button { Content = "Flip Lines" };
			flip.Click += (s, e) => { if(OnFlipClick != null) OnFlipClick(this, EventArgs.Empty); };
			OkButton.Click += (s, e) => { if(OnOkClick != null) OnOkClick(this, EventArgs.Empty); };
			CancelButton.Click += (s, e) => { if(OnCancelClick != null) OnCancelClick(this, EventArgs.Empty); };
			subdivisions.PropertyChanged += (s, e) => { if(e.Property == NumericUpDown.ValueProperty && OnSubdivisionChanged != null) OnSubdivisionChanged(this, EventArgs.Empty); };

			// Mirror and copy exclude each other
			mirror.IsCheckedChanged += (s, e) => { if(mirror.IsChecked == true && copy.IsChecked == true) copy.IsChecked = false; };
			copy.IsCheckedChanged += (s, e) => { if(mirror.IsChecked == true && copy.IsChecked == true) mirror.IsChecked = false; };

			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), Margin = new Thickness(0, 0, 0, 8) };
			void Row(int row, string label, Control box)
			{
				var l = new TextBlock { Text = label, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
				Grid.SetRow(l, row);
				Grid.SetRow(box, row);
				Grid.SetColumn(box, 1);
				box.Margin = new Thickness(0, 3);
				grid.Children.Add(l);
				grid.Children.Add(box);
			}
			Row(0, "Align floor:", floor);
			Row(1, "Align ceiling:", ceiling);
			Row(2, "Brightness:", brightness);
			Row(3, "Subdivisions:", subdivisions);

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
			buttons.Children.Add(OkButton);
			buttons.Children.Add(CancelButton);

			var layout = new StackPanel { Margin = new Thickness(12), Spacing = 4 };
			layout.Children.Add(grid);
			layout.Children.Add(mirror);
			layout.Children.Add(copy);
			layout.Children.Add(flip);
			layout.Children.Add(buttons);

			window = new Window
			{
				Title = "Options",
				SizeToContent = SizeToContent.WidthAndHeight,
				CanResize = false,
				ShowInTaskbar = false,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Content = layout
			};
			// Closing the window by hand cancels the mode, like UDB's FormClosed
			window.Closed += (s, e) => { if(OnCancelClick != null) OnCancelClick(this, EventArgs.Empty); };
		}

		/// <summary>Shows the window over the main window.</summary>
		public void Show(object owner)
		{
			Window main = DialogHost.Owner == null ? null : DialogHost.Owner();
			if(main != null) window.Show(main);
			else window.Show();
		}

		public void Dispose() { Close(); }

		public void Close()
		{
			if(closing) return;
			closing = true;
			window.Close();
		}
	}
}
