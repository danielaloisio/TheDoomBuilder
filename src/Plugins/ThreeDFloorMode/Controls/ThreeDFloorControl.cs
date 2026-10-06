// One 3D floor in the 3D floors window: its textures and heights, the type/flags/alpha arguments of the Sector_Set3dFloor action, the
// control sector's brightness and tags, and the sectors it is tagged to. UDB's ThreeDFloorHelperControl built from Avalonia controls; the
// editor window works with the same members (Update, SetDefaults, ApplyToThreeDFloor, Used, checkedListBoxSectors...).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;
using Control = Avalonia.Controls.Control;
using CodeImp.DoomBuilder.Windows;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class ThreeDFloorHelperControl
	{
		#region ================== Variables

		private ThreeDFloor threeDFloor;
		public Linedef linedef;
		private bool isnew;
		private Sector sector;
		private List<int> checkedsectors;
		private bool used;

		private readonly Border view = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0, 192, 0)), Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(8) };
		private readonly FlatSelector sectorTopFlat = new FlatSelector();
		private readonly TextureSelector sectorBorderTexture = new TextureSelector { Required = false };
		private readonly FlatSelector sectorBottomFlat = new FlatSelector();
		private readonly NumberBox sectorCeilingHeight = new NumberBox { AllowDecimal = false, AllowNegative = true, MinWidth = 80 };
		private readonly NumberBox sectorFloorHeight = new NumberBox { AllowDecimal = false, AllowNegative = true, MinWidth = 80 };
		private readonly NumberBox sectorBrightness = new NumberBox { AllowDecimal = false, AllowNegative = false, MinWidth = 80 };
		private readonly TextBlock borderHeightLabel = new TextBlock { Text = "0", VerticalAlignment = VerticalAlignment.Center };
		private readonly TextBlock tagsLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
		private readonly ArgumentModel typeModel = new ArgumentModel();
		private readonly ArgumentModel flagsModel = new ArgumentModel();
		private readonly ArgumentModel alphaModel = new ArgumentModel();
		private ArgumentBox typeArgument;
		private ArgumentBox flagsArgument;
		private ArgumentBox alphaArgument;

		/// <summary>The sectors this 3D floor is tagged to.</summary>
		public readonly CheckedSectorList checkedListBoxSectors = new CheckedSectorList();

		/// <summary>The window this control sits in (it handles duplicating, splitting and detaching).</summary>
		internal ThreeDFloorEditorWindow Editor { get; set; }

		#endregion

		#region ================== Properties

		public ThreeDFloor ThreeDFloor { get { return threeDFloor; } }
		public bool IsNew { get { return isnew; } }
		public Sector Sector { get { return sector; } }
		public List<int> CheckedSectors { get { return checkedsectors; } }
		public bool Used { get { return used; } set { used = value; } }

		/// <summary>The control to put on screen.</summary>
		public Control View { get { return view; } }
		public bool Visible { get { return view.IsVisible; } set { view.IsVisible = value; } }
		public void Show() { Visible = true; }
		public void Hide() { Visible = false; }

		// For the tests
		internal NumberBox CeilingHeightBox { get { return sectorCeilingHeight; } }
		internal NumberBox FloorHeightBox { get { return sectorFloorHeight; } }
		internal NumberBox BrightnessBox { get { return sectorBrightness; } }
		internal TextBlock BorderHeightText { get { return borderHeightLabel; } }
		internal TextBlock TagsText { get { return tagsLabel; } }
		internal FlatSelector TopFlat { get { return sectorTopFlat; } }
		internal FlatSelector BottomFlat { get { return sectorBottomFlat; } }
		internal TextureSelector BorderTexture { get { return sectorBorderTexture; } }
		internal ArgumentModel TypeArgument { get { return typeModel; } }
		internal ArgumentModel FlagsArgument { get { return flagsModel; } }
		internal ArgumentModel AlphaArgument { get { return alphaModel; } }
		internal IEnumerable<AvButton> Buttons { get { return buttons; } }
		private readonly List<AvButton> buttons = new List<AvButton>();

		#endregion

		#region ================== Constructors

		// Create the control from an existing linedef
		public ThreeDFloorHelperControl(ThreeDFloor threeDFloor)
		{
			Build();
			sectorTopFlat.Initialize();
			sectorBorderTexture.Initialize();
			sectorBottomFlat.Initialize();
			Update(threeDFloor);
		}

		// Create a duplicate of the given control
		public ThreeDFloorHelperControl(ThreeDFloorHelperControl ctrl) : this()
		{
			Update(ctrl);
		}

		// Create a blank control for a new 3D floor
		public ThreeDFloorHelperControl()
		{
			Build();
			sectorTopFlat.Initialize();
			sectorBorderTexture.Initialize();
			sectorBottomFlat.Initialize();
			SetDefaults();
		}

		private AvButton Button(string text, Action click)
		{
			var button = new AvButton { Content = text, Margin = new Thickness(0, 0, 4, 0) };
			button.Click += (s, e) => click();
			buttons.Add(button);
			return button;
		}

		private static Control Labeled(string label, Control control)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2) };
			row.Children.Add(new TextBlock { Text = label, Width = 80, VerticalAlignment = VerticalAlignment.Center });
			row.Children.Add(control);
			return row;
		}

		private void Build()
		{
			typeArgument = new ArgumentBox(typeModel);
			flagsArgument = new ArgumentBox(flagsModel);
			alphaArgument = new ArgumentBox(alphaModel);

			checkedListBoxSectors.ItemCheck += OnItemCheck;
			sectorCeilingHeight.WhenTextChanged += (s, e) => RecomputeBorderHeight();
			sectorFloorHeight.WhenTextChanged += (s, e) => RecomputeBorderHeight();

			// Top, border and bottom, with the heights beside them
			var surfaces = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
			void Cell(int col, string label, Control picture, Control height)
			{
				var title = new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center };
				var stack = new StackPanel { Margin = new Thickness(0, 0, 8, 0), Spacing = 2 };
				stack.Children.Add(title);
				stack.Children.Add(picture);
				if(height != null) stack.Children.Add(height);
				Grid.SetColumn(stack, col);
				surfaces.Children.Add(stack);
			}
			Cell(0, "Top", sectorTopFlat, sectorCeilingHeight);
			Cell(1, "Border", sectorBorderTexture, borderHeightLabel);
			Cell(2, "Bottom", sectorBottomFlat, sectorFloorHeight);

			var action = new StackPanel { MinWidth = 260 };
			action.Children.Add(Labeled("Type:", typeArgument));
			action.Children.Add(Labeled("Flags:", flagsArgument));
			action.Children.Add(Labeled("Alpha:", alphaArgument));
			action.Children.Add(Labeled("Brightness:", sectorBrightness));
			action.Children.Add(Labeled("Tag(s):", tagsLabel));
			action.Children.Add(Button("Edit control sector", EditSector));

			var sectorbuttons = new WrapPanel { Orientation = Orientation.Horizontal };
			sectorbuttons.Children.Add(Button("Check all", () => SetAll(true)));
			sectorbuttons.Children.Add(Button("Uncheck all", () => SetAll(false)));

			var sectors = new StackPanel { MinWidth = 200, Spacing = 2 };
			sectors.Children.Add(new TextBlock { Text = "Sectors", FontWeight = FontWeight.SemiBold });
			sectors.Children.Add(checkedListBoxSectors.View);
			sectors.Children.Add(sectorbuttons);

			var commands = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
			commands.Children.Add(Button("Duplicate", () => Editor?.DuplicateThreeDFloor(this)));
			commands.Children.Add(Button("Split", () => Editor?.SplitThreeDFloor(this)));
			commands.Children.Add(Button("Detach", () => Editor?.DetachThreeDFloor(this)));

			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
			row.Children.Add(surfaces);
			row.Children.Add(action);
			row.Children.Add(sectors);

			var content = new StackPanel();
			content.Children.Add(row);
			content.Children.Add(commands);
			view.Child = content;
			UpdateBorder();
		}

		// A new 3D floor has a green stripe at the left (UDB paints it in the control's Paint event)
		private void UpdateBorder()
		{
			view.BorderThickness = isnew ? new Thickness(5, 0, 0, 0) : new Thickness(0);
		}

		#endregion

		#region ================== Methods

		private void SetupArguments()
		{
			SetupArgument(typeModel, typeArgument, General.Map.Config.LinedefActions[160].Args[1]);
			SetupArgument(flagsModel, flagsArgument, General.Map.Config.LinedefActions[160].Args[2]);
			SetupArgument(alphaModel, alphaArgument, General.Map.Config.LinedefActions[160].Args[3]);
		}

		private static void SetupArgument(ArgumentModel model, ArgumentBox box, CodeImp.DoomBuilder.Config.ArgumentInfo info)
		{
			model.Setup(info);
			box.Rebuild();
			box.Show();
		}

		private static void SetArgument(ArgumentModel model, ArgumentBox box, int value)
		{
			model.SetValue(value);
			box.Show();
		}

		public void SetDefaults()
		{
			isnew = true;
			UpdateBorder();
			threeDFloor = new ThreeDFloor();

			sectorBorderTexture.TextureName = General.Settings.DefaultTexture;
			sectorTopFlat.TextureName = General.Settings.DefaultCeilingTexture;
			sectorBottomFlat.TextureName = General.Settings.DefaultFloorTexture;
			sectorCeilingHeight.Text = General.Settings.DefaultCeilingHeight.ToString();
			sectorFloorHeight.Text = General.Settings.DefaultFloorHeight.ToString();

			SetupArguments();
			typeModel.SetDefaultValue();
			flagsModel.SetDefaultValue();
			alphaModel.SetDefaultValue();
			typeArgument.Show();
			flagsArgument.Show();
			alphaArgument.Show();

			tagsLabel.Text = "0";

			AddSectorCheckboxes();

			for(int i = 0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, true);

			//When creating a NEW 3d sector, find information about what is selected to populate the defaults
			int FloorHeight = int.MinValue;
			int SectorDarkest = int.MaxValue;

			foreach(Sector s in BuilderPlug.TDFEW.SelectedSectors)
			{
				if(s.FloorHeight > FloorHeight)
					FloorHeight = s.FloorHeight;

				if(s.Brightness < SectorDarkest)
					SectorDarkest = s.Brightness;
			}

			//set the floor height to match the lowest sector selected, then offset the height by the configured default
			if(FloorHeight != int.MinValue)
			{
				int DefaultHeight = General.Settings.DefaultCeilingHeight - General.Settings.DefaultFloorHeight;

				sectorFloorHeight.Text = FloorHeight.ToString();
				sectorCeilingHeight.Text = (FloorHeight + DefaultHeight).ToString();
			}

			//set the brightness to match the darkest of all the selected sectors by default
			if(SectorDarkest != int.MaxValue)
				sectorBrightness.Text = SectorDarkest.ToString();
			else
				sectorBrightness.Text = General.Settings.DefaultBrightness.ToString();

			sector = General.Map.Map.CreateSector();
		}

		// Copies the settings of another control
		public void Update(ThreeDFloorHelperControl ctrl)
		{
			sectorBorderTexture.TextureName = threeDFloor.BorderTexture = ctrl.threeDFloor.BorderTexture;
			sectorTopFlat.TextureName = threeDFloor.TopFlat = ctrl.threeDFloor.TopFlat;
			sectorBottomFlat.TextureName = threeDFloor.BottomFlat = ctrl.threeDFloor.BottomFlat;
			sectorCeilingHeight.Text = ctrl.threeDFloor.TopHeight.ToString();
			sectorFloorHeight.Text = ctrl.threeDFloor.BottomHeight.ToString();
			borderHeightLabel.Text = (ctrl.threeDFloor.TopHeight - ctrl.threeDFloor.BottomHeight).ToString();
			threeDFloor.TopHeight = ctrl.threeDFloor.TopHeight;
			threeDFloor.BottomHeight = ctrl.threeDFloor.BottomHeight;
			SetArgument(typeModel, typeArgument, ctrl.threeDFloor.Type);
			SetArgument(flagsModel, flagsArgument, ctrl.threeDFloor.Flags);
			SetArgument(alphaModel, alphaArgument, ctrl.threeDFloor.Alpha);
			sectorBrightness.Text = ctrl.threeDFloor.Brightness.ToString();

			threeDFloor.FloorSlope = ctrl.ThreeDFloor.FloorSlope;
			threeDFloor.FloorSlopeOffset = ctrl.ThreeDFloor.FloorSlopeOffset;
			threeDFloor.CeilingSlope = ctrl.ThreeDFloor.CeilingSlope;
			threeDFloor.CeilingSlopeOffset = ctrl.ThreeDFloor.CeilingSlopeOffset;

			for(int i = 0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, ctrl.checkedListBoxSectors.GetItemChecked(i));
		}

		// Shows an existing 3D floor
		public void Update(ThreeDFloor threeDFloor)
		{
			isnew = false;
			UpdateBorder();

			this.threeDFloor = threeDFloor;

			sectorBorderTexture.TextureName = threeDFloor.BorderTexture;
			sectorTopFlat.TextureName = threeDFloor.TopFlat;
			sectorBottomFlat.TextureName = threeDFloor.BottomFlat;
			sectorCeilingHeight.Text = threeDFloor.TopHeight.ToString();
			sectorFloorHeight.Text = threeDFloor.BottomHeight.ToString();
			borderHeightLabel.Text = (threeDFloor.TopHeight - threeDFloor.BottomHeight).ToString();

			SetupArguments();
			SetArgument(typeModel, typeArgument, threeDFloor.Type);
			SetArgument(flagsModel, flagsArgument, threeDFloor.Flags);
			SetArgument(alphaModel, alphaArgument, threeDFloor.Alpha);

			sectorBrightness.Text = threeDFloor.Brightness.ToString();

			AddSectorCheckboxes();

			if(sector == null || sector.IsDisposed)
				sector = General.Map.Map.CreateSector();

			if(threeDFloor.Sector != null)
			{
				threeDFloor.Sector.CopyPropertiesTo(sector);
				tagsLabel.Text = String.Join(", ", sector.Tags.Select(o => o.ToString()).ToArray());
			}

			if(sector != null && !sector.IsDisposed)
				sector.Selected = false;
		}

		// Writes what the controls say into the 3D floor
		public void ApplyToThreeDFloor()
		{
			Regex r = new Regex(@"\d+");

			threeDFloor.TopHeight = sectorCeilingHeight.GetResult(threeDFloor.TopHeight);
			threeDFloor.BottomHeight = sectorFloorHeight.GetResult(threeDFloor.BottomHeight);
			threeDFloor.TopFlat = sectorTopFlat.TextureName;
			threeDFloor.BottomFlat = sectorBottomFlat.TextureName;
			threeDFloor.BorderTexture = sectorBorderTexture.TextureName;
			threeDFloor.Type = typeModel.GetResult(threeDFloor.Type);
			threeDFloor.Flags = flagsModel.GetResult(threeDFloor.Flags);
			threeDFloor.Alpha = alphaModel.GetResult(threeDFloor.Alpha);
			threeDFloor.Brightness = sectorBrightness.GetResult(threeDFloor.Brightness);
			threeDFloor.Tags = sector.Tags;
			threeDFloor.IsNew = isnew;

			if(threeDFloor.Sector != null)
			{
				sector.CopyPropertiesTo(threeDFloor.Sector);
				tagsLabel.Text = String.Join(", ", sector.Tags.Select(o => o.ToString()).ToArray());
			}

			threeDFloor.TaggedSectors = new List<Sector>();

			for(int i = 0; i < checkedListBoxSectors.Items.Count; i++)
			{
				string text = checkedListBoxSectors.Items[i].ToString();
				bool ischecked = !(checkedListBoxSectors.GetItemCheckState(i) == CheckState.Unchecked);

				if(ischecked)
				{
					var matches = r.Matches(text);
					Sector s = General.Map.Map.GetSectorByIndex(int.Parse(matches[0].ToString()));
					threeDFloor.TaggedSectors.Add(s);
				}
			}
		}

		// The sectors that can be tagged: the selected ones and the ones the 3D floor is tagged to already
		private void AddSectorCheckboxes()
		{
			List<Sector> sectors = new List<Sector>(BuilderPlug.TDFEW.SelectedSectors.OrderBy(o => o.Index));
			checkedsectors = new List<int>();

			checkedListBoxSectors.Items.Clear();

			foreach(Sector s in ThreeDFloor.TaggedSectors)
			{
				if(!sectors.Contains(s))
					sectors.Add(s);
			}

			foreach(Sector s in sectors)
			{
				int i = checkedListBoxSectors.Items.Add("Sector " + s.Index.ToString(), ThreeDFloor.TaggedSectors.Contains(s));

				if(ThreeDFloor.TaggedSectors.Contains(s))
					checkedsectors.Add(s.Index);

				// A tagged sector that is not selected cannot be changed here
				if(!BuilderPlug.TDFEW.SelectedSectors.Contains(s))
					checkedListBoxSectors.SetItemCheckState(i, CheckState.Indeterminate);
			}
		}

		private void SetAll(bool ischecked)
		{
			for(int i = 0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, ischecked);
		}

		private void EditSector()
		{
			sector.SetCeilTexture(sectorTopFlat.TextureName);
			sector.SetFloorTexture(sectorBottomFlat.TextureName);
			sector.CeilHeight = sectorCeilingHeight.GetResult(sector.CeilHeight);
			sector.FloorHeight = sectorFloorHeight.GetResult(sector.FloorHeight);
			sector.Brightness = sectorBrightness.GetResult(sector.Brightness);

			DialogResult result = General.Interface.ShowEditSectors(new List<Sector> { sector });

			if(result == DialogResult.OK)
			{
				sectorTopFlat.TextureName = sector.CeilTexture;
				sectorBottomFlat.TextureName = sector.FloorTexture;
				sectorCeilingHeight.Text = sector.CeilHeight.ToString();
				sectorFloorHeight.Text = sector.FloorHeight.ToString();
				sectorBrightness.Text = sector.Brightness.ToString();
				tagsLabel.Text = String.Join(", ", sector.Tags.Select(o => o.ToString()).ToArray());
			}
		}

		// Keeps the list of checked sector numbers (a locked row never changes)
		private void OnItemCheck(int index, CheckState state)
		{
			if(checkedsectors == null || index < 0 || index >= checkedListBoxSectors.Items.Count) return;
			if(state == CheckState.Indeterminate) return;

			var matches = new Regex(@"\d+").Matches(checkedListBoxSectors.Items[index]);
			if(matches.Count == 0) return;
			int sectornum = int.Parse(matches[0].ToString());

			if(state == CheckState.Checked)
			{
				if(!checkedsectors.Contains(sectornum)) checkedsectors.Add(sectornum);
			}
			else
				checkedsectors.Remove(sectornum);
		}

		private void RecomputeBorderHeight()
		{
			borderHeightLabel.Text = (sectorCeilingHeight.GetResult(threeDFloor.TopHeight) - sectorFloorHeight.GetResult(threeDFloor.BottomHeight)).ToString();
		}

		#endregion
	}
}
