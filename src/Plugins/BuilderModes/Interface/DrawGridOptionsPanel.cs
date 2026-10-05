using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using InterpolationMode = CodeImp.DoomBuilder.Geometry.InterpolationTools.Mode;
using GridLockMode = CodeImp.DoomBuilder.BuilderModes.DrawGridMode.GridLockMode;

namespace CodeImp.DoomBuilder.BuilderModes
{
	/// <summary>
	/// The "Draw Grid" docker: number of slices, which directions are locked to the grid, the interpolation of the slices' spacing
	/// and the drawing options. Same members as UDB's panel (the mode drives it); the controls are Avalonia, carried in the Docker
	/// as a native control.
	/// </summary>
	internal class DrawGridOptionsPanel : System.Windows.Forms.Control
	{
		public event EventHandler OnValueChanged;
		public event EventHandler OnGridLockModeChanged;
		public event EventHandler OnContinuousDrawingChanged;
		public event EventHandler OnShowGuidelinesChanged;
		public event EventHandler OnRelativeInterpolationChanged;

		private static readonly string[] interpolations = { MenusForm.GradientInterpolationModes.Linear, MenusForm.GradientInterpolationModes.EaseInOutSine, MenusForm.GradientInterpolationModes.EaseInSine, MenusForm.GradientInterpolationModes.EaseOutSine };

		private readonly NumericUpDown slicesH = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 3, Increment = 1, FormatString = "0", MinWidth = 100 };
		private readonly NumericUpDown slicesV = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 3, Increment = 1, FormatString = "0", MinWidth = 100 };
		private readonly Button reset = new Button { Content = "Reset", VerticalAlignment = VerticalAlignment.Center };
		private readonly ComboBox gridlockmode = new ComboBox { ItemsSource = new[] { "None", "Horizontal", "Vertical", "Both" }, MinWidth = 100 };
		private readonly ComboBox interphmode = new ComboBox { ItemsSource = interpolations, SelectedIndex = 0, MinWidth = 140 };
		private readonly ComboBox interpvmode = new ComboBox { ItemsSource = interpolations, SelectedIndex = 0, MinWidth = 140 };
		private readonly CheckBox triangulate = new CheckBox { Content = "Triangulate" };
		private readonly CheckBox continuousdrawing = new CheckBox { Content = "Continuous drawing" };
		private readonly CheckBox showguidelines = new CheckBox { Content = "Show guidelines" };
		private readonly CheckBox relativeinterpolation = new CheckBox { Content = "Relative interpolation" };
		private bool blockevents;

		public bool Triangulate { get { return triangulate.IsChecked == true; } set { Silently(() => triangulate.IsChecked = value); } }
		public GridLockMode GridLockMode { get { return (GridLockMode)Math.Max(0, gridlockmode.SelectedIndex); } set { Silently(() => gridlockmode.SelectedIndex = (int)value); UpdateEnabled(); } }
		public int HorizontalSlices { get { return (int)slicesH.Value; } set { Silently(() => slicesH.Value = value); } }
		public int MaxHorizontalSlices { get { return (int)slicesH.Maximum; } set { slicesH.Maximum = value; } }
		public int VerticalSlices { get { return (int)slicesV.Value; } set { Silently(() => slicesV.Value = value); } }
		public int MaxVerticalSlices { get { return (int)slicesV.Maximum; } set { slicesV.Maximum = value; } }
		public bool ContinuousDrawing { get { return continuousdrawing.IsChecked == true; } set { continuousdrawing.IsChecked = value; } }
		public bool ShowGuidelines { get { return showguidelines.IsChecked == true; } set { showguidelines.IsChecked = value; } }
		public bool RelativeInterpolation { get { return relativeinterpolation.IsChecked == true; } set { relativeinterpolation.IsChecked = value; } }

		public InterpolationMode HorizontalInterpolationMode
		{
			get
			{
				GridLockMode mode = GridLockMode;
				return (mode == GridLockMode.BOTH || mode == GridLockMode.HORIZONTAL) ? InterpolationMode.LINEAR : (InterpolationMode)Math.Max(0, interphmode.SelectedIndex);
			}
			set { interphmode.SelectedIndex = (int)value; }
		}

		public InterpolationMode VerticalInterpolationMode
		{
			get
			{
				GridLockMode mode = GridLockMode;
				return (mode == GridLockMode.BOTH || mode == GridLockMode.VERTICAL) ? InterpolationMode.LINEAR : (InterpolationMode)Math.Max(0, interpvmode.SelectedIndex);
			}
			set { interpvmode.SelectedIndex = (int)value; }
		}

		public DrawGridOptionsPanel()
		{
			gridlockmode.SelectedIndex = 0;

			slicesH.ValueChanged += (s, e) => ValueChanged();
			slicesV.ValueChanged += (s, e) => ValueChanged();
			triangulate.IsCheckedChanged += (s, e) => ValueChanged();
			gridlockmode.SelectionChanged += (s, e) =>
			{
				UpdateEnabled();
				if(!blockevents && OnGridLockModeChanged != null) OnGridLockModeChanged(this, EventArgs.Empty);
			};
			interphmode.DropDownClosed += (s, e) => General.Interface.FocusDisplay();
			interpvmode.DropDownClosed += (s, e) => General.Interface.FocusDisplay();
			interphmode.SelectionChanged += (s, e) => ValueChanged();
			interpvmode.SelectionChanged += (s, e) => ValueChanged();
			reset.Click += (s, e) => Reset();
			continuousdrawing.IsCheckedChanged += (s, e) => OnContinuousDrawingChanged?.Invoke(ContinuousDrawing, EventArgs.Empty);
			showguidelines.IsCheckedChanged += (s, e) => OnShowGuidelinesChanged?.Invoke(ShowGuidelines, EventArgs.Empty);
			relativeinterpolation.IsCheckedChanged += (s, e) => OnRelativeInterpolationChanged?.Invoke(RelativeInterpolation, EventArgs.Empty);

			UpdateEnabled();
			NativeControl = Build();
		}

		private Control Build()
		{
			var slices = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 6, RowSpacing = 4 };
			Place(slices, new TextBlock { Text = "Horizontal", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
			Place(slices, slicesH, 1, 0);
			Place(slices, new TextBlock { Text = "Vertical", VerticalAlignment = VerticalAlignment.Center }, 0, 1);
			Place(slices, slicesV, 1, 1);
			Grid.SetColumn(reset, 2);
			Grid.SetRowSpan(reset, 2);
			slices.Children.Add(reset);
			Place(slices, new TextBlock { Text = "Lock slices to grid:", VerticalAlignment = VerticalAlignment.Center }, 0, 2);
			Place(slices, gridlockmode, 1, 2);

			var interp = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 6, RowSpacing = 4 };
			Place(interp, new TextBlock { Text = "Horizontal", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
			Place(interp, interphmode, 1, 0);
			Place(interp, new TextBlock { Text = "Vertical", VerticalAlignment = VerticalAlignment.Center }, 0, 1);
			Place(interp, interpvmode, 1, 1);

			var panel = new StackPanel { Margin = new Thickness(8), Spacing = 8 };
			panel.Children.Add(Group(" Number of slices: ", slices, triangulate));
			panel.Children.Add(Group(" Slices spacing interpolation: ", interp));
			panel.Children.Add(Group(" Options: ", continuousdrawing, showguidelines, relativeinterpolation));
			return new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
		}

		private static void Place(Grid grid, Control c, int column, int row)
		{
			Grid.SetColumn(c, column);
			Grid.SetRow(c, row);
			grid.Children.Add(c);
		}

		private static Control Group(string title, params Control[] children)
		{
			var stack = new StackPanel { Spacing = 4 };
			stack.Children.Add(new TextBlock { Text = title.Trim(), FontWeight = Avalonia.Media.FontWeight.SemiBold });
			foreach(Control c in children) stack.Children.Add(c);
			return new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, CornerRadius = new CornerRadius(3), Padding = new Thickness(6), Child = stack };
		}

		private void Silently(Action change)
		{
			bool before = blockevents;
			blockevents = true;
			change();
			blockevents = before;
		}

		private void ValueChanged()
		{
			if(!blockevents && OnValueChanged != null) OnValueChanged(this, EventArgs.Empty);
		}

		// Locked directions have no slice count of their own
		private void UpdateEnabled()
		{
			GridLockMode mode = GridLockMode;
			slicesH.IsEnabled = (mode == GridLockMode.NONE || mode == GridLockMode.VERTICAL);
			slicesV.IsEnabled = (mode == GridLockMode.NONE || mode == GridLockMode.HORIZONTAL);
			interphmode.IsEnabled = slicesH.IsEnabled;
			interpvmode.IsEnabled = slicesV.IsEnabled;
			reset.IsEnabled = (mode != GridLockMode.BOTH);
		}

		private void Reset()
		{
			GridLockMode mode = GridLockMode;
			Silently(() =>
			{
				if(mode == GridLockMode.NONE || mode == GridLockMode.VERTICAL) slicesH.Value = 3;
				if(mode == GridLockMode.NONE || mode == GridLockMode.HORIZONTAL) slicesV.Value = 3;
			});
			OnValueChanged?.Invoke(this, EventArgs.Empty);
		}

		public new void Dispose() { }
	}
}
