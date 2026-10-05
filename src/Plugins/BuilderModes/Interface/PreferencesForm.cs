using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.BuilderModes
{
	/// <summary>
	/// The plugin's tab ("Editing") of the program preferences: what the classic modes do on insert/select/paste, the ranges in pixels,
	/// the behaviors and the defaults of new sectors. Same settings and names as UDB's form; the controls are Avalonia, handed to the
	/// preferences window as the native control of a tab page.
	/// </summary>
	public class PreferencesForm : IDisposable
	{
		private readonly ComboBox heightbysidedef = Combo("Do nothing", "Change the ceiling height", "Change the floor height", "Change both floor and ceiling height", "Change the texture offset");
		private readonly ComboBox splitbehavior = Combo("Interpolate texture coordinates", "Duplicate texture coordinates", "Reset X coordinate, duplicate Y coordinate", "Reset X and Y coordinates");
		private readonly ComboBox scaletexturesonslopes = Combo("Use a scale of 1 as base", "Use current scale as base", "Don't scale");
		private readonly ComboBox eventlinelabelvisibility = Combo("Never show", "Forward only", "Reverse only", "Forward + Reverse");
		private readonly ComboBox eventlinelabelstyle = Combo("Action only", "Action + short arguments", "Action + full arguments");

		private readonly CheckBox editnewthing = Check("Edit thing properties when inserting a new thing");
		private readonly CheckBox editnewsector = Check("Edit sector properties after drawing a new sector");
		private readonly CheckBox additiveselect = Check("Additive selecting without holding Shift");
		private readonly CheckBox additivepaintselect = Check("Additive paint selecting without holding Shift");
		private readonly CheckBox autoclearselection = Check("Automatically clear selection in Classic Modes");
		private readonly CheckBox visualmodeclearselection = Check("Automatically clear selection in Visual Mode");
		private readonly CheckBox autodragonpaste = Check("Automatically drag selection on paste");
		private readonly CheckBox autoaligntexturesoncreate = Check("Auto-align textures of newly created linedefs");
		private readonly CheckBox dontMoveGeometryOutsideBounds = Check("Don't move selection if any part of it is outside of map boundary");
		private readonly CheckBox syncSelection = Check("Synchronize selection between Visual and Classic modes");
		private readonly CheckBox switchviewmodes = Check("Switch view modes when switching to the same Classic Mode");
		private readonly CheckBox autodrawonedit = Check("Start drawing when Edit pressed over empty space in Classic modes\nInsert new thing when Edit pressed over empty space in Things mode");
		private readonly CheckBox useoppositesmartpivothandle = Check("Opposite side/vertex is smart pivot handle on triangular sectors");
		private readonly CheckBox selectafterundoredo = Check("Select changed map elements after undo/redo");
		private readonly CheckBox usebuggyfloodselect = Check("Use buggy flood select in Visual Mode");

		private readonly NumberBox stitchrange = Number();
		private readonly NumberBox highlightrange = Number();
		private readonly NumberBox highlightthingsrange = Number();
		private readonly NumberBox splitlinedefsrange = Number();
		private readonly NumberBox mouseselectionthreshold = Number();
		private readonly NumberBox defaultbrightness = Number();
		private readonly NumberBox defaultceilheight = Number(true);
		private readonly NumberBox defaultfloorheight = Number(true);

		public PreferencesForm()
		{
			// Apply current settings to interface
			heightbysidedef.SelectedIndex = General.Settings.ReadPluginSetting("changeheightbysidedef", 0);
			editnewthing.IsChecked = General.Settings.ReadPluginSetting("editnewthing", true);
			editnewsector.IsChecked = General.Settings.ReadPluginSetting("editnewsector", false);
			additiveselect.IsChecked = General.Settings.ReadPluginSetting("additiveselect", false);
			additivepaintselect.IsChecked = General.Settings.ReadPluginSetting("additivepaintselect", additiveselect.IsChecked == true); // Use the same setting as additive select by default
			stitchrange.Text = General.Settings.ReadPluginSetting("stitchrange", 20).ToString();
			highlightrange.Text = General.Settings.ReadPluginSetting("highlightrange", 20).ToString();
			highlightthingsrange.Text = General.Settings.ReadPluginSetting("highlightthingsrange", 10).ToString();
			splitlinedefsrange.Text = General.Settings.ReadPluginSetting("splitlinedefsrange", 10).ToString();
			mouseselectionthreshold.Text = General.Settings.ReadPluginSetting("mouseselectionthreshold", 2).ToString();
			splitbehavior.SelectedIndex = (int)General.Settings.SplitLineBehavior;
			autoclearselection.IsChecked = BuilderPlug.Me.AutoClearSelection;
			visualmodeclearselection.IsChecked = BuilderPlug.Me.VisualModeClearSelection;
			autodragonpaste.IsChecked = BuilderPlug.Me.AutoDragOnPaste;
			autoaligntexturesoncreate.IsChecked = BuilderPlug.Me.AutoAlignTextureOffsetsOnCreate;
			dontMoveGeometryOutsideBounds.IsChecked = BuilderPlug.Me.DontMoveGeometryOutsideMapBoundary;
			syncSelection.IsChecked = BuilderPlug.Me.SyncSelection;
			switchviewmodes.IsChecked = General.Settings.SwitchViewModes;
			autodrawonedit.IsChecked = BuilderPlug.Me.AutoDrawOnEdit;
			defaultbrightness.Text = General.Settings.DefaultBrightness.ToString();
			defaultceilheight.Text = General.Settings.DefaultCeilingHeight.ToString();
			defaultfloorheight.Text = General.Settings.DefaultFloorHeight.ToString();
			scaletexturesonslopes.SelectedIndex = General.Settings.ReadPluginSetting("scaletexturesonslopes", 0);
			eventlinelabelvisibility.SelectedIndex = General.Settings.ReadPluginSetting("eventlinelabelvisibility", 3);
			eventlinelabelstyle.SelectedIndex = General.Settings.ReadPluginSetting("eventlinelabelstyle", 2);
			useoppositesmartpivothandle.IsChecked = General.Settings.ReadPluginSetting("useoppositesmartpivothandle", true);
			selectafterundoredo.IsChecked = General.Settings.ReadPluginSetting("selectchangedafterundoredo", false);
			usebuggyfloodselect.IsChecked = General.Settings.ReadPluginSetting("usebuggyfloodselect", false);
		}

		#region ================== Controls

		private static ComboBox Combo(params string[] items) { return new ComboBox { ItemsSource = items, SelectedIndex = 0, MinWidth = 260 }; }
		private static CheckBox Check(string text) { return new CheckBox { Content = text }; }
		private static NumberBox Number(bool negative = false) { return new NumberBox { AllowDecimal = false, AllowNegative = negative, AllowRelative = false, MinWidth = 100 }; }

		private static Control Labelled(string label, Control field, string unit = null)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			row.Children.Add(new TextBlock { Text = label, MinWidth = 220, VerticalAlignment = VerticalAlignment.Center });
			row.Children.Add(field);
			if(unit != null) row.Children.Add(new TextBlock { Text = unit, VerticalAlignment = VerticalAlignment.Center });
			return row;
		}

		private static Control Group(string title, params Control[] children)
		{
			var stack = new StackPanel { Spacing = 6 };
			stack.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold });
			foreach(Control c in children) stack.Children.Add(c);
			return new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, CornerRadius = new CornerRadius(3), Padding = new Thickness(10), Child = stack };
		}

		/// <summary>The controls of the tab.</summary>
		internal Control Build()
		{
			var page = new StackPanel { Spacing = 10, Margin = new Thickness(12) };
			page.Children.Add(Group("Options",
				editnewthing, editnewsector, additiveselect, additivepaintselect, autoclearselection, visualmodeclearselection, autodragonpaste,
				autoaligntexturesoncreate, dontMoveGeometryOutsideBounds, syncSelection, switchviewmodes, autodrawonedit,
				useoppositesmartpivothandle, selectafterundoredo));
			page.Children.Add(Group("Ranges",
				Labelled("Highlight geometry within:", highlightrange, "pixels"),
				Labelled("Highlight things within:", highlightthingsrange, "pixels"),
				Labelled("Stitch geometry within:", stitchrange, "pixels"),
				Labelled("Split linedefs within:", splitlinedefsrange, "pixels"),
				Labelled("Mouse selection threshold:", mouseselectionthreshold, "pixels")));
			page.Children.Add(Group("Behavior",
				Labelled("When sector height changes are used on a wall in Visual Mode:", heightbysidedef),
				Labelled("When splitting a linedef:", splitbehavior),
				Labelled("When auto-aligning textures on slopes:", scaletexturesonslopes),
				usebuggyfloodselect));
			page.Children.Add(Group("Event line labels", eventlinelabelvisibility, eventlinelabelstyle));
			page.Children.Add(Group("Default sector settings",
				Labelled("Default brightness:", defaultbrightness),
				Labelled("Default ceiling height:", defaultceilheight, "m.u."),
				Labelled("Default floor height:", defaultfloorheight, "m.u.")));
			return page;
		}

		#endregion

		#region ================== Events

		// When OK is pressed on the preferences dialog
		public void OnAccept(PreferencesController controller)
		{
			// Write preferred settings
			General.Settings.WritePluginSetting("changeheightbysidedef", heightbysidedef.SelectedIndex);
			General.Settings.WritePluginSetting("editnewthing", editnewthing.IsChecked == true);
			General.Settings.WritePluginSetting("editnewsector", editnewsector.IsChecked == true);
			General.Settings.WritePluginSetting("additiveselect", additiveselect.IsChecked == true);
			General.Settings.WritePluginSetting("additivepaintselect", additivepaintselect.IsChecked == true);
			General.Settings.WritePluginSetting("stitchrange", stitchrange.GetResult(0));
			General.Settings.WritePluginSetting("highlightrange", highlightrange.GetResult(0));
			General.Settings.WritePluginSetting("highlightthingsrange", highlightthingsrange.GetResult(0));
			General.Settings.WritePluginSetting("splitlinedefsrange", splitlinedefsrange.GetResult(0));
			General.Settings.WritePluginSetting("mouseselectionthreshold", mouseselectionthreshold.GetResult(0));
			General.Settings.WritePluginSetting("autoclearselection", autoclearselection.IsChecked == true);
			General.Settings.WritePluginSetting("visualmodeclearselection", visualmodeclearselection.IsChecked == true);
			General.Settings.WritePluginSetting("autodragonpaste", autodragonpaste.IsChecked == true);
			General.Settings.WritePluginSetting("autodrawonedit", autodrawonedit.IsChecked == true);
			General.Settings.WritePluginSetting("autoaligntextureoffsetsoncreate", autoaligntexturesoncreate.IsChecked == true);
			General.Settings.WritePluginSetting("dontmovegeometryoutsidemapboundary", dontMoveGeometryOutsideBounds.IsChecked == true);
			General.Settings.WritePluginSetting("syncselection", syncSelection.IsChecked == true);
			General.Settings.WritePluginSetting("scaletexturesonslopes", scaletexturesonslopes.SelectedIndex);
			General.Settings.WritePluginSetting("eventlinelabelvisibility", eventlinelabelvisibility.SelectedIndex);
			General.Settings.WritePluginSetting("eventlinelabelstyle", eventlinelabelstyle.SelectedIndex);
			General.Settings.WritePluginSetting("useoppositesmartpivothandle", useoppositesmartpivothandle.IsChecked == true);
			General.Settings.WritePluginSetting("selectchangedafterundoredo", selectafterundoredo.IsChecked == true);
			General.Settings.WritePluginSetting("usebuggyfloodselect", usebuggyfloodselect.IsChecked == true);
			General.Settings.SwitchViewModes = switchviewmodes.IsChecked == true;
			General.Settings.SplitLineBehavior = (SplitLineBehavior)splitbehavior.SelectedIndex;

			// Default sector values
			General.Settings.DefaultBrightness = General.Clamp(defaultbrightness.GetResult(192), 0, 255);

			int ceilHeight = defaultceilheight.GetResult(128);
			int floorHeight = defaultfloorheight.GetResult(0);
			if(ceilHeight < floorHeight) General.Swap(ref ceilHeight, ref floorHeight);

			General.Settings.DefaultCeilingHeight = ceilHeight;
			General.Settings.DefaultFloorHeight = floorHeight;
		}

		// When Cancel is pressed on the preferences dialog
		public void OnCancel(PreferencesController controller) { }

		#endregion

		#region ================== Methods

		// This sets up the form with the preferences controller
		public void Setup(PreferencesController controller)
		{
			controller.AddTab(new System.Windows.Forms.TabPage { Text = "Editing", NativeControl = new ScrollViewer { Content = Build() } });
			controller.OnAccept += OnAccept;
			controller.OnCancel += OnCancel;
		}

		public void Dispose() { }

		#endregion
	}
}
