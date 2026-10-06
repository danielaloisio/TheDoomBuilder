// The color picker for sectors (UDMF): the light color and the fade color of the selected sectors. UDB's SectorColorPicker. The sectors change
// while the color is picked (and the display with them); Cancel withdraws it all.
using System;
using System.Collections.Generic;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.ColorPicker.Controls;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.VisualModes;

namespace CodeImp.DoomBuilder.ColorPicker.Windows
{
	public class SectorColorPicker : ColorPickerWindow, IColorPicker
	{
		public ColorPickerType Type { get { return ColorPickerType.CP_SECTOR; } }

		private const int DEFAULT_LIGHT_COLOR = 0xFFFFFF;
		private const int DEFAULT_FADE_COLOR = 0;

		private List<Sector> selection;
		private List<VisualSector> visualSelection;
		private int curSectorColor;
		private int curFadeColor;
		private int initialSectorColor;
		private int initialFadeColor;
		private static string currentColorTag = "lightcolor";
		private string mode;
		private readonly RadioButton rbSectorColor = new RadioButton { Content = "Sector color", Tag = "lightcolor", GroupName = "sectorcolor" };
		private readonly RadioButton rbFadeColor = new RadioButton { Content = "Fade color", Tag = "fadecolor", GroupName = "sectorcolor" };

		// For the tests
		internal RadioButton SectorColorButton { get { return rbSectorColor; } }
		internal RadioButton FadeColorButton { get { return rbFadeColor; } }
		internal IList<Sector> Selection { get { return selection; } }

		public bool Setup(string editingModeName)
		{
			mode = editingModeName;

			if(mode == "SectorsMode")
			{
				selection = (List<Sector>)(General.Map.Map.GetSelectedSectors(true));
			}
			else
			{
				// Visual mode: the selected sectors, or the sectors of the selected surfaces
				selection = new List<Sector>();
				VisualMode vm = (VisualMode)General.Editing.Mode;
				visualSelection = vm.GetSelectedVisualSectors(false);
				if(visualSelection.Count > 0)
				{
					foreach(VisualSector vs in visualSelection)
						selection.Add(vs.Sector);
				}
				else
				{
					visualSelection = new List<VisualSector>();
					selection = (List<Sector>)(General.Map.Map.GetSelectedSectors(true));
					foreach(Sector s in selection)
					{
						if(vm.VisualSectorExists(s))
							visualSelection.Add(vm.GetVisualSector(s));
					}
				}
			}

			string rest = selection.Count + " sector" + (selection.Count > 1 ? "s" : "");
			General.Map.UndoRedo.CreateUndo("Edit color of " + rest);
			foreach(Sector s in selection) s.Fields.BeforeFieldsChange();

			// Get the current colors from the first sector
			curSectorColor = selection[0].Fields.GetValue("lightcolor", DEFAULT_LIGHT_COLOR);
			curFadeColor = selection[0].Fields.GetValue("fadecolor", DEFAULT_FADE_COLOR);

			// Every sector must have the fields, or the picked color has nothing to change
			for(int i = 0; i < selection.Count; i++)
			{
				if(!selection[i].Fields.ContainsKey("lightcolor"))
					selection[i].Fields.Add("lightcolor", new UniValue(UniversalType.Color, curSectorColor));
				if(!selection[i].Fields.ContainsKey("fadecolor"))
					selection[i].Fields.Add("fadecolor", new UniValue(UniversalType.Color, curFadeColor));
			}

			initialSectorColor = curSectorColor;
			initialFadeColor = curFadeColor;

			var picker = new ColorPickerControl();
			picker.Initialize(Color.FromArgb(currentColorTag == "lightcolor" ? curSectorColor : curFadeColor));
			picker.OnColorChanged += OnColorPickerControl1OnColorChanged;

			if(currentColorTag == "lightcolor") rbSectorColor.IsChecked = true;
			else rbFadeColor.IsChecked = true;
			rbSectorColor.IsCheckedChanged += rbColor_CheckedChanged;
			rbFadeColor.IsCheckedChanged += rbColor_CheckedChanged;

			var group = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Avalonia.Thickness(0, 6, 0, 0) };
			group.Children.Add(new TextBlock { Text = "Edit:", VerticalAlignment = VerticalAlignment.Center });
			group.Children.Add(rbSectorColor);
			group.Children.Add(rbFadeColor);
			var layout = new DockPanel { Margin = new Avalonia.Thickness(8) };
			DockPanel.SetDock(group, Dock.Bottom);
			layout.Children.Add(group);
			layout.Children.Add(picker);

			Attach(picker, layout);
			Title = "Editing " + rest;
			return true;
		}

		protected override void OnClosing(bool ok)
		{
			if(ok)
			{
				// The default colors are not stored
				foreach(Sector s in selection)
				{
					if((int)s.Fields["lightcolor"].Value == DEFAULT_LIGHT_COLOR)
						s.Fields.Remove("lightcolor");

					if((int)s.Fields["fadecolor"].Value == DEFAULT_FADE_COLOR)
						s.Fields.Remove("fadecolor");
				}
			}
			else
			{
				General.Map.UndoRedo.WithdrawUndo();
			}
		}

		private void OnColorPickerControl1OnColorChanged(object sender, ColorChangedEventArgs e)
		{
			foreach(Sector s in selection)
			{
				s.Fields[currentColorTag].Value = e.RGB.Red << 16 | e.RGB.Green << 8 | e.RGB.Blue;
				s.UpdateNeeded = true;
				s.UpdateCache();
			}

			if(mode == "SectorsMode")
			{
				General.Interface.RedrawDisplay();
			}
			else
			{
				foreach(VisualSector vs in visualSelection) vs.UpdateSectorData();
			}
		}

		// The other color is picked: its first value is the one to go back to
		private void rbColor_CheckedChanged(object sender, EventArgs e)
		{
			RadioButton b = (RadioButton)sender;
			if(b.IsChecked == true)
			{
				currentColorTag = (string)b.Tag;
				if(currentColorTag == "lightcolor")
					Picker.SetInitialColor(Color.FromArgb(initialSectorColor));
				else
					Picker.SetInitialColor(Color.FromArgb(initialFadeColor));

				Picker.SetCurrentColor(Color.FromArgb((int)selection[0].Fields[currentColorTag].Value));
			}
		}
	}
}
