// The dialogs that belong to the visual (3D) mode: "Fit Textures" and "Create arch" (between slope handles). Same classes and members
// as UDB's forms. Both change the map while they are open (the effect is seen in the 3D view) and the caller withdraws the undo
// when they are cancelled.
using System;
using System.Collections.Generic;
using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;
using Control = Avalonia.Controls.Control;
using CheckBox = Avalonia.Controls.CheckBox;
using Button = Avalonia.Controls.Button;
using DialogResult = System.Windows.Forms.DialogResult;

namespace CodeImp.DoomBuilder.BuilderModes
{
	// The parameters of the "fit textures" dialog: a plain data structure, kept as it is
	internal struct FitTextureOptions
	{
		public double HorizontalRepeat;
		public double VerticalRepeat;
		public int PatternWidth;
		public int PatternHeight;
		public bool FitWidth;
		public bool FitHeight;
		public bool FitAcrossSurfaces;
		public bool AutoWidth;
		public bool AutoHeight;
		public Rectangle GlobalBounds;
		public Rectangle Bounds;

		// Initial texture coordinates
		public double InitialOffsetX;
		public double InitialOffsetY;
		public double ControlSideOffsetX;
		public double ControlSideOffsetY;
		public double InitialScaleX;
		public double InitialScaleY;
	}

	/// <summary>Fits the textures of the selected walls to the surface they cover, repeating them as asked.</summary>
	internal class FitTexturesForm : Interface.PluginDialog
	{
		// Settings, kept while the program runs
		private static double horizontalrepeat = 1.0;
		private static double verticalrepeat = 1.0;
		private static bool fitacrosssurfaces = true;
		private static bool fitwidth = true;
		private static bool fitheight = true;

		private readonly SimpleDialog window;
		private List<SortedVisualSide> strips;
		private bool blockupdate;

		private readonly CheckBox cbfitconnected = new CheckBox { Content = "Fit across connected surfaces" };
		private readonly CheckBox cbfitwidth = new CheckBox { Content = "Fit width" };
		private readonly CheckBox cbfitheight = new CheckBox { Content = "Fit height" };
		private readonly CheckBox cbautowidth = new CheckBox { Content = "Auto" };
		private readonly CheckBox cbautoheight = new CheckBox { Content = "Auto" };
		private readonly NumberBox horizrepeat = new NumberBox { AllowDecimal = true, AllowNegative = true, AllowRelative = false, AllowExpressions = true, ButtonStepFloat = 0.1f, MinWidth = 90 };
		private readonly NumberBox vertrepeat = new NumberBox { AllowDecimal = true, AllowNegative = true, AllowRelative = false, AllowExpressions = true, ButtonStepFloat = 0.1f, MinWidth = 90 };
		private readonly NumberBox patternwidth = new NumberBox { AllowDecimal = false, AllowNegative = false, AllowRelative = false, MinWidth = 90 };
		private readonly NumberBox patternheight = new NumberBox { AllowDecimal = false, AllowNegative = false, AllowRelative = false, MinWidth = 90 };
		private readonly Button resethoriz = new Button { Content = "Reset" };
		private readonly Button resetvert = new Button { Content = "Reset" };
		private readonly TextBlock labelhorizrepeat = new TextBlock { Text = "Horizontal:", VerticalAlignment = VerticalAlignment.Center };
		private readonly TextBlock labelvertrepeat = new TextBlock { Text = "Vertical:", VerticalAlignment = VerticalAlignment.Center };
		private readonly TextBlock labelpatternwidth = new TextBlock { Text = "Pattern width:", VerticalAlignment = VerticalAlignment.Center };
		private readonly TextBlock labelpatternheight = new TextBlock { Text = "Pattern height:", VerticalAlignment = VerticalAlignment.Center };
		private readonly Border repeatgroup;

		protected override SimpleDialog Window { get { return window; } }

		public FitTexturesForm()
		{
			var options = new StackPanel { Spacing = 4 };
			options.Children.Add(cbfitconnected);
			options.Children.Add(cbfitwidth);
			options.Children.Add(cbfitheight);

			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 8, RowSpacing = 6 };
			Place(grid, labelhorizrepeat, 0, 0); Place(grid, horizrepeat, 1, 0); Place(grid, resethoriz, 2, 0); Place(grid, cbautowidth, 3, 0);
			Place(grid, labelpatternwidth, 0, 1); Place(grid, patternwidth, 1, 1);
			Place(grid, labelvertrepeat, 0, 2); Place(grid, vertrepeat, 1, 2); Place(grid, resetvert, 2, 2); Place(grid, cbautoheight, 3, 2);
			Place(grid, labelpatternheight, 0, 3); Place(grid, patternheight, 1, 3);
			repeatgroup = Group("Texture Repeating", grid);

			var stack = new StackPanel { Spacing = 10 };
			stack.Children.Add(Group("Options", options));
			stack.Children.Add(repeatgroup);

			window = new SimpleDialog("Fit Textures", stack);
			window.OkButton.Content = "Apply";
			window.Closing += (s, e) => StoreSettings();

			horizrepeat.WhenTextChanged += (s, e) => { if(!blockupdate) UpdateChanges(); };
			vertrepeat.WhenTextChanged += (s, e) => { if(!blockupdate) UpdateChanges(); };
			patternwidth.WhenTextChanged += (s, e) => { if(!blockupdate) UpdateChanges(); };
			patternheight.WhenTextChanged += (s, e) => { if(!blockupdate) UpdateChanges(); };
			resethoriz.Click += (s, e) => horizrepeat.Text = "1";
			resetvert.Click += (s, e) => vertrepeat.Text = "1";
			foreach(CheckBox cb in new[] { cbfitconnected, cbfitwidth, cbfitheight, cbautowidth, cbautoheight })
				cb.IsCheckedChanged += (s, e) => { if(blockupdate) return; UpdateRepeatGroup(); UpdateChanges(); };
		}

		private static void Place(Grid grid, Control c, int column, int row)
		{
			Grid.SetColumn(c, column);
			Grid.SetRow(c, row);
			grid.Children.Add(c);
		}

		internal static Border Group(string title, Control content)
		{
			var stack = new StackPanel { Spacing = 6 };
			stack.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold });
			stack.Children.Add(content);
			return new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, CornerRadius = new CornerRadius(3), Padding = new Thickness(8), Child = stack };
		}

		public bool Setup(IEnumerable<BaseVisualGeometrySidedef> sides)
		{
			// Get shapes
			strips = BuilderModesTools.SortVisualSides(sides);

			// No dice...
			if(strips.Count == 0)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "Failed to setup sidedef chains...");
				return false;
			}

			// Restore settings
			blockupdate = true;

			// Make sure we start with sensible values for horizontal and vertical repeat
			if(horizontalrepeat == 0.0) horizontalrepeat = 1.0;
			if(verticalrepeat == 0.0) verticalrepeat = 1.0;

			horizrepeat.Text = horizontalrepeat.ToString();
			vertrepeat.Text = verticalrepeat.ToString();
			patternwidth.Text = "0";
			patternheight.Text = "0";
			cbfitconnected.IsChecked = fitacrosssurfaces;
			cbfitconnected.IsEnabled = (strips.Count > 1);
			cbfitwidth.IsChecked = fitwidth;
			cbfitheight.IsChecked = fitheight;
			UpdateRepeatGroup();

			blockupdate = false;

			// Trigger update
			UpdateChanges();

			return true;
		}

		private void UpdateChanges()
		{
			// Apply changes
			FitTextureOptions options = new FitTextureOptions
			{
				FitAcrossSurfaces = (cbfitconnected.IsEnabled && cbfitconnected.IsChecked == true),
				FitWidth = cbfitwidth.IsChecked == true,
				FitHeight = cbfitheight.IsChecked == true,
				PatternWidth = (int)patternwidth.GetResultFloat(0),
				PatternHeight = (int)patternheight.GetResultFloat(0),
				AutoWidth = cbautowidth.IsChecked == true,
				AutoHeight = cbautoheight.IsChecked == true
			};

			// Default the repeats to 1 if the value can't be parsed or if it's 0
			double hrepeat = horizrepeat.GetResultFloat(double.NaN);
			if(double.IsNaN(hrepeat) || hrepeat == 0.0) hrepeat = 1.0;
			double vrepeat = vertrepeat.GetResultFloat(double.NaN);
			if(double.IsNaN(vrepeat) || vrepeat == 0.0) vrepeat = 1.0;

			options.HorizontalRepeat = hrepeat;
			options.VerticalRepeat = vrepeat;

			foreach(SortedVisualSide side in strips) side.OnTextureFit(options);
		}

		private void UpdateRepeatGroup()
		{
			bool width = cbfitwidth.IsChecked == true, height = cbfitheight.IsChecked == true;

			// Disable whole group?
			repeatgroup.IsEnabled = width || height;
			if(!repeatgroup.IsEnabled) return;

			// Update control status
			cbautowidth.IsEnabled = width;
			patternwidth.IsEnabled = width && cbautowidth.IsChecked == true;
			labelpatternwidth.IsEnabled = patternwidth.IsEnabled;
			labelhorizrepeat.IsEnabled = width;
			horizrepeat.IsEnabled = width && cbautowidth.IsChecked != true;
			resethoriz.IsEnabled = horizrepeat.IsEnabled;

			cbautoheight.IsEnabled = height;
			patternheight.IsEnabled = height && cbautoheight.IsChecked == true;
			labelpatternheight.IsEnabled = patternheight.IsEnabled;
			labelvertrepeat.IsEnabled = height;
			vertrepeat.IsEnabled = height && cbautoheight.IsChecked != true;
			resetvert.IsEnabled = vertrepeat.IsEnabled;
		}

		// Store settings (when applied)
		private void StoreSettings()
		{
			if(!window.Accepted || strips == null) return;
			horizontalrepeat = horizrepeat.GetResultFloat(0);
			verticalrepeat = vertrepeat.GetResultFloat(0);
			fitacrosssurfaces = cbfitconnected.IsChecked == true;
			fitwidth = cbfitwidth.IsChecked == true;
			fitheight = cbfitheight.IsChecked == true;
		}
	}

	/// <summary>Creates an arch (a curved slope) between two slope handles: angle, offset, scale and direction, shown live in the 3D view.</summary>
	internal class SlopeArchForm : Interface.PluginDialog
	{
		private readonly SlopeArcher slopearcher;
		private readonly SimpleDialog window;
		private readonly double originaltheta, originaloffset, originalscale, originalheightoffset;
		private double oldtheta, oldoffset, oldscale;
		private bool blockupdate;
		public event EventHandler UpdateChangedObjects;

		private readonly NumberBox theta = new NumberBox { AllowDecimal = true, AllowNegative = false, AllowRelative = false, MinWidth = 100 };
		private readonly NumberBox offset = new NumberBox { AllowDecimal = true, AllowNegative = false, AllowRelative = false, MinWidth = 100 };
		private readonly NumberBox scale = new NumberBox { AllowDecimal = true, AllowNegative = false, AllowRelative = false, MinWidth = 100 };
		private readonly NumberBox heightoffset = new NumberBox { AllowDecimal = true, AllowNegative = true, AllowRelative = false, MinWidth = 100 };
		private readonly RadioButton up = new RadioButton { Content = "Up", GroupName = "archdir", IsChecked = true };
		private readonly RadioButton down = new RadioButton { Content = "Down", GroupName = "archdir" };
		private readonly CheckBox lockoffset = new CheckBox { Content = "Auto angle offset" };

		protected override SimpleDialog Window { get { return window; } }

		internal SlopeArchForm(SlopeArcher slopearcher)
		{
			this.slopearcher = slopearcher;

			oldtheta = originaltheta = Math.Round(Angle2D.RadToDeg(slopearcher.Theta), 2);
			oldoffset = originaloffset = Math.Round(Angle2D.RadToDeg(slopearcher.OffsetAngle), 2);
			oldscale = originalscale = slopearcher.Scale;
			originalheightoffset = slopearcher.HeightOffset;

			blockupdate = true;
			theta.Text = originaltheta.ToString();
			offset.Text = originaloffset.ToString();
			scale.Text = (originalscale * 100.0).ToString();
			heightoffset.Text = originalheightoffset.ToString();
			blockupdate = false;

			var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			var halfcircle = new Button { Content = "Half circle" };
			var quarterleft = new Button { Content = "Quarter circle (left)" };
			var quarterright = new Button { Content = "Quarter circle (right)" };
			var invert = new Button { Content = "Invert" };
			presets.Children.Add(halfcircle);
			presets.Children.Add(quarterleft);
			presets.Children.Add(quarterright);
			presets.Children.Add(invert);

			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 8, RowSpacing = 6 };
			AddRow(grid, 0, "Angle:", theta);
			AddRow(grid, 1, "Angle offset:", offset);
			Grid.SetColumn(lockoffset, 2); Grid.SetRow(lockoffset, 1); grid.Children.Add(lockoffset);
			AddRow(grid, 2, "Scale %:", scale);
			AddRow(grid, 3, "Height offset:", heightoffset);

			var direction = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
			direction.Children.Add(up);
			direction.Children.Add(down);

			var stack = new StackPanel { Spacing = 10 };
			stack.Children.Add(presets);
			stack.Children.Add(grid);
			stack.Children.Add(FitTexturesForm.Group("Direction", direction));

			window = new SimpleDialog("Create arch", stack);
			window.OkButton.Content = "Apply";
			window.Opened += (s, e) =>
			{
				// Immediately apply the arch when the form is shown
				slopearcher.ApplySlope();
				UpdateChangedObjects?.Invoke(this, EventArgs.Empty);
			};

			halfcircle.Click += (s, e) => SetAngles(180.0, 0.0);
			quarterleft.Click += (s, e) => SetAngles(90.0, 90.0);
			quarterright.Click += (s, e) => SetAngles(90.0, 0.0, keepoffsetmemory: true);
			invert.Click += (s, e) => Invert();
			theta.WhenTextChanged += (s, e) => { if(!blockupdate) ThetaChanged(); };
			offset.WhenTextChanged += (s, e) => { if(!blockupdate) OffsetChanged(); };
			scale.WhenTextChanged += (s, e) => { if(!blockupdate) ScaleChanged(); };
			heightoffset.WhenTextChanged += (s, e) => { if(!blockupdate) UpdateArch(); };
			up.IsCheckedChanged += (s, e) => { if(!blockupdate) UpdateArch(); };
			lockoffset.IsCheckedChanged += (s, e) => offset.IsEnabled = lockoffset.IsChecked != true;
		}

		private static void AddRow(Grid grid, int row, string label, Control box)
		{
			var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
			Grid.SetRow(text, row);
			grid.Children.Add(text);
			Grid.SetColumn(box, 1);
			Grid.SetRow(box, row);
			grid.Children.Add(box);
		}

		// Sets text without raising our own change handlers
		private void SetText(NumberBox box, string text)
		{
			bool before = blockupdate;
			blockupdate = true;
			box.Text = text;
			blockupdate = before;
		}

		/// <summary>Updates the arch with the values currently entered in the dialog</summary>
		private void UpdateArch()
		{
			double t = theta.GetResultFloat(originaltheta);
			double o = offset.GetResultFloat(originaloffset);
			double s = scale.GetResultFloat(originalscale * 100.0) / 100.0;

			// Flip the scale if "down" is checked
			if(up.IsChecked != true) s *= -1.0;

			slopearcher.Theta = Angle2D.DegToRad(t);
			slopearcher.OffsetAngle = Angle2D.DegToRad(o);
			slopearcher.Scale = s;
			slopearcher.HeightOffset = heightoffset.GetResultFloat(originalheightoffset);

			slopearcher.ApplySlope();

			// BaseVisualMode added an event handler to the dialog, so it updates the geometry when we tell it to
			UpdateChangedObjects?.Invoke(this, EventArgs.Empty);
		}

		private void SetAngles(double newtheta, double newoffset, bool keepoffsetmemory = false)
		{
			SetText(theta, newtheta.ToString());
			SetText(offset, newoffset.ToString());
			oldtheta = newtheta;
			if(!keepoffsetmemory) oldoffset = newoffset;
			UpdateArch();
		}

		// Handles updates of the theta value, with sanity checks
		private void ThetaChanged()
		{
			double newtheta = theta.GetResultFloat(originaltheta);
			double newoffset = offset.GetResultFloat(originaloffset);

			// Make sure that the new theta is between 0.0 and 180.0
			if(newtheta > 180.0)
			{
				newtheta = 180.0;
				SetText(theta, "180");
				General.Interface.DisplayStatus(StatusType.Warning, "The angle can not be greater than 180.");
			}
			else if(newtheta <= 0.0)
			{
				newtheta = oldtheta;
				SetText(theta, oldtheta.ToString());
			}

			// If the angle offset is locked automatically change its value to reflect the changed theta value
			if(lockoffset.IsChecked == true)
			{
				double diff = oldtheta - newtheta;
				newoffset = offset.GetResultFloat(originaloffset) + diff / 2.0;

				// Make sure that the result isn't invalid
				if(newoffset < 0.0)
				{
					SetText(theta, oldtheta.ToString());
					SetText(offset, oldoffset.ToString());
				}
				else SetText(offset, newoffset.ToString());
			}

			// If the new result is larger than 180.0 reset to the previous values
			if(theta.GetResultFloat(originaltheta) + offset.GetResultFloat(originaloffset) > 180.0)
			{
				SetText(theta, oldtheta.ToString());
				SetText(offset, oldoffset.ToString());
				General.Interface.DisplayStatus(StatusType.Warning, "The sum of the angle and offset angle can not be greater than 180.");
			}

			// Remember the old values
			oldtheta = theta.GetResultFloat(originaltheta);
			oldoffset = offset.GetResultFloat(originaloffset);

			UpdateArch();
		}

		// Handles updates of the angle offset value, with sanity checks
		private void OffsetChanged()
		{
			// If the new result is larger than 180.0 reset to the previous values
			if(theta.GetResultFloat(originaltheta) + offset.GetResultFloat(originaloffset) > 180.0)
			{
				SetText(theta, oldtheta.ToString());
				SetText(offset, oldoffset.ToString());
			}

			// Remember the old values
			oldtheta = theta.GetResultFloat(originaltheta);
			oldoffset = offset.GetResultFloat(originaloffset);

			UpdateArch();
		}

		// Handles updates of the scale value, with sanity checks
		private void ScaleChanged()
		{
			if(scale.GetResultFloat(originalscale) <= 0.0) SetText(scale, oldscale.ToString());

			// Remember the old value
			oldscale = scale.GetResultFloat(originalscale);

			UpdateArch();
		}

		// Inverts the current slope
		private void Invert()
		{
			// Flip up/down direction
			blockupdate = true;
			bool wasup = up.IsChecked == true;
			up.IsChecked = !wasup;
			down.IsChecked = wasup;
			blockupdate = false;

			double t = theta.GetResultFloat(originaltheta);
			double o = offset.GetResultFloat(originaloffset);

			// Subtract theta from the offset, if the result is greater than 0, otherwise add theta
			if(o - t < 0.0) o = o + t; else o = o - t;

			SetText(offset, o.ToString());

			UpdateArch();
		}
	}
}
