using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Rendering;

namespace CodeImp.DoomBuilder.Windows
{
	public enum PreferenceKind { Bool, Int, Choice, Path, Color }

	/// <summary>
	/// One setting in the preferences dialog. The value is edited on the item and only reaches the program settings on
	/// <see cref="PreferencesModel.Apply"/>. Int and Choice values are ints (a choice is the index), a color is its ARGB int,
	/// a path is a string and a bool is a bool.
	/// </summary>
	public sealed class Preference
	{
		private object value;
		internal Func<object> Read;
		internal Action<object> Write;

		public string Key { get; internal set; }
		public string Tab { get; internal set; }
		public string Group { get; internal set; }
		public string Label { get; internal set; }
		public PreferenceKind Kind { get; internal set; }
		public int Min { get; internal set; }
		public int Max { get; internal set; }
		public string[] Choices { get; internal set; }
		/// <summary>Shows the value next to a slider (e.g. "75%"), when it is not just the number.</summary>
		internal Func<int, string> Display;
		/// <summary>A change needs the resources of the open map to be loaded again.</summary>
		public bool ReloadsResources { get; internal set; }

		public object Original { get; private set; }

		public object Value
		{
			get { return value; }
			set
			{
				if(Kind == PreferenceKind.Int || Kind == PreferenceKind.Choice) value = Math.Max(Min, Math.Min(Max, Convert.ToInt32(value)));
				this.value = value;
			}
		}

		public bool IsChanged { get { return !Equals(Value, Original); } }
		public string ValueText { get { return Kind == PreferenceKind.Int && Display != null ? Display((int)Value) : Convert.ToString(Value); } }

		internal void Load() { Original = Read(); Value = Original; Original = Value; }
		internal void Save() { Write(Value); }
	}

	/// <summary>
	/// The logic of UDB's preferences dialog without any UI. The settings are described as <see cref="Preference"/> items grouped
	/// into tabs; the window builds its controls from them. Covers the interface, display, recovery and the main map colors.
	/// Not covered yet: action shortcut keys, script editor, paste options, toasts and the tabs plugins add.
	/// </summary>
	public sealed class PreferencesModel
	{
		public const string InterfaceTab = "Interface", DisplayTab = "Display", RecoveryTab = "Recovery", ColorsTab = "Colors";
		private const float ViewDistanceStep = 500.0f;

		private readonly List<Preference> items = new List<Preference>();
		private readonly bool oldautosave;

		public PreferencesModel()
		{
			ProgramConfiguration s = General.Settings;
			oldautosave = s.Autosave;

			// ---- interface: the visual modes and the mouse
			Slider(InterfaceTab, "Visual mode", "Image brightness", "imagebrightness", 0, 10, () => s.ImageBrightness, v => s.ImageBrightness = v, reload: true);
			Alpha(InterfaceTab, "Visual mode", "Double sided lines transparency", "doublesidedalpha", () => s.DoubleSidedAlpha, v => s.DoubleSidedAlpha = v);
			Alpha(InterfaceTab, "Classic modes", "Active things transparency", "activethingsalpha", () => s.ActiveThingsAlpha, v => s.ActiveThingsAlpha = v);
			Alpha(InterfaceTab, "Classic modes", "Inactive things transparency", "inactivethingsalpha", () => s.InactiveThingsAlpha, v => s.InactiveThingsAlpha = v);
			Alpha(InterfaceTab, "Classic modes", "Hidden things transparency", "hiddenthingsalpha", () => s.HiddenThingsAlpha, v => s.HiddenThingsAlpha = v);
			Choice(InterfaceTab, "Visual mode", "Default view mode", "defaultviewmode", new[] { "Wireframe", "Brightness Levels", "Floor Textures", "Ceiling Textures" }, () => s.DefaultViewMode, v => s.DefaultViewMode = v);
			Slider(InterfaceTab, "Visual mode", "Field of view", "fieldofview", 5, 17, () => s.VisualFOV / 10, v => s.VisualFOV = v * 10, display: v => v * 10 + "°");
			Slider(InterfaceTab, "Visual mode", "Mouse sensitivity", "mousespeed", 1, 20, () => s.MouseSpeed / 100, v => s.MouseSpeed = v * 100);
			Slider(InterfaceTab, "Visual mode", "Move speed", "movespeed", 1, 20, () => s.MoveSpeed / 100, v => s.MoveSpeed = v * 100);
			Slider(InterfaceTab, "Visual mode", "View distance", "viewdistance", 1, 128, () => (int)(s.ViewDistance / ViewDistanceStep), v => s.ViewDistance = v * ViewDistanceStep, display: v => (v * ViewDistanceStep) + " map units");
			Slider(InterfaceTab, "Visual mode", "Vertex scale", "vertexscale3d", 2, 15, () => (int)(s.GZVertexScale3D * 10), v => s.GZVertexScale3D = v * 0.1f, display: v => v * 10 + "%");
			Flag(InterfaceTab, "Visual mode", "Invert Y axis", "invertyaxis", () => s.InvertYAxis, v => s.InvertYAxis = v);
			Flag(InterfaceTab, "Visual mode", "Animate visual selection", "animatevisualselection", () => s.AnimateVisualSelection, v => s.AnimateVisualSelection = v);
			Slider(InterfaceTab, "Classic modes", "Auto scroll speed", "autoscrollspeed", 0, 5, () => s.AutoScrollSpeed, v => s.AutoScrollSpeed = v);
			Slider(InterfaceTab, "Classic modes", "Zoom speed", "zoomfactor", 1, 10, () => s.ZoomFactor, v => s.ZoomFactor = v);
			Slider(InterfaceTab, "Classic modes", "Vertex size", "vertexscale2d", 1, 40, () => (int)(s.GZVertexScale2D * 4.0), v => s.GZVertexScale2D = v / 4.0f, display: v => v * 25 + "%" + (v == 4 ? " (default)" : ""));

			// ---- interface: panels and toolbars
			Choice(InterfaceTab, "Side panel", "Dockers position", "dockersposition", new[] { "Left", "Right", "None" }, () => s.DockersPosition, v => s.DockersPosition = v);
			Flag(InterfaceTab, "Side panel", "Collapse dockers automatically", "collapsedockers", () => s.CollapseDockers, v => s.CollapseDockers = v);
			Flag(InterfaceTab, "Toolbars", "File", "toolbar_file", () => s.ToolbarFile, v => s.ToolbarFile = v);
			Flag(InterfaceTab, "Toolbars", "Script", "toolbar_script", () => s.ToolbarScript, v => s.ToolbarScript = v);
			Flag(InterfaceTab, "Toolbars", "Undo / Redo", "toolbar_undo", () => s.ToolbarUndo, v => s.ToolbarUndo = v);
			Flag(InterfaceTab, "Toolbars", "Copy / Paste", "toolbar_copy", () => s.ToolbarCopy, v => s.ToolbarCopy = v);
			Flag(InterfaceTab, "Toolbars", "Prefabs", "toolbar_prefabs", () => s.ToolbarPrefabs, v => s.ToolbarPrefabs = v);
			Flag(InterfaceTab, "Toolbars", "Things filter", "toolbar_filter", () => s.ToolbarFilter, v => s.ToolbarFilter = v);
			Flag(InterfaceTab, "Toolbars", "View modes", "toolbar_viewmodes", () => s.ToolbarViewModes, v => s.ToolbarViewModes = v);
			Flag(InterfaceTab, "Toolbars", "Geometry", "toolbar_geometry", () => s.ToolbarGeometry, v => s.ToolbarGeometry = v);
			Flag(InterfaceTab, "Toolbars", "Testing", "toolbar_testing", () => s.ToolbarTesting, v => s.ToolbarTesting = v);
			Flag(InterfaceTab, "Toolbars", "GZDoom", "toolbar_gzdoom", () => s.GZToolbarGZDoom, v => s.GZToolbarGZDoom = v);

			// ---- interface: general
			Slider(InterfaceTab, "General", "Recent files in the menu", "recentfiles", 8, 25, () => s.MaxRecentFiles, v => s.MaxRecentFiles = v);
			Folder(InterfaceTab, "General", "Screenshots folder", "screenshotspath", () => s.ScreenshotsPath, v => s.ScreenshotsPath = v);
			Flag(InterfaceTab, "General", "Start the game right after saving for a test", "autolaunchontest", () => s.AutoLaunchOnTest, v => s.AutoLaunchOnTest = v);
			Flag(InterfaceTab, "General", "Locate the texture group when selecting a texture", "locatetexturegroup", () => s.LocateTextureGroup, v => s.LocateTextureGroup = v);
			Flag(InterfaceTab, "General", "Remember the selected tab of the edit windows", "storeedittab", () => s.StoreSelectedEditTab, v => s.StoreSelectedEditTab = v);
			Flag(InterfaceTab, "General", "Check for updates on startup", "checkforupdates", () => s.CheckForUpdates, v => s.CheckForUpdates = v);

			// ---- display
			Flag(DisplayTab, "Classic modes", "Show texture sizes", "showtexturesizes", () => s.ShowTextureSizes, v => s.ShowTextureSizes = v);
			Flag(DisplayTab, "Classic modes", "Texture sizes below the name", "texturesizesbelow", () => s.TextureSizesBelow, v => s.TextureSizesBelow = v);
			Flag(DisplayTab, "Classic modes", "Show frames per second", "showfps", () => s.ShowFPS, v => s.ShowFPS = v);
			Flag(DisplayTab, "Classic modes", "Flat shaded vertices", "flatshadevertices", () => s.FlatShadeVertices, v => s.FlatShadeVertices = v);
			Flag(DisplayTab, "Classic modes", "Old highlight mode", "oldhighlightmode", () => s.GZOldHighlightMode, v => s.GZOldHighlightMode = v);
			Flag(DisplayTab, "Classic modes", "Mark extra floors", "markextrafloors", () => s.GZMarkExtraFloors, v => s.GZMarkExtraFloors = v);
			Flag(DisplayTab, "Classic modes", "Stretch the view to the map's aspect", "stretchview", () => s.GZStretchView, v => s.GZStretchView = v);
			Flag(DisplayTab, "Classic modes", "Plot linedefs in parallel", "parallellinedefs", () => s.ParallelizedLinedefPlotting, v => s.ParallelizedLinedefPlotting = v);
			Flag(DisplayTab, "Classic modes", "Plot vertices in parallel", "parallelvertices", () => s.ParallelizedVertexPlotting, v => s.ParallelizedVertexPlotting = v);
			Flag(DisplayTab, "Filtering", "Bilinear filtering in classic modes", "classicbilinear", () => s.ClassicBilinear, v => s.ClassicBilinear = v);
			Flag(DisplayTab, "Filtering", "Bilinear filtering in visual mode", "visualbilinear", () => s.VisualBilinear, v => s.VisualBilinear = v);
			Flag(DisplayTab, "Filtering", "High quality display", "qualitydisplay", () => s.QualityDisplay, v => s.QualityDisplay = v);
			Flag(DisplayTab, "Filtering", "Black background in the image browsers", "blackbrowsers", () => s.BlackBrowsers, v => s.BlackBrowsers = v);
			Flag(DisplayTab, "GZDoom", "Synchronize the 2D and 3D cameras", "synchcameras", () => s.GZSynchCameras, v => s.GZSynchCameras = v);
			Slider(DisplayTab, "GZDoom", "Dynamic lights shown", "dynlightcount", 1, 16, () => s.GZMaxDynamicLights / 16, v => s.GZMaxDynamicLights = v * 16, display: v => (v * 16).ToString());

			// ---- recovery
			Flag(RecoveryTab, "Autosave", "Save a backup of the map regularly", "autosave", () => s.Autosave, v => s.Autosave = v);
			Slider(RecoveryTab, "Autosave", "Backups to keep", "autosavecount", 1, 50, () => s.AutosaveCount, v => s.AutosaveCount = v);
			Slider(RecoveryTab, "Autosave", "Minutes between backups", "autosaveinterval", 1, 60, () => s.AutosaveInterval, v => s.AutosaveInterval = v);

			// ---- colors
			ColorCollection c = General.Colors;
			Hue("Background", "colorbackcolor", () => c.Background, v => c.Background = v);
			Hue("Vertices", "colorvertices", () => c.Vertices, v => c.Vertices = v);
			Hue("Lines", "colorlinedefs", () => c.Linedefs, v => c.Linedefs = v);
			Hue("Highlight", "colorhighlight", () => c.Highlight, v => c.Highlight = v);
			Hue("Selection", "colorselection", () => c.Selection, v => c.Selection = v);
			Hue("Indication", "colorindication", () => c.Indication, v => c.Indication = v);
			Hue("Grid", "colorgrid", () => c.Grid, v => c.Grid = v);
			Hue("64 grid", "colorgrid64", () => c.Grid64, v => c.Grid64 = v);
			Hue("Model wireframe", "colormd3", () => c.ModelWireframe, v => c.ModelWireframe = v);
			Hue("Info line", "colorinfo", () => c.InfoLine, v => c.InfoLine = v);
			Hue("Guidelines", "colorguidelines", () => c.Guideline, v => c.Guideline = v);
			Hue("3D floors", "color3dfloors", () => c.ThreeDFloor, v => c.ThreeDFloor = v);

			foreach(Preference p in items) p.Load();
		}

		#region ================== Descriptors

		private void Add(Preference p) { items.Add(p); }

		private void Flag(string tab, string group, string label, string key, Func<bool> read, Action<bool> write)
		{
			Add(new Preference { Tab = tab, Group = group, Label = label, Key = key, Kind = PreferenceKind.Bool, Read = () => read(), Write = v => write((bool)v) });
		}

		private void Slider(string tab, string group, string label, string key, int min, int max, Func<int> read, Action<int> write,
							Func<int, string> display = null, bool reload = false)
		{
			Add(new Preference { Tab = tab, Group = group, Label = label, Key = key, Kind = PreferenceKind.Int, Min = min, Max = max, Display = display, ReloadsResources = reload,
								 Read = () => read(), Write = v => write((int)v) });
		}

		// The transparency settings are 0..1 (1 = opaque); the slider counts the steps of transparency, 0 to 10
		private void Alpha(string tab, string group, string label, string key, Func<float> read, Action<float> write)
		{
			Add(new Preference { Tab = tab, Group = group, Label = label, Key = key, Kind = PreferenceKind.Int, Min = 0, Max = 10, Display = v => v * 10 + "%",
								 Read = () => (int)Math.Round((1.0f - read()) * 10.0f), Write = v => write(1.0f - ((int)v * 0.1f)) });
		}

		private void Choice(string tab, string group, string label, string key, string[] choices, Func<int> read, Action<int> write)
		{
			Add(new Preference { Tab = tab, Group = group, Label = label, Key = key, Kind = PreferenceKind.Choice, Min = 0, Max = choices.Length - 1, Choices = choices,
								 Read = () => read(), Write = v => write((int)v) });
		}

		private void Folder(string tab, string group, string label, string key, Func<string> read, Action<string> write)
		{
			Add(new Preference { Tab = tab, Group = group, Label = label, Key = key, Kind = PreferenceKind.Path, Read = () => read(), Write = v => write(((string)v ?? "").Trim()) });
		}

		private void Hue(string label, string key, Func<PixelColor> read, Action<PixelColor> write)
		{
			Add(new Preference { Tab = ColorsTab, Group = "Map", Label = label, Key = key, Kind = PreferenceKind.Color,
								 Read = () => read().ToInt(), Write = v => write(PixelColor.FromInt((int)v)) });
		}

		#endregion

		#region ================== Properties and methods

		public IReadOnlyList<Preference> Items { get { return items; } }
		public IEnumerable<string> Tabs { get { return items.Select(i => i.Tab).Distinct(); } }
		public IEnumerable<Preference> InTab(string tab) { return items.Where(i => i.Tab == tab); }
		public Preference Find(string key) { return items.First(i => i.Key == key); }

		/// <summary>True when something changed that needs the open map's resources loaded again.</summary>
		public bool ReloadResources { get { return items.Any(i => i.ReloadsResources && i.IsChanged); } }

		/// <summary>Why the preferences cannot be accepted (null when they can).</summary>
		public string Validate()
		{
			Preference path = Find("screenshotspath");
			string folder = ((string)path.Value ?? "").Trim();
			if(path.IsChanged && !Directory.Exists(folder)) return "Screenshots folder does not exist!\nPlease enter a correct path.";
			return null;
		}

		/// <summary>Writes every value to the program settings, and starts or stops the autosave when that was switched.</summary>
		public void Apply()
		{
			foreach(Preference p in items) p.Save();
			General.Colors.CreateAssistColors();

			if(General.Map != null && oldautosave != General.Settings.Autosave)
			{
				if(General.Settings.Autosave) General.AutoSaver.InitializeTimer();
				else General.AutoSaver.StopTimer();
			}
		}

		#endregion
	}
}
