using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder.Geometry;

namespace CodeImp.DoomBuilder.BuilderModes
{
	/// <summary>
	/// The "Edit Selection" docker: position, size and rotation of the selection, to read and to type over, plus flipping and the
	/// options for sectors and textures. Same members as UDB's panel (the mode drives it); the controls are Avalonia, carried in
	/// the Docker as a native control. A value is applied when the user typed it and leaves the box or presses Enter.
	/// </summary>
	internal class EditSelectionPanel : System.Windows.Forms.Control
	{
		private readonly EditSelectionMode mode;
		private bool userinput;
		private bool preventchanges;
		private Vector2D orgpos, orgsize, abspos, relpos, abssize, relsize;
		private double absrotate;

		private readonly Button orgposx = new Button(), orgposy = new Button(), orgsizex = new Button(), orgsizey = new Button();
		private readonly TextBox absposx = new TextBox(), absposy = new TextBox(), relposx = new TextBox(), relposy = new TextBox();
		private readonly TextBox abssizex = new TextBox(), abssizey = new TextBox(), relsizex = new TextBox(), relsizey = new TextBox();
		private readonly TextBox absrot = new TextBox();
		private readonly CheckBox preciseposition = new CheckBox { Content = "Precise position" };
		private readonly CheckBox pinfloortextures = new CheckBox { Content = "Pin floor textures" };
		private readonly CheckBox pinceilingtextures = new CheckBox { Content = "Pin ceiling textures" };
		private readonly ComboBox heightmode = new ComboBox { ItemsSource = new[] { "Keep heights", "Adjust ceiling heights", "Adjust floor heights" }, MinWidth = 160 };

		public EditSelectionPanel(EditSelectionMode mode)
		{
			this.mode = mode;

			preventchanges = true;
			preciseposition.IsChecked = General.Map.UDMF && mode.UsePrecisePosition;     // sub-unit positions exist only in UDMF
			preciseposition.IsEnabled = General.Map.UDMF;
			preventchanges = false;

			Wire(absposx, v => mode.SetAbsPosX(v), () => abspos.x);
			Wire(absposy, v => mode.SetAbsPosY(v), () => abspos.y);
			Wire(relposx, v => mode.SetRelPosX(v), () => relpos.x);
			Wire(relposy, v => mode.SetRelPosY(v), () => relpos.y);
			Wire(abssizex, v => mode.SetAbsSizeX(v), () => abssize.x);
			Wire(abssizey, v => mode.SetAbsSizeY(v), () => abssize.y);
			Wire(relsizex, v => mode.SetRelSizeX(v), () => relsize.x);
			Wire(relsizey, v => mode.SetRelSizeY(v), () => relsize.y);
			Wire(absrot, v => mode.SetAbsRotation(Angle2D.DegToRad(v)), () => absrotate);

			orgposx.Click += (s, e) => { mode.SetAbsPosX(orgpos.x); General.Interface.FocusDisplay(); };
			orgposy.Click += (s, e) => { mode.SetAbsPosY(orgpos.y); General.Interface.FocusDisplay(); };
			orgsizex.Click += (s, e) => { mode.SetAbsSizeX(orgsize.x); General.Interface.FocusDisplay(); };
			orgsizey.Click += (s, e) => { mode.SetAbsSizeY(orgsize.y); General.Interface.FocusDisplay(); };

			preciseposition.IsCheckedChanged += (s, e) => { if(preventchanges) return; mode.UsePrecisePosition = preciseposition.IsChecked == true; General.Interface.FocusDisplay(); };
			heightmode.SelectionChanged += (s, e) => { if(preventchanges || heightmode.SelectedIndex == -1) return; mode.SectorHeightAdjustMode = (EditSelectionMode.HeightAdjustMode)heightmode.SelectedIndex; };
			pinfloortextures.IsCheckedChanged += (s, e) => { if(preventchanges) return; preventchanges = true; mode.PinFloorTextures = pinfloortextures.IsChecked == true; preventchanges = false; };
			pinceilingtextures.IsCheckedChanged += (s, e) => { if(preventchanges) return; preventchanges = true; mode.PinCeilingTextures = pinceilingtextures.IsChecked == true; preventchanges = false; };

			NativeControl = Build();
		}

		// ---- the same members as UDB's panel

		/// <summary>The values the selection had when the mode started (the buttons next to them put them back).</summary>
		public void ShowOriginalValues(Vector2D pos, Vector2D size)
		{
			orgpos = pos;
			orgsize = size;
			Ui(() =>
			{
				orgposx.Content = pos.x.ToString(CultureInfo.CurrentCulture);
				orgposy.Content = pos.y.ToString(CultureInfo.CurrentCulture);
				orgsizex.Content = size.x.ToString(CultureInfo.CurrentCulture);
				orgsizey.Content = size.y.ToString(CultureInfo.CurrentCulture);
			});
		}

		/// <summary>The values the selection has now. Typing is not an input any more once they are shown.</summary>
		public void ShowCurrentValues(Vector2D pos, Vector2D relpos, Vector2D size, Vector2D relsize, double rotation)
		{
			abspos = pos;
			this.relpos = relpos;
			abssize = size;
			this.relsize = relsize;
			absrotate = Angle2D.RadToDeg(rotation);
			Ui(() =>
			{
				preventchanges = true;
				absposx.Text = pos.x.ToString("0.#"); absposy.Text = pos.y.ToString("0.#");
				relposx.Text = relpos.x.ToString("0.#"); relposy.Text = relpos.y.ToString("0.#");
				abssizex.Text = size.x.ToString("0.#"); abssizey.Text = size.y.ToString("0.#");
				relsizex.Text = relsize.x.ToString("0.#"); relsizey.Text = relsize.y.ToString("0.#");
				absrot.Text = absrotate.ToString("0.#");
				preventchanges = false;
				userinput = false;
			});
		}

		internal void SetTextureTransformSettings(bool enable)
		{
			if(!enable)
			{
				Ui(() => { pinfloortextures.IsEnabled = false; pinceilingtextures.IsEnabled = false; });
				return;
			}
			Ui(() =>
			{
				preventchanges = true;
				pinfloortextures.IsChecked = mode.PinFloorTextures;
				pinceilingtextures.IsChecked = mode.PinCeilingTextures;
				preventchanges = false;
			});
		}

		internal void SetHeightAdjustMode(EditSelectionMode.HeightAdjustMode adjustmode, bool enable)
		{
			Ui(() =>
			{
				preventchanges = true;
				heightmode.SelectedIndex = (int)adjustmode;
				heightmode.IsEnabled = enable;
				preventchanges = false;
			});
		}

		public void Dispose() { }

		// ---- controls

		// A number box: typing marks it as the user's input; leaving it or Enter applies it (a number that does not read keeps the old one)
		private void Wire(TextBox box, Action<double> apply, Func<double> current)
		{
			box.Width = 80;
			box.PropertyChanged += (s, e) => { if(e.Property == TextBox.TextProperty && !preventchanges) userinput = true; };
			Action commit = () =>
			{
				if(!userinput) return;
				double value;
				if(!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) value = current();
				apply(value);
			};
			box.LostFocus += (s, e) => commit();
			box.KeyDown += (s, e) => { if(e.Key == Key.Enter) { commit(); General.Interface.FocusDisplay(); e.Handled = true; } };
		}

		private static void Ui(Action action)
		{
			if(Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) action(); else Avalonia.Threading.Dispatcher.UIThread.Post(action);
		}

		private static Control Row(string label, params Control[] boxes)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			row.Children.Add(new TextBlock { Text = label, Width = 60, VerticalAlignment = VerticalAlignment.Center });
			foreach(Control c in boxes) row.Children.Add(c);
			return row;
		}

		private static Control Group(string title, params Control[] rows)
		{
			var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 6) };
			panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
			foreach(Control r in rows) panel.Children.Add(r);
			return panel;
		}

		private Control Build()
		{
			var flip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			var fliph = new Button { Content = "Flip horizontally" };
			var flipv = new Button { Content = "Flip vertically" };
			fliph.Click += (s, e) => { General.Actions.InvokeAction("buildermodes_flipselectionh"); General.Interface.FocusDisplay(); };
			flipv.Click += (s, e) => { General.Actions.InvokeAction("buildermodes_flipselectionv"); General.Interface.FocusDisplay(); };
			flip.Children.Add(fliph);
			flip.Children.Add(flipv);

			var panel = new StackPanel { Margin = new Thickness(10) };
			panel.Children.Add(Group("Original (click to restore)", Row("Position", orgposx, orgposy), Row("Size", orgsizex, orgsizey)));
			panel.Children.Add(Group("Current", Row("Position", absposx, absposy), Row("Size", abssizex, abssizey), Row("Rotation", absrot)));
			panel.Children.Add(Group("Relative to the start", Row("Position", relposx, relposy), Row("Size", relsizex, relsizey)));
			panel.Children.Add(flip);
			panel.Children.Add(Group("Options", preciseposition, Row("Sector heights", heightmode), pinfloortextures, pinceilingtextures));
			return new ScrollViewer { Content = panel };
		}
	}
}
