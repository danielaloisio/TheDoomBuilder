using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.BuilderModes.Interface
{
	/// <summary>
	/// The "Draw Settings" docker: the heights, brightness and textures that new sectors and sidedefs get while drawing (each can be
	/// overridden or left to the defaults), plus buttons to fill or clear the textures of the selection. Same behavior as UDB's
	/// panel; the controls are Avalonia, carried in the Docker as a native control.
	/// </summary>
	internal class SectorDrawingOptionsPanel : System.Windows.Forms.Control
	{
		private readonly CheckBox cbOverrideCeilingTexture = new CheckBox { Content = "Ceiling" };
		private readonly CheckBox cbOverrideFloorTexture = new CheckBox { Content = "Floor" };
		private readonly CheckBox cbOverrideTopTexture = new CheckBox { Content = "Upper" };
		private readonly CheckBox cbOverrideMiddleTexture = new CheckBox { Content = "Middle" };
		private readonly CheckBox cbOverrideBottomTexture = new CheckBox { Content = "Lower" };
		private readonly CheckBox cbCeilHeight = new CheckBox { Content = "Ceiling height" };
		private readonly CheckBox cbFloorHeight = new CheckBox { Content = "Floor height" };
		private readonly CheckBox cbBrightness = new CheckBox { Content = "Brightness" };
		private readonly NumberBox ceilHeight = new NumberBox { AllowNegative = true, AllowRelative = false, ButtonStep = 8 };
		private readonly NumberBox floorHeight = new NumberBox { AllowNegative = true, AllowRelative = false, ButtonStep = 8 };
		private readonly NumberBox brightness = new NumberBox { AllowNegative = false, AllowRelative = false, ButtonStep = 16 };
		private readonly FlatSelector ceiling = new FlatSelector();
		private readonly FlatSelector floor = new FlatSelector();
		private readonly TextureSelector top = new TextureSelector();
		private readonly TextureSelector middle = new TextureSelector();
		private readonly TextureSelector bottom = new TextureSelector();
		private readonly Button getsectortexturesfromselection = new Button { Content = "Get flats from selection" };
		private readonly Button getsidetexturesfromselection = new Button { Content = "Get textures from selection" };
		private readonly Button getheightandbrightnessfromselection = new Button { Content = "Get heights and brightness from selection" };
		private bool setting;

		public SectorDrawingOptionsPanel()
		{
			cbOverrideCeilingTexture.IsCheckedChanged += (s, e) => OverrideChanged(cbOverrideCeilingTexture, ceiling, v => General.Map.Options.OverrideCeilingTexture = v, () => General.Map.Options.DefaultCeilingTexture = ceiling.TextureName);
			cbOverrideFloorTexture.IsCheckedChanged += (s, e) => OverrideChanged(cbOverrideFloorTexture, floor, v => General.Map.Options.OverrideFloorTexture = v, () => General.Map.Options.DefaultFloorTexture = floor.TextureName);
			cbOverrideTopTexture.IsCheckedChanged += (s, e) => OverrideChanged(cbOverrideTopTexture, top, v => General.Map.Options.OverrideTopTexture = v, () => General.Map.Options.DefaultTopTexture = top.TextureName);
			cbOverrideMiddleTexture.IsCheckedChanged += (s, e) => OverrideChanged(cbOverrideMiddleTexture, middle, v => General.Map.Options.OverrideMiddleTexture = v, () => General.Map.Options.DefaultWallTexture = middle.TextureName);
			cbOverrideBottomTexture.IsCheckedChanged += (s, e) => OverrideChanged(cbOverrideBottomTexture, bottom, v => General.Map.Options.OverrideBottomTexture = v, () => General.Map.Options.DefaultBottomTexture = bottom.TextureName);
			cbCeilHeight.IsCheckedChanged += (s, e) => OverrideChanged(cbCeilHeight, ceilHeight, v => General.Map.Options.OverrideCeilingHeight = v, null);
			cbFloorHeight.IsCheckedChanged += (s, e) => OverrideChanged(cbFloorHeight, floorHeight, v => General.Map.Options.OverrideFloorHeight = v, null);
			cbBrightness.IsCheckedChanged += (s, e) => OverrideChanged(cbBrightness, brightness, v => General.Map.Options.OverrideBrightness = v, null);

			ceilHeight.WhenTextChanged += (s, e) => { if(!setting) General.Map.Options.CustomCeilingHeight = ceilHeight.GetResult(General.Map.Options.CustomCeilingHeight); };
			floorHeight.WhenTextChanged += (s, e) => { if(!setting) General.Map.Options.CustomFloorHeight = floorHeight.GetResult(General.Map.Options.CustomFloorHeight); };
			brightness.WhenTextChanged += (s, e) => { if(!setting) General.Map.Options.CustomBrightness = General.Clamp(brightness.GetResult(General.Map.Options.CustomBrightness), 0, 255); };

			ceiling.ValueChanged += (s, e) => { if(!setting) General.Map.Options.DefaultCeilingTexture = ceiling.TextureName; };
			floor.ValueChanged += (s, e) => { if(!setting) General.Map.Options.DefaultFloorTexture = floor.TextureName; };
			top.ValueChanged += (s, e) => { if(!setting) General.Map.Options.DefaultTopTexture = top.TextureName; };
			middle.ValueChanged += (s, e) => { if(!setting) General.Map.Options.DefaultWallTexture = middle.TextureName; };
			bottom.ValueChanged += (s, e) => { if(!setting) General.Map.Options.DefaultBottomTexture = bottom.TextureName; };

			getsectortexturesfromselection.Click += (s, e) => GetSectorTextures();
			getsidetexturesfromselection.Click += (s, e) => GetSideTextures();
			getheightandbrightnessfromselection.Click += (s, e) => GetHeightAndBrightness();

			NativeControl = Build();
		}

		#region ================== Layout

		private Control Build()
		{
			var panel = new StackPanel { Margin = new Thickness(8), Spacing = 8 };

			panel.Children.Add(Group("Sector properties",
				Row(cbCeilHeight, ceilHeight), Row(cbFloorHeight, floorHeight), Row(cbBrightness, brightness),
				getheightandbrightnessfromselection));

			panel.Children.Add(Group("Flats",
				Pair(cbOverrideCeilingTexture, ceiling, Fill(() => FillFlat(true)), Clear(() => ClearFlat(true))),
				Pair(cbOverrideFloorTexture, floor, Fill(() => FillFlat(false)), Clear(() => ClearFlat(false))),
				getsectortexturesfromselection));

			panel.Children.Add(Group("Wall textures",
				Pair(cbOverrideTopTexture, top, Fill(() => FillSide(0)), Clear(() => ClearSide(0))),
				Pair(cbOverrideMiddleTexture, middle, Fill(() => FillSide(1)), Clear(() => ClearSide(1))),
				Pair(cbOverrideBottomTexture, bottom, Fill(() => FillSide(2)), Clear(() => ClearSide(2))),
				getsidetexturesfromselection));

			var all = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			var fillall = new Button { Content = "Fill all" };
			var clearall = new Button { Content = "Clear all" };
			fillall.Click += (s, e) => FillAll();
			clearall.Click += (s, e) => ClearAll();
			all.Children.Add(fillall);
			all.Children.Add(clearall);
			panel.Children.Add(all);

			return new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
		}

		private static Button Fill(Action action) { var b = new Button { Content = "Fill" }; b.Click += (s, e) => action(); return b; }
		private static Button Clear(Action action) { var b = new Button { Content = "Clear" }; b.Click += (s, e) => action(); return b; }

		private static Control Group(string title, params Control[] children)
		{
			var stack = new StackPanel { Spacing = 4 };
			stack.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold });
			foreach(Control c in children) stack.Children.Add(c);
			return new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, CornerRadius = new CornerRadius(3), Padding = new Thickness(6), Child = stack };
		}

		private static Control Row(CheckBox check, Control field)
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
			grid.Children.Add(check);
			Grid.SetColumn(field, 1);
			grid.Children.Add(field);
			return grid;
		}

		// A check box, the selector it enables and the buttons that fill/clear the selection with it
		private static Control Pair(CheckBox check, Control selector, Button fill, Button clear)
		{
			var buttons = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
			buttons.Children.Add(fill);
			buttons.Children.Add(clear);
			selector.Width = 80;
			selector.Height = 100;
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			row.Children.Add(selector);
			row.Children.Add(buttons);
			var stack = new StackPanel { Spacing = 2 };
			stack.Children.Add(check);
			stack.Children.Add(row);
			return stack;
		}

		#endregion

		#region ================== Setup / Terminate

		public void Setup()
		{
			setting = true;
			ceilHeight.Text = General.Map.Options.CustomCeilingHeight.ToString();
			floorHeight.Text = General.Map.Options.CustomFloorHeight.ToString();
			brightness.StepValues = General.Map.Config.BrightnessLevels;
			brightness.Text = General.Map.Options.CustomBrightness.ToString();
			foreach(ImageSelector selector in new ImageSelector[] { ceiling, floor, top, middle, bottom }) selector.Initialize();
			ceiling.TextureName = General.Map.Options.DefaultCeilingTexture;
			floor.TextureName = General.Map.Options.DefaultFloorTexture;
			top.TextureName = General.Map.Options.DefaultTopTexture;
			middle.TextureName = General.Map.Options.DefaultWallTexture;
			bottom.TextureName = General.Map.Options.DefaultBottomTexture;

			cbOverrideCeilingTexture.IsChecked = General.Map.Options.OverrideCeilingTexture;
			cbOverrideFloorTexture.IsChecked = General.Map.Options.OverrideFloorTexture;
			cbOverrideTopTexture.IsChecked = General.Map.Options.OverrideTopTexture;
			cbOverrideMiddleTexture.IsChecked = General.Map.Options.OverrideMiddleTexture;
			cbOverrideBottomTexture.IsChecked = General.Map.Options.OverrideBottomTexture;
			cbCeilHeight.IsChecked = General.Map.Options.OverrideCeilingHeight;
			cbFloorHeight.IsChecked = General.Map.Options.OverrideFloorHeight;
			cbBrightness.IsChecked = General.Map.Options.OverrideBrightness;
			setting = false;

			UpdateEnabled();
		}

		internal void Terminate()
		{
			ceiling.StopUpdate();
			floor.StopUpdate();
			top.StopUpdate();
			middle.StopUpdate();
			bottom.StopUpdate();
		}

		public new void Dispose() { Terminate(); }

		private void UpdateEnabled()
		{
			getsectortexturesfromselection.IsEnabled = (cbOverrideCeilingTexture.IsChecked == true || cbOverrideFloorTexture.IsChecked == true);
			getsidetexturesfromselection.IsEnabled = (cbOverrideTopTexture.IsChecked == true || cbOverrideMiddleTexture.IsChecked == true || cbOverrideBottomTexture.IsChecked == true);
			getheightandbrightnessfromselection.IsEnabled = (cbCeilHeight.IsChecked == true || cbFloorHeight.IsChecked == true || cbBrightness.IsChecked == true);
			ceiling.IsEnabled = cbOverrideCeilingTexture.IsChecked == true;
			floor.IsEnabled = cbOverrideFloorTexture.IsChecked == true;
			top.IsEnabled = cbOverrideTopTexture.IsChecked == true;
			middle.IsEnabled = cbOverrideMiddleTexture.IsChecked == true;
			bottom.IsEnabled = cbOverrideBottomTexture.IsChecked == true;
			ceilHeight.IsEnabled = cbCeilHeight.IsChecked == true;
			floorHeight.IsEnabled = cbFloorHeight.IsChecked == true;
			brightness.IsEnabled = cbBrightness.IsChecked == true;
		}

		// A check box turns an override on or off; when a texture one comes on, its current texture becomes the default at once
		private void OverrideChanged(CheckBox check, Control field, Action<bool> store, Action storeDefault)
		{
			bool on = check.IsChecked == true;
			field.IsEnabled = on;
			if(!setting)
			{
				store(on);
				if(on && storeDefault != null) storeDefault();
			}
			UpdateEnabled();
		}

		#endregion

		#region ================== Fill / Clear

		private static string Describe(string noun, int count) { return count > 1 ? count + " " + noun + "s" : noun; }

		private static void Finish(bool redraw)
		{
			General.Map.Data.UpdateUsedTextures();
			General.Map.IsChanged = true;
			if(redraw)
			{
				General.Map.Map.Update();
				General.Interface.RedrawDisplay();
			}
		}

		private void FillFlat(bool isceiling)
		{
			ICollection<Sector> sectors = General.Map.Map.GetSelectedSectors(true);
			if(sectors.Count == 0) return;
			General.Map.UndoRedo.CreateUndo((isceiling ? "Fill ceiling texture for " : "Fill floor texture for ") + Describe("sector", sectors.Count));
			foreach(Sector s in sectors)
			{
				if(isceiling) s.SetCeilTexture(ceiling.TextureName); else s.SetFloorTexture(floor.TextureName);
			}
			Finish(false);
			if(General.Map.Renderer2D.ViewMode == (isceiling ? Rendering.ViewMode.CeilingTextures : Rendering.ViewMode.FloorTextures))
				General.Interface.RedrawDisplay();
		}

		private void ClearFlat(bool isceiling)
		{
			ICollection<Sector> sectors = General.Map.Map.GetSelectedSectors(true);
			if(sectors.Count == 0) return;
			General.Map.UndoRedo.CreateUndo((isceiling ? "Clear ceiling texture from " : "Clear floor texture from ") + Describe("sector", sectors.Count));
			foreach(Sector s in sectors)
			{
				if(isceiling) s.SetCeilTexture("-"); else s.SetFloorTexture("-");
			}
			Finish(true);
		}

		// part: 0 = upper, 1 = middle, 2 = lower
		private static readonly string[] partnames = { "upper", "middle", "lower" };

		private string SideTexture(int part) { return part == 0 ? top.TextureName : (part == 1 ? middle.TextureName : bottom.TextureName); }
		private static bool Required(Sidedef side, int part) { return part == 0 ? side.HighRequired() : (part == 1 ? side.MiddleRequired() : side.LowRequired()); }
		private static string Current(Sidedef side, int part) { return part == 0 ? side.HighTexture : (part == 1 ? side.MiddleTexture : side.LowTexture); }
		private static void Set(Sidedef side, int part, string name)
		{
			if(part == 0) side.SetTextureHigh(name); else if(part == 1) side.SetTextureMid(name); else side.SetTextureLow(name);
		}

		private void FillSide(int part)
		{
			ICollection<Linedef> lines = General.Map.Map.GetSelectedLinedefs(true);
			if(lines.Count == 0) return;
			General.Map.UndoRedo.CreateUndo("Fill " + partnames[part] + " texture for " + Describe("linedef", lines.Count));
			foreach(Linedef l in lines)
			{
				if(l.Front != null && Required(l.Front, part)) Set(l.Front, part, SideTexture(part));
				if(l.Back != null && Required(l.Back, part)) Set(l.Back, part, SideTexture(part));
			}
			Finish(false);
		}

		private void ClearSide(int part)
		{
			ICollection<Linedef> lines = General.Map.Map.GetSelectedLinedefs(true);
			if(lines.Count == 0) return;
			General.Map.UndoRedo.CreateUndo("Clear " + partnames[part] + " texture from " + Describe("linedef", lines.Count));
			foreach(Linedef l in lines)
			{
				if(l.Front != null && Current(l.Front, part) != "-") Set(l.Front, part, "-");
				if(l.Back != null && Current(l.Back, part) != "-") Set(l.Back, part, "-");
			}
			Finish(false);
		}

		private void FillAll()
		{
			ICollection<Sector> sectors = General.Map.Map.GetSelectedSectors(true);
			if(sectors.Count > 0)
			{
				General.Map.UndoRedo.CreateUndo("Fill all textures for " + Describe("sector", sectors.Count));
				foreach(Sector s in sectors)
				{
					foreach(Sidedef side in s.Sidedefs)
					{
						if(top.IsEnabled && side.HighRequired()) side.SetTextureHigh(top.TextureName);
						if(middle.IsEnabled && side.MiddleRequired()) side.SetTextureMid(middle.TextureName);
						if(bottom.IsEnabled && side.LowRequired()) side.SetTextureLow(bottom.TextureName);
					}
					if(floor.IsEnabled) s.SetFloorTexture(floor.TextureName);
					if(ceiling.IsEnabled) s.SetCeilTexture(ceiling.TextureName);
				}
			}
			else
			{
				ICollection<Linedef> lines = General.Map.Map.GetSelectedLinedefs(true);
				if(lines.Count == 0) return;
				General.Map.UndoRedo.CreateUndo("Fill all textures for " + Describe("linedef", lines.Count));
				foreach(Linedef l in lines)
				{
					for(int part = 0; part < 3; part++)
					{
						Control enabled = part == 0 ? top : (part == 1 ? (Control)middle : bottom);
						if(!enabled.IsEnabled) continue;
						if(l.Front != null && Required(l.Front, part)) Set(l.Front, part, SideTexture(part));
						if(l.Back != null && Required(l.Back, part)) Set(l.Back, part, SideTexture(part));
					}
				}
			}
			Finish(true);
		}

		private void ClearAll()
		{
			ICollection<Sector> sectors = General.Map.Map.GetSelectedSectors(true);
			if(sectors.Count > 0)
			{
				General.Map.UndoRedo.CreateUndo("Clear all texture from " + Describe("sector", sectors.Count));
				foreach(Sector s in sectors)
				{
					foreach(Sidedef side in s.Sidedefs)
						for(int part = 0; part < 3; part++)
							if(Current(side, part) != "-") Set(side, part, "-");
					s.SetCeilTexture("-");
					s.SetFloorTexture("-");
				}
			}
			else
			{
				ICollection<Linedef> lines = General.Map.Map.GetSelectedLinedefs(true);
				if(lines.Count == 0) return;
				General.Map.UndoRedo.CreateUndo("Clear all texture from " + Describe("linedef", lines.Count));
				foreach(Linedef l in lines)
				{
					foreach(Sidedef side in new[] { l.Front, l.Back })
					{
						if(side == null) continue;
						for(int part = 0; part < 3; part++)
							if(Current(side, part) != "-") Set(side, part, "-");
					}
				}
			}
			Finish(true);
		}

		#endregion

		#region ================== Get properties from the selection

		private void GetSectorTextures()
		{
			ICollection<Sector> sectors = General.Map.Map.GetSelectedSectors(true);
			if(sectors.Count == 0)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "This action requires selected sector");
				return;
			}

			Sector s = General.GetByIndex(sectors, 0);
			if(cbOverrideCeilingTexture.IsChecked == true) ceiling.TextureName = s.CeilTexture;
			if(cbOverrideFloorTexture.IsChecked == true) floor.TextureName = s.FloorTexture;
		}

		private void GetSideTextures()
		{
			ICollection<Linedef> lines = General.Map.Map.GetSelectedLinedefs(true);
			if(lines.Count == 0)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "This action requires selected linedef");
				return;
			}

			Sidedef s = null;
			foreach(Linedef l in lines)
			{
				s = (l.Front ?? l.Back);
				if(s.MiddleTexture != "-" || s.HighTexture != "-" || s.LowTexture != "-") break;
			}

			if(s == null)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "Selection doesn't contain suitable sidedefs");
				return;
			}

			if(cbOverrideTopTexture.IsChecked == true) top.TextureName = s.HighTexture;
			if(cbOverrideMiddleTexture.IsChecked == true) middle.TextureName = s.MiddleTexture;
			if(cbOverrideBottomTexture.IsChecked == true) bottom.TextureName = s.LowTexture;
		}

		private void GetHeightAndBrightness()
		{
			ICollection<Sector> sectors = General.Map.Map.GetSelectedSectors(true);
			if(sectors.Count == 0)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "This action requires selected sector");
				return;
			}

			Sector s = General.GetByIndex(sectors, 0);
			if(cbCeilHeight.IsChecked == true) ceilHeight.Text = s.CeilHeight.ToString();
			if(cbFloorHeight.IsChecked == true) floorHeight.Text = s.FloorHeight.ToString();
			if(cbBrightness.IsChecked == true) brightness.Text = s.Brightness.ToString();
		}

		#endregion
	}
}
