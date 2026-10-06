// The window of the stair sector builder mode: the controls (what UDB's designer made) and the window around them. It is shown next to the map
// while the mode runs; closing it with the close button is a Cancel of the mode.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CodeImp.DoomBuilder.StairSectorBuilderMode
{
	public partial class StairSectorBuilderForm
	{
		private readonly Window window = new Window { Title = "Stair Sector Builder", SizeToContent = SizeToContent.WidthAndHeight, CanResize = false, ShowInTaskbar = false };
		private bool closingbymode;

		private readonly FxButton btnOK = new FxButton("OK");
		private readonly FxButton btnCancel = new FxButton("Cancel");
		private readonly FxNum numberofsectors = new FxNum(false);

		private readonly FxTabs tabcontrol = new FxTabs();
		private readonly FxTab tabPage1 = new FxTab("Straight", null), tabPage2 = new FxTab("Auto curve", null), tabPage3 = new FxTab("Catmull Rom", null);
		private readonly FxCheck distinctbaseheights = new FxCheck("Distinct base heights");
		private readonly FxNum spacing = new FxNum(false);
		private readonly FxCheck distinctsectors = new FxCheck("Distinct sectors") { Enabled = false };
		private readonly FxCheck singledirection = new FxCheck("Single direction") { Enabled = false };
		private readonly FxCheck singlesteps = new FxCheck("Single steps") { Enabled = false };
		private readonly FxRadio sideback = new FxRadio("Back", "stairside");
		private readonly FxRadio sidefront = new FxRadio("Front", "stairside") { Checked = true };
		private readonly FxNum sectordepth = new FxNum(false);
		private readonly FxCombo autocurveflipping = new FxCombo("None", "Source direction", "Destination direction");
		private readonly FxNum autocurveoutervertexmultiplier = new FxNum(false);
		private readonly FxNum autocurveinnervertexmultiplier = new FxNum(false);
		private readonly FxCombo splineflipping = new FxCombo("None", "Source direction", "Destination direction");
		private readonly FxNum splineoutervertexmultiplier = new FxNum(false);
		private readonly FxNum splineinnervertexmultiplier = new FxNum(false);
		private readonly FxNum numberofcontrolpoints = new FxNum(false);

		private readonly FxCheck floorflat = new FxCheck("Floor flat");
		private readonly FxFlat floorflattexture = new FxFlat { Enabled = false };
		private readonly FxCheck ceilingflat = new FxCheck("Ceiling flat");
		private readonly FxFlat ceilingflattexture = new FxFlat { Enabled = false };

		private readonly FxCheck floorheightmodification = new FxCheck("Floor height") { Checked = true };
		private readonly FxNum floorbase = new FxNum(true);
		private readonly FxNum floorheightmod = new FxNum(true);
		private readonly FxLabel floorfirst = new FxLabel("floorfirst");
		private readonly FxLabel floorlast = new FxLabel("floorlast");
		private readonly FxCheck ceilingheightmodification = new FxCheck("Ceiling height") { Checked = true };
		private readonly FxNum ceilingbase = new FxNum(true);
		private readonly FxNum ceilingheightmod = new FxNum(true);
		private readonly FxLabel ceilingfirst = new FxLabel("ceilingfirst");
		private readonly FxLabel ceilinglast = new FxLabel("ceilinglast");

		private readonly FxCheck uppertexture = new FxCheck("Upper texture") { Checked = true };
		private readonly FxTexture uppertexturetexture = new FxTexture();
		private readonly FxCheck upperunpegged = new FxCheck("Unpegged") { Checked = true };
		private readonly FxCheck middletexture = new FxCheck("Middle texture");
		private readonly FxTexture middletexturetexture = new FxTexture { Enabled = false };
		private readonly FxCheck lowertexture = new FxCheck("Lower texture") { Checked = true };
		private readonly FxTexture lowertexturetexture = new FxTexture();
		private readonly FxCheck lowerunpegged = new FxCheck("Unpegged") { Checked = true };

		private readonly FxText prefabname = new FxText();
		private readonly FxPrefabList prefabs = new FxPrefabList();
		private readonly FxButton prefabsave = new FxButton("Save");
		private readonly FxButton prefabload = new FxButton("Load");
		private readonly FxButton prefabdelete = new FxButton("Delete");
		private readonly FxButton prefabdefault = new FxButton("Set default");

		// For the tests
		internal Window Window { get { return window; } }
		internal FxButton OkButton { get { return btnOK; } }
		internal FxButton CancelButton { get { return btnCancel; } }
		internal FxNum NumberOfSectorsBox { get { return numberofsectors; } }
		internal FxNum SectorDepthBox { get { return sectordepth; } }
		internal FxNum SpacingBox { get { return spacing; } }
		internal FxNum FloorBaseBox { get { return floorbase; } }
		internal FxNum FloorModBox { get { return floorheightmod; } }
		internal FxNum CeilingBaseBox { get { return ceilingbase; } }
		internal FxNum CeilingModBox { get { return ceilingheightmod; } }
		internal FxLabel FloorFirst { get { return floorfirst; } }
		internal FxLabel FloorLast { get { return floorlast; } }
		internal FxLabel CeilingFirst { get { return ceilingfirst; } }
		internal FxLabel CeilingLast { get { return ceilinglast; } }
		internal FxCheck FloorHeightCheck { get { return floorheightmodification; } }
		internal FxCheck SingleStepsCheck { get { return singlesteps; } }
		internal FxCheck SingleDirectionCheck { get { return singledirection; } }
		internal FxCheck DistinctSectorsCheck { get { return distinctsectors; } }
		internal FxRadio FrontRadio { get { return sidefront; } }
		internal FxRadio BackRadio { get { return sideback; } }
		internal FxCombo AutoCurveFlipping { get { return autocurveflipping; } }
		internal FxCombo SplineFlipping { get { return splineflipping; } }
		internal FxNum AutoInner { get { return autocurveinnervertexmultiplier; } }
		internal FxNum SplineInner { get { return splineinnervertexmultiplier; } }
		internal FxNum ControlPointsBox { get { return numberofcontrolpoints; } }
		internal FxText PrefabNameBox { get { return prefabname; } }
		internal FxPrefabList PrefabList { get { return prefabs; } }
		internal FxButton PrefabSaveButton { get { return prefabsave; } }
		internal FxButton PrefabLoadButton { get { return prefabload; } }
		internal FxButton PrefabDeleteButton { get { return prefabdelete; } }
		internal FxButton PrefabDefaultButton { get { return prefabdefault; } }
		internal FxCheck UpperTextureCheck { get { return uppertexture; } }
		internal FxTexture UpperTextureBox { get { return uppertexturetexture; } }
		internal FxCheck FloorFlatCheck { get { return floorflat; } }
		internal FxFlat FloorFlatBox { get { return floorflattexture; } }

		private static StackPanel Row(params Control[] controls)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			foreach(Control c in controls) row.Children.Add(c);
			return row;
		}

		private static TextBlock Label(string text) { return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, MinWidth = 130 }; }

		// A column of "label: box" lines
		private static StackPanel Lines(params (string, Control)[] lines)
		{
			var panel = new StackPanel { Spacing = 4, Margin = new Thickness(6) };
			foreach(var line in lines) panel.Children.Add(Row(Label(line.Item1), line.Item2));
			return panel;
		}

		private void BuildView()
		{
			// The three kinds of stairs
			var straight = new StackPanel { Spacing = 4, Margin = new Thickness(6) };
			straight.Children.Add(Row(Label("Sector depth"), sectordepth.View));
			straight.Children.Add(Row(Label("Spacing"), spacing.View));
			straight.Children.Add(distinctbaseheights.View);
			var side = new FxGroup("Side");
			side.Content.Children.Add(Row(sidefront.View, sideback.View));
			var appearance = new FxGroup("Appearance");
			appearance.Content.Children.Add(singlesteps.View);
			appearance.Content.Children.Add(singledirection.View);
			appearance.Content.Children.Add(distinctsectors.View);
			straight.Children.Add(side.View);
			straight.Children.Add(appearance.View);
			tabPage1.Item.Content = straight;
			tabPage2.Item.Content = Lines(("Inner vertex multiplier", autocurveinnervertexmultiplier.View), ("Outer vertex multiplier", autocurveoutervertexmultiplier.View), ("Flipping", autocurveflipping.View));
			tabPage3.Item.Content = Lines(("Control points", numberofcontrolpoints.View), ("Inner vertex multiplier", splineinnervertexmultiplier.View), ("Outer vertex multiplier", splineoutervertexmultiplier.View), ("Flipping", splineflipping.View));
			tabcontrol.Add(tabPage1);
			tabcontrol.Add(tabPage2);
			tabcontrol.Add(tabPage3);

			var general = new FxGroup("General");
			general.Content.Children.Add(Row(Label("Number of sectors"), numberofsectors.View));

			// The heights
			var floor = new FxGroup("Floor");
			floor.Content.Children.Add(floorheightmodification.View);
			floor.Content.Children.Add(Row(Label("Modify"), floorheightmod.View));
			floor.Content.Children.Add(Row(Label("Base"), floorbase.View));
			floor.Content.Children.Add(Row(Label("First:"), floorfirst.View, Label("Last:"), floorlast.View));
			var ceiling = new FxGroup("Ceiling");
			ceiling.Content.Children.Add(ceilingheightmodification.View);
			ceiling.Content.Children.Add(Row(Label("Modify"), ceilingheightmod.View));
			ceiling.Content.Children.Add(Row(Label("Base"), ceilingbase.View));
			ceiling.Content.Children.Add(Row(Label("First:"), ceilingfirst.View, Label("Last:"), ceilinglast.View));

			// The textures
			var floorflatgroup = new FxGroup("");
			floorflatgroup.Content.Children.Add(floorflat.View);
			floorflatgroup.Content.Children.Add(floorflattexture.View);
			var ceilingflatgroup = new FxGroup("");
			ceilingflatgroup.Content.Children.Add(ceilingflat.View);
			ceilingflatgroup.Content.Children.Add(ceilingflattexture.View);
			var upper = new FxGroup("");
			upper.Content.Children.Add(uppertexture.View);
			upper.Content.Children.Add(uppertexturetexture.View);
			upper.Content.Children.Add(upperunpegged.View);
			var middle = new FxGroup("");
			middle.Content.Children.Add(middletexture.View);
			middle.Content.Children.Add(middletexturetexture.View);
			var lower = new FxGroup("");
			lower.Content.Children.Add(lowertexture.View);
			lower.Content.Children.Add(lowertexturetexture.View);
			lower.Content.Children.Add(lowerunpegged.View);

			// The prefabs
			var prefabgroup = new FxGroup("Prefabs");
			prefabgroup.Content.Children.Add(prefabs.View);
			prefabgroup.Content.Children.Add(prefabname.View);
			prefabgroup.Content.Children.Add(Row(prefabsave.View, prefabload.View, prefabdelete.View, prefabdefault.View));

			var left = new StackPanel { Spacing = 8 };
			left.Children.Add(general.View);
			left.Children.Add(tabcontrol.View);
			left.Children.Add(floor.View);
			left.Children.Add(ceiling.View);
			var middlecolumn = new StackPanel { Spacing = 8 };
			middlecolumn.Children.Add(Row(floorflatgroup.View, ceilingflatgroup.View));
			middlecolumn.Children.Add(Row(upper.View, middle.View, lower.View));
			middlecolumn.Children.Add(prefabgroup.View);
			var layout = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
			layout.Children.Add(Row(left, middlecolumn));
			layout.Children.Add(ButtonRow(btnOK, btnCancel));
			window.Content = layout;

			// What the controls do
			btnOK.Click += btnOK_Click;
			btnCancel.Click += btnCancel_Click;
			numberofsectors.WhenTextChanged += numberofsectors_WhenTextChanged;
			sectordepth.WhenTextChanged += sectordepth_WhenTextChanged;
			spacing.WhenTextChanged += spacing_WhenTextChanged;
			sidefront.View.IsCheckedChanged += (s, e) => sidefront_CheckedChanged(s, EventArgs.Empty);
			sideback.View.IsCheckedChanged += (s, e) => sideback_CheckedChanged(s, EventArgs.Empty);
			tabcontrol.SelectedIndexChanged += tabcontrol_SelectedIndexChanged;
			autocurveinnervertexmultiplier.WhenTextChanged += autocurveinnervertexmultiplier_WhenTextChanged;
			autocurveoutervertexmultiplier.WhenTextChanged += autocurveoutervertexmultiplier_WhenTextChanged;
			splineinnervertexmultiplier.WhenTextChanged += splineinnervertexmultiplier_WhenTextChanged;
			splineoutervertexmultiplier.WhenTextChanged += splineoutervertexmultiplier_WhenTextChanged;
			numberofcontrolpoints.WhenTextChanged += numberofcontrolpoints_WhenTextChanged;
			autocurveflipping.SelectedIndexChanged += autocurveflipping_SelectedIndexChanged;
			splineflipping.SelectedIndexChanged += splineflipping_SelectedIndexChanged;
			singlesteps.CheckedChanged += singleseteps_CheckedChanged;
			singledirection.CheckedChanged += singledirection_CheckedChanged;
			distinctsectors.CheckedChanged += distinctsectors_CheckedChanged;
			distinctbaseheights.CheckedChanged += distinctbaseheights_CheckedChanged;
			floorheightmodification.CheckedChanged += floorheightmodification_CheckedChanged;
			ceilingheightmodification.CheckedChanged += ceilingheightmodification_CheckedChanged;
			floorheightmod.WhenTextChanged += floorheightmod_WhenTextChanged;
			ceilingheightmod.WhenTextChanged += ceilingheightmod_WhenTextChanged;
			floorbase.WhenTextChanged += floorbase_WhenTextChanged;
			ceilingbase.WhenTextChanged += ceilingbase_WhenTextChanged;
			floorflat.CheckedChanged += floorflat_CheckedChanged;
			ceilingflat.CheckedChanged += ceilingflat_CheckedChanged;
			uppertexture.CheckedChanged += uppertexture_CheckedChanged;
			middletexture.CheckedChanged += middletexture_CheckedChanged;
			lowertexture.CheckedChanged += lowertexture_CheckedChanged;
			prefabsave.Click += prefabsave_Click;
			prefabload.Click += prefabload_Click;
			prefabdelete.Click += prefabdelete_Click;
			prefabdefault.Click += prefabdefault_Click;
			prefabs.DoubleClick += prefabs_DoubleClick;
			window.Closing += StairSectorBuilderForm_FormClosing;
		}

		private static StackPanel ButtonRow(FxButton ok, FxButton cancel)
		{
			ok.View.MinWidth = cancel.View.MinWidth = 90;
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
			row.Children.Add(ok.View);
			row.Children.Add(cancel.View);
			return row;
		}

		/// <summary>Takes the window away when the mode ends (the next run makes a new one).</summary>
		public void Hide() { Close(); }

		/// <summary>Closes the window, for good: closing it with the close button is a Cancel of the mode instead.</summary>
		public void Close()
		{
			closingbymode = true;
			window.Close();
		}

		public void Dispose() { Close(); }
	}
}
