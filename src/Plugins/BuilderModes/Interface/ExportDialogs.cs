// The settings dialogs of the exporters (Wavefront .obj, idStudio, image): UDB's WavefrontSettingsForm, idStudioExporterForm and
// ImageExportSettingsForm as Avalonia windows, with the same members the exporters read. Shown modally over the main window.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CodeImp.DoomBuilder.BuilderModes.IO;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvControl = Avalonia.Controls.Control;
using DialogResult = System.Windows.Forms.DialogResult;
using Panel = Avalonia.Controls.Panel;
using ComboBox = Avalonia.Controls.ComboBox;

namespace CodeImp.DoomBuilder.BuilderModes.Interface
{
	/// <summary>The small pieces the three dialogs have in common.</summary>
	internal static class ExportUi
	{
		public static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals = 0)
		{
			return new NumericUpDown { Minimum = min, Maximum = max, Value = value, FormatString = decimals == 0 ? "0" : "0." + new string('0', decimals), Increment = decimals == 0 ? 1 : 0.1m, MinWidth = 110 };
		}

		/// <summary>A label with its control beside it (the control takes the rest of the row).</summary>
		public static AvControl Labeled(string label, AvControl control, AvControl extra = null)
		{
			var row = new DockPanel { LastChildFill = true };
			var text = new TextBlock { Text = label, Width = 110, VerticalAlignment = VerticalAlignment.Center };
			DockPanel.SetDock(text, Dock.Left);
			row.Children.Add(text);
			if(extra != null) { DockPanel.SetDock(extra, Dock.Right); extra.Margin = new Thickness(6, 0, 0, 0); row.Children.Add(extra); }
			row.Children.Add(control);
			return row;
		}

		public static void Add(Panel panel, params AvControl[] controls)
		{
			foreach(AvControl c in controls) { c.Margin = new Thickness(0, 3); panel.Children.Add(c); }
		}

		/// <summary>The folder chosen by the user, or null when cancelled.</summary>
		public static string PickFolder(string title, string initial)
		{
			Window owner = DialogHost.Owner == null ? null : DialogHost.Owner();
			if(owner == null) return null;
			IStorageFolder start = !string.IsNullOrEmpty(initial) && Directory.Exists(initial) ? DialogPump.Run(() => owner.StorageProvider.TryGetFolderFromPathAsync(initial)) : null;
			var folders = DialogPump.Run(() => owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, SuggestedStartLocation = start, AllowMultiple = false }));
			if(folders == null || folders.Count == 0) return null;
			return folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
		}

		/// <summary>The file chosen with the system's save dialog, or null when cancelled.</summary>
		public static string PickSaveFile(string title, string filter, string path)
		{
			SaveFileDialog dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = path, OverwritePrompt = true };
			string dir = string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
			if(!string.IsNullOrEmpty(dir)) dialog.InitialDirectory = dir;
			return General.Dialogs.ShowFileDialog(dialog) == DialogResult.OK ? dialog.FileName : null;
		}

		public static void Error(string text, string caption)
		{
			MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	/// <summary>Export to Wavefront .obj (UDB's WavefrontSettingsForm).</summary>
	public class WavefrontSettingsForm : IDisposable, IWin32Window
	{
		private readonly int sectorsCount;
		private readonly TextBox tbExportPath = new TextBox();
		private readonly AvButton browse = new AvButton { Content = "..." };
		private readonly AvCheckBox cbExportTextures = new AvCheckBox { Content = "Export textures" };
		private readonly AvCheckBox cbExportForGZDoom = new AvCheckBox { Content = "Export for GZDoom" };
		private readonly NumericUpDown nudScale = ExportUi.Number(-2048, 2048, 1, 4);
		private readonly TextBox tbBasePath = new TextBox();
		private readonly TextBox tbActorPath = new TextBox();
		private readonly TextBox tbModelPath = new TextBox();
		private readonly TextBox tbActorName = new TextBox();
		private readonly TextBox tbSprite = new TextBox { MaxLength = 4, Width = 70, HorizontalAlignment = HorizontalAlignment.Left };
		private readonly ListBox lbSkipTextures = new ListBox { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle, Height = 90 };
		private readonly List<string> skipped = new List<string>();
		private readonly AvCheckBox cbIgnoreControlSectors = new AvCheckBox { Content = "Ignore 3D floor control sectors", IsChecked = true };
		private readonly AvCheckBox cbNormalizeLowestVertex = new AvCheckBox { Content = "Normalize lowest vertex z to 0", IsChecked = true };
		private readonly AvCheckBox cbCenterModel = new AvCheckBox { Content = "Center model", IsChecked = true };
		private readonly AvCheckBox cbGenerateCode = new AvCheckBox { Content = "Generate ZScript/DECORATE" };
		private readonly AvCheckBox cbGenerateModeldef = new AvCheckBox { Content = "Generate MODELDEF" };
		private readonly RadioButton rbZScript = new RadioButton { Content = "ZScript", IsChecked = true, GroupName = "actorformat" };
		private readonly RadioButton rbDecorate = new RadioButton { Content = "DECORATE", GroupName = "actorformat" };
		private readonly AvCheckBox cbSolid = new AvCheckBox { Content = "Solid" };
		private readonly AvCheckBox cbSpawnOnCeiling = new AvCheckBox { Content = "Spawn on ceiling" };
		private readonly AvCheckBox cbNoGravity = new AvCheckBox { Content = "No gravity" };
		private readonly List<AvControl> gzcontrols = new List<AvControl>();
		private readonly List<AvControl> codecontrols = new List<AvControl>();
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		public string FilePath { get { return (tbExportPath.Text ?? "").Trim(); } }
		public bool ExportTextures { get { return cbExportTextures.IsChecked == true; } }
		public bool UseGZDoomScale { get { return cbExportForGZDoom.IsChecked == true; } }
		public float ObjScale { get { return (float)(nudScale.Value ?? 1m); } }
		public string BasePath { get { return (tbBasePath.Text ?? "").Trim(); } }
		public string ActorPath { get { return (tbActorPath.Text ?? "").Trim(); } }
		public string ModelPath { get { return (tbModelPath.Text ?? "").Trim(); } }
		public string ActorName { get { return (tbActorName.Text ?? "").Trim(); } }
		public List<string> SkipTextures { get { return new List<string>(skipped); } }
		public bool IgnoreControlSectors { get { return cbIgnoreControlSectors.IsChecked == true; } }
		public bool NormalizeLowestVertex { get { return cbNormalizeLowestVertex.IsChecked == true; } }
		public bool CenterModel { get { return cbCenterModel.IsChecked == true; } }
		public bool NoGravity { get { return cbNoGravity.IsChecked == true; } }
		public bool SpawnOnCeiling { get { return cbSpawnOnCeiling.IsChecked == true; } }
		public bool Solid { get { return cbSolid.IsChecked == true; } }
		public bool ZScript { get { return rbZScript.IsChecked == true; } }
		public bool GenerateCode { get { return cbGenerateCode.IsChecked == true; } }
		public bool GenerateModeldef { get { return cbGenerateModeldef.IsChecked == true; } }
		public string Sprite { get { return (tbSprite.Text ?? "").Trim().ToUpperInvariant(); } }

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal TextBox ExportPathBox { get { return tbExportPath; } }
		internal NumericUpDown ScaleBox { get { return nudScale; } }
		internal AvCheckBox GZDoomBox { get { return cbExportForGZDoom; } }
		internal TextBox ActorNameBox { get { return tbActorName; } }

		public WavefrontSettingsForm(int sectorsCount)
		{
			this.sectorsCount = sectorsCount;

			string name = Path.GetFileNameWithoutExtension(General.Map.FileTitle) + "_" + General.Map.Options.LevelName + ".obj";
			string folder = string.IsNullOrEmpty(General.Map.FilePathName) ? Path.GetTempPath() : Path.GetDirectoryName(General.Map.FilePathName);
			tbExportPath.Text = Path.Combine(folder, name);

			cbExportTextures.IsChecked = General.Settings.ReadPluginSetting("objexporttextures", false);
			cbExportForGZDoom.IsChecked = General.Settings.ReadPluginSetting("objgzdoomscale", false);
			nudScale.Value = (decimal)General.Settings.ReadPluginSetting("objscale", 1.0f);

			string mapname = Path.GetFileNameWithoutExtension(General.Map.FileTitle);
			tbActorName.Text = char.ToUpper(mapname[0]) + mapname.Substring(1);
			string initialfolder = string.IsNullOrEmpty(General.Map.FilePathName) ? Path.GetTempPath() : Path.GetDirectoryName(General.Map.FilePathName);
			tbBasePath.Text = General.Settings.ReadPluginSetting("objbasepath", initialfolder);
			tbActorPath.Text = General.Settings.ReadPluginSetting("objactorpath", initialfolder);
			tbModelPath.Text = General.Settings.ReadPluginSetting("objmodelpath", initialfolder);
			tbSprite.Text = General.Settings.ReadPluginSetting("objsprite", "PLAY");
			cbGenerateCode.IsChecked = General.Settings.ReadPluginSetting("objgeneratecode", true);
			cbGenerateModeldef.IsChecked = General.Settings.ReadPluginSetting("objgeneratemodeldef", true);

			System.Collections.IDictionary skiptexture = General.Settings.ReadPluginSetting("objskiptextures", new System.Collections.Hashtable());
			foreach(System.Collections.DictionaryEntry de in skiptexture) skipped.Add((string)de.Value);
			RefreshSkipped();
		}

		private void RefreshSkipped() { lbSkipTextures.ItemsSource = null; lbSkipTextures.ItemsSource = skipped.ToList(); }

		private void UpdateEnabled()
		{
			bool gz = cbExportForGZDoom.IsChecked == true;
			bool code = gz && cbGenerateCode.IsChecked == true;
			foreach(AvControl c in gzcontrols) c.IsEnabled = gz;
			foreach(AvControl c in codecontrols) c.IsEnabled = code;
			tbModelPath.IsEnabled = gz;
			tbExportPath.IsEnabled = browse.IsEnabled = cbExportTextures.IsEnabled = nudScale.IsEnabled = !gz;
			cbNoGravity.IsEnabled = !(cbSpawnOnCeiling.IsChecked == true);
		}

		private bool PathIsValid(string path)
		{
			if(string.IsNullOrEmpty(path)) return false;
			if(!path.EndsWith(Path.DirectorySeparatorChar.ToString())) path += Path.DirectorySeparatorChar;
			return Directory.Exists(Path.GetDirectoryName(path));
		}

		private AvControl PathRow(string label, TextBox box, string pickertitle, Action<string> picked = null)
		{
			var pick = new AvButton { Content = "..." };
			pick.Click += (s, e) =>
			{
				string folder = ExportUi.PickFolder(pickertitle, box.Text);
				if(folder == null) return;
				box.Text = folder;
				if(picked != null) picked(folder);
			};
			gzcontrols.Add(pick);
			return ExportUi.Labeled(label, box, pick);
		}

		private SimpleDialog Build()
		{
			browse.Click += (s, e) =>
			{
				string file = ExportUi.PickSaveFile("Export to Wavefront .obj", "Wavefront OBJ (*.obj)|*.obj", tbExportPath.Text);
				if(file != null) tbExportPath.Text = file;
			};

			var addtexture = new AvButton { Content = General.Map.Config.MixTexturesFlats ? "Add texture/flat" : "Add texture" };
			addtexture.Click += (s, e) => AddSkipped(General.Interface.BrowseTexture(General.Interface, "-"));
			var addflat = new AvButton { Content = "Add flat", IsVisible = !General.Map.Config.MixTexturesFlats };
			addflat.Click += (s, e) => AddSkipped(General.Interface.BrowseFlat(General.Interface, "-"));
			var remove = new AvButton { Content = "Remove selected" };
			remove.Click += (s, e) =>
			{
				var selected = lbSkipTextures.Selection.SelectedItems.Cast<string>().ToList();
				skipped.RemoveAll(selected.Contains);
				RefreshSkipped();
			};
			var skipbuttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			skipbuttons.Children.Add(addtexture);
			skipbuttons.Children.Add(addflat);
			skipbuttons.Children.Add(remove);

			var reset = new AvButton { Content = "Reset paths" };
			reset.Click += (s, e) => tbBasePath.Text = tbActorPath.Text = tbModelPath.Text = Path.GetDirectoryName(General.Map.FilePathName);
			var formats = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
			formats.Children.Add(new TextBlock { Text = "Actor format:", VerticalAlignment = VerticalAlignment.Center });
			formats.Children.Add(rbZScript);
			formats.Children.Add(rbDecorate);
			var actorflags = new WrapPanel { Orientation = Orientation.Horizontal };
			foreach(AvCheckBox c in new[] { cbSolid, cbSpawnOnCeiling, cbNoGravity }) { c.Margin = new Thickness(0, 0, 12, 0); actorflags.Children.Add(c); }
			var sprite = ExportUi.Labeled("Sprite:", tbSprite);

			codecontrols.AddRange(new AvControl[] { tbActorPath, rbZScript, rbDecorate, cbSolid, cbSpawnOnCeiling, cbNoGravity, tbSprite });
			gzcontrols.AddRange(new AvControl[] { tbBasePath, tbModelPath, tbActorName, cbGenerateCode, cbGenerateModeldef, reset });

			cbExportForGZDoom.IsCheckedChanged += (s, e) => UpdateEnabled();
			cbGenerateCode.IsCheckedChanged += (s, e) => UpdateEnabled();
			cbSpawnOnCeiling.IsCheckedChanged += (s, e) => { if(cbSpawnOnCeiling.IsChecked == true) cbNoGravity.IsChecked = true; UpdateEnabled(); };
			tbActorName.PropertyChanged += (s, e) =>
			{
				if(e.Property != TextBox.TextProperty) return;
				string error = ActorNameError(ActorName);
				ToolTip.SetTip(tbActorName, error);
				tbActorName.Background = error == null ? null : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 192, 192));
			};

			var panel = new StackPanel { Width = 520 };
			ExportUi.Add(panel,
				ExportUi.Labeled("Path:", tbExportPath, browse),
				ExportUi.Labeled("Scale:", nudScale),
				cbExportTextures,
				cbIgnoreControlSectors, cbNormalizeLowestVertex, cbCenterModel,
				new TextBlock { Text = "Skip textures:", Margin = new Thickness(0, 6, 0, 0) }, lbSkipTextures, skipbuttons,
				new Separator(),
				cbExportForGZDoom,
				PathRow("Base path:", tbBasePath, "Select base folder", f => { if(string.IsNullOrWhiteSpace(tbActorPath.Text)) tbActorPath.Text = f; if(string.IsNullOrWhiteSpace(tbModelPath.Text)) tbModelPath.Text = f; }),
				PathRow("Actor path:", tbActorPath, "Select actor folder"),
				PathRow("Model path:", tbModelPath, "Select model folder"),
				ExportUi.Labeled("Actor name:", tbActorName),
				reset,
				cbGenerateCode, cbGenerateModeldef, formats, actorflags, sprite);

			UpdateEnabled();

			dialog = new SimpleDialog("Export " + (sectorsCount == -1 ? "whole map" : sectorsCount + (sectorsCount > 1 ? " sectors" : " sector")) + " to Wavefront .obj", new ScrollViewer { Content = panel, MaxHeight = 640 });
			dialog.OkButton.Content = "Export";
			dialog.Validate = Validate;
			return dialog;
		}

		private void AddSkipped(string name)
		{
			if(string.IsNullOrEmpty(name) || skipped.Contains(name)) return;
			skipped.Add(name);
			RefreshSkipped();
		}

		internal static string ActorNameError(string name)
		{
			if(name.Length == 0) return "Actor name can not be empty";
			string error = null;
			if(name.Any(char.IsWhiteSpace)) error = "Actor name can not contain whitespace";
			if(char.IsDigit(name[0])) error = (error == null ? "" : error + "\n") + "Actor name can not start with a digit";
			return error;
		}

		private bool Validate()
		{
			if(UseGZDoomScale)
			{
				string error = ActorNameError(ActorName);
				if(error != null) { ExportUi.Error(error.Replace("\n", ". ") + "!", "Error"); return false; }
				if(!PathIsValid(BasePath)) { ExportUi.Error("Base path does not exist!", "Error"); return false; }
				if(GenerateCode && !PathIsValid(ActorPath)) { ExportUi.Error("Actor path does not exist!", "Error"); return false; }
				if(GenerateModeldef && !PathIsValid(ModelPath)) { ExportUi.Error("Model path does not exist!", "Error"); return false; }
				if(Sprite.Length != 4) { ExportUi.Error("Sprite name must be exactly 4 alphanumeric characters long!", "Error"); return false; }
			}
			else
			{
				if(ObjScale == 0) { ExportUi.Error("Scale should not be zero!", "Error"); return false; }
				if(!Directory.Exists(Path.GetDirectoryName(FilePath))) { ExportUi.Error("Selected path does not exist!", "Error"); return false; }
			}

			General.Settings.WritePluginSetting("objexporttextures", ExportTextures);
			General.Settings.WritePluginSetting("objgzdoomscale", UseGZDoomScale);
			General.Settings.WritePluginSetting("objscale", ObjScale);
			General.Settings.WritePluginSetting("objbasepath", tbBasePath.Text);
			General.Settings.WritePluginSetting("objactorpath", tbActorPath.Text);
			General.Settings.WritePluginSetting("objmodelpath", tbModelPath.Text);
			General.Settings.WritePluginSetting("objsprite", Sprite);
			General.Settings.WritePluginSetting("objgeneratecode", GenerateCode);
			General.Settings.WritePluginSetting("objgeneratemodeldef", GenerateModeldef);
			var skiptexture = new Dictionary<string, string>();
			for(int i = 0; i < skipped.Count; i++) skiptexture["texture" + i] = skipped[i];
			General.Settings.WritePluginSetting("objskiptextures", skiptexture);
			return true;
		}

		public DialogResult ShowDialog()
		{
			return DialogHost.ShowModal(Build()) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}

	/// <summary>Export to idStudio (UDB's idStudioExporterForm).</summary>
	public class idStudioExporterForm : IDisposable, IWin32Window
	{
		private readonly TextBox gui_ModPath = new TextBox();
		private readonly TextBox gui_MapName = new TextBox();
		private readonly NumericUpDown gui_Downscale = ExportUi.Number(1, 1000, 20);
		private readonly NumericUpDown gui_xShift = ExportUi.Number(-1000000, 1000000, 0);
		private readonly NumericUpDown gui_yShift = ExportUi.Number(-1000000, 1000000, 0);
		private readonly NumericUpDown gui_zShift = ExportUi.Number(-1000000, 1000000, 0);
		private readonly AvCheckBox gui_ExportTextures = new AvCheckBox { Content = "Export Textures" };
		private readonly RadioButton gui_ExpMapTextures = new RadioButton { Content = "Map Textures Only", IsChecked = true, GroupName = "idtextures" };
		private readonly RadioButton gui_ExpAllTextures = new RadioButton { Content = "All Textures", GroupName = "idtextures" };
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		public string ModPath { get { return gui_ModPath.Text ?? ""; } }
		public string MapName { get { return gui_MapName.Text ?? ""; } }
		public float Downscale { get { return (float)(gui_Downscale.Value ?? 1m); } }
		public float xShift { get { return (float)(gui_xShift.Value ?? 0m); } }
		public float yShift { get { return (float)(gui_yShift.Value ?? 0m); } }
		public float zShift { get { return (float)(gui_zShift.Value ?? 0m); } }
		public bool ExportTextures { get { return gui_ExportTextures.IsChecked == true; } }
		public bool ExportAllTextures { get { return gui_ExpAllTextures.IsChecked == true; } }

		public HashSet<string> MapTextures = new HashSet<string>();
		public HashSet<string> MapFlats = new HashSet<string>();

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal TextBox MapNameBox { get { return gui_MapName; } }

		public idStudioExporterForm()
		{
			gui_ModPath.Text = Path.GetDirectoryName(General.Map.FilePathName);
			gui_MapName.Text = General.Map.Options.LevelName.ToLower();

			foreach(Linedef line in General.Map.Map.Linedefs)
			{
				if(line.Front == null) continue;
				MapTextures.Add(line.Front.LowTexture);
				MapTextures.Add(line.Front.MiddleTexture);
				MapTextures.Add(line.Front.HighTexture);
				if(line.Back == null) continue;
				MapTextures.Add(line.Back.LowTexture);
				MapTextures.Add(line.Back.MiddleTexture);
				MapTextures.Add(line.Back.HighTexture);
			}
			foreach(Sector sector in General.Map.Map.Sectors)
			{
				MapFlats.Add(sector.FloorTexture);
				MapFlats.Add(sector.CeilTexture);
			}
			MapFlats.Remove("-");
			MapFlats.Remove("");        // the empty string is a texture when exporting Hordemax maps
			MapTextures.Remove("-");
			MapTextures.Remove("");
		}

		internal static bool IsValidMapName(string name)
		{
			if(name.Length == 0 || name[0] < 'a' || name[0] > 'z') return false;
			foreach(char c in name)
				if(!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
			return true;
		}

		private SimpleDialog Build()
		{
			var pick = new AvButton { Content = "..." };
			pick.Click += (s, e) =>
			{
				string folder = ExportUi.PickFolder("Select Mod Folder", gui_ModPath.Text);
				if(folder != null) gui_ModPath.Text = folder;
			};

			int imageCount = General.Map.Data.Textures.Count + General.Map.Data.Flats.Count;
			var textures = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
			ExportUi.Add(textures,
				new TextBlock { Text = "Exporting textures may take some time.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
				gui_ExpMapTextures,
				new TextBlock { Text = string.Format("{0} TGA images and {0} material2 decls will be created.", MapTextures.Count + MapFlats.Count), Margin = new Thickness(24, 0, 0, 0) },
				gui_ExpAllTextures,
				new TextBlock { Text = string.Format("{0} TGA images and {0} material2 decls will be created.", imageCount), Margin = new Thickness(24, 0, 0, 0) });
			gui_ExportTextures.IsCheckedChanged += (s, e) => textures.IsEnabled = gui_ExportTextures.IsChecked == true;
			textures.IsEnabled = false;

			var panel = new StackPanel { Width = 440 };
			ExportUi.Add(panel,
				new TextBlock { Text = "This tool is still in development.\nNot all map features may convert correctly.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
				ExportUi.Labeled("Mod Folder:", gui_ModPath, pick),
				ExportUi.Labeled("File Name:", gui_MapName),
				new TextBlock { Text = "Transformations:", Margin = new Thickness(0, 6, 0, 0) },
				ExportUi.Labeled("Downscale:", gui_Downscale),
				ExportUi.Labeled("X Shift:", gui_xShift),
				ExportUi.Labeled("Y Shift:", gui_yShift),
				ExportUi.Labeled("Z Shift:", gui_zShift),
				gui_ExportTextures, textures);

			dialog = new SimpleDialog("Export to idStudio", panel);
			dialog.OkButton.Content = "Export";
			dialog.Validate = () =>
			{
				if(IsValidMapName(MapName)) return true;
				MessageBox.Show("Map names must be all lowercase, numbers and underscores only. First char must be letter.", "Invalid Map Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			};
			return dialog;
		}

		public DialogResult ShowDialog()
		{
			return DialogHost.ShowModal(Build()) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}

	internal enum ImageExportResult { OK, Canceled, OutOfMemory, ImageTooBig }

	/// <summary>Export to image (UDB's ImageExportSettingsForm): the settings, then the export runs on its own thread with progress.</summary>
	public class ImageExportSettingsForm : IWin32Window
	{
		private readonly TextBox tbExportPath = new TextBox();
		private readonly ComboBox cbImageFormat = new ComboBox { ItemsSource = new[] { "PNG", "JPG" }, SelectedIndex = 0, MinWidth = 100 };
		private readonly ComboBox cbPixelFormat = new ComboBox { ItemsSource = new[] { "32 bit", "24 bit", "16 bit" }, SelectedIndex = 0, MinWidth = 100 };
		private readonly ComboBox cbScale = new ComboBox { ItemsSource = new[] { "100%", "200%", "400%", "800%" }, SelectedIndex = 0, MinWidth = 100 };
		private readonly RadioButton rbFloor = new RadioButton { Content = "Floor", IsChecked = true, GroupName = "imagesurface" };
		private readonly RadioButton rbCeiling = new RadioButton { Content = "Ceiling", GroupName = "imagesurface" };
		private readonly AvCheckBox cbFullbright = new AvCheckBox { Content = "Use fullbright", IsChecked = true };
		private readonly AvCheckBox cbApplySectorColors = new AvCheckBox { Content = "Apply sector colors", IsChecked = true };
		private readonly AvCheckBox cbTransparency = new AvCheckBox { Content = "Allow transparency" };
		private readonly AvCheckBox cbBrightmap = new AvCheckBox { Content = "Create brightmap" };
		private readonly AvCheckBox cbTiles = new AvCheckBox { Content = "Create 64x64 tiles" };
		private readonly ProgressBar progress = new ProgressBar { Minimum = 0, Maximum = 100, IsVisible = false };
		private readonly TextBlock lbPhase = new TextBlock { IsVisible = false };
		private readonly AvButton browse = new AvButton { Content = "..." };
		private readonly AvButton export = new AvButton { Content = "Export", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly AvButton close = new AvButton { Content = "Close", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly StackPanel settingspanel = new StackPanel();
		private Window window;
		private Thread exportthread;
		private volatile bool exporting;
		private volatile bool cancelexport;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		public string FilePath { get { return (tbExportPath.Text ?? "").Trim(); } }
		public bool Floor { get { return rbFloor.IsChecked == true; } }
		public bool Fullbright { get { return cbFullbright.IsChecked == true; } }
		public bool Transparency { get { return cbTransparency.IsChecked == true; } }
		public bool ApplySectorColors { get { return cbApplySectorColors.IsChecked == true; } }
		public bool Brightmap { get { return cbBrightmap.IsChecked == true; } }
		public bool Tiles { get { return cbTiles.IsChecked == true; } }
		public float ImageScale { get { return (float)Math.Pow(2, Math.Max(0, cbScale.SelectedIndex)); } }

		// For the tests
		internal Window Window { get { return window; } }
		internal TextBox ExportPathBox { get { return tbExportPath; } }
		internal AvButton ExportButton { get { return export; } }
		internal AvButton CloseButton { get { return close; } }
		internal bool Exporting { get { return exporting; } }
		internal ComboBox ImageFormatBox { get { return cbImageFormat; } }
		/// <summary>The text of the last message shown when an export finished (null until then).</summary>
		internal string LastResult { get; private set; }

		public ImageExportSettingsForm()
		{
			string name = Path.GetFileNameWithoutExtension(General.Map.FileTitle) + "_" + General.Map.Options.LevelName + "_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
			string folder = string.IsNullOrEmpty(General.Map.FilePathName) ? Path.GetTempPath() : Path.GetDirectoryName(General.Map.FilePathName);
			tbExportPath.Text = Path.Combine(folder, name + ".png");

			cbFullbright.IsChecked = General.Settings.ReadPluginSetting("imageexportfullbright", true);
			cbApplySectorColors.IsChecked = General.Settings.ReadPluginSetting("imageexportapplysectorcolors", true);
			cbTransparency.IsChecked = General.Settings.ReadPluginSetting("imageexporttransparency", false);
			cbBrightmap.IsChecked = General.Settings.ReadPluginSetting("imageexportbrightmap", false);
			cbTiles.IsChecked = General.Settings.ReadPluginSetting("imageexporttiles", false);
			cbScale.SelectedIndex = Math.Min(3, Math.Max(0, General.Settings.ReadPluginSetting("imageexportscale", 0)));
		}

		public System.Drawing.Imaging.ImageFormat GetImageFormat()
		{
			return cbImageFormat.SelectedIndex == 1 ? System.Drawing.Imaging.ImageFormat.Jpeg : System.Drawing.Imaging.ImageFormat.Png;
		}

		public System.Drawing.Imaging.PixelFormat GetPixelFormat()
		{
			switch(cbPixelFormat.SelectedIndex)
			{
				case 1: return System.Drawing.Imaging.PixelFormat.Format24bppRgb;
				case 2: return System.Drawing.Imaging.PixelFormat.Format16bppRgb555;
				default: return System.Drawing.Imaging.PixelFormat.Format32bppArgb;
			}
		}

		private void Build()
		{
			browse.Click += (s, e) =>
			{
				string file = ExportUi.PickSaveFile("Export to image", "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg", tbExportPath.Text);
				if(file == null) return;
				tbExportPath.Text = file;
				cbImageFormat.SelectedIndex = Path.GetExtension(file).ToLowerInvariant() == ".jpg" ? 1 : 0;
			};
			cbImageFormat.SelectionChanged += (s, e) => tbExportPath.Text = Path.ChangeExtension(tbExportPath.Text, cbImageFormat.SelectedIndex == 1 ? ".jpg" : ".png");
			export.Click += (s, e) => { if(exporting) cancelexport = true; else StartExport(); };
			close.Click += (s, e) => { if(!exporting) window.Close(false); };

			var surface = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
			surface.Children.Add(rbFloor);
			surface.Children.Add(rbCeiling);
			ExportUi.Add(settingspanel,
				ExportUi.Labeled("Path:", tbExportPath, browse),
				ExportUi.Labeled("Image format:", cbImageFormat),
				ExportUi.Labeled("Color depth:", cbPixelFormat),
				ExportUi.Labeled("Scale:", cbScale),
				surface, cbFullbright, cbApplySectorColors, cbTransparency, cbBrightmap, cbTiles);

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
			buttons.Children.Add(export);
			buttons.Children.Add(close);

			var layout = new StackPanel { Margin = new Thickness(12), Width = 460 };
			layout.Children.Add(settingspanel);
			ExportUi.Add(layout, lbPhase, progress);
			layout.Children.Add(buttons);

			window = new Window { Title = "Image export settings", SizeToContent = SizeToContent.Height, Width = 484, CanResize = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = layout };
			window.Closing += (s, e) => { if(exporting) e.Cancel = true; };      // not while the export is running
		}

		/// <summary>Shows the settings; the export runs from the dialog.</summary>
		public DialogResult ShowDialog()
		{
			Window owner = DialogHost.Owner == null ? null : DialogHost.Owner();
			if(owner == null) return DialogResult.Cancel;
			Build();
			DialogPump.Run(() => window.ShowDialog<bool>(owner));
			return DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }

		// Disables the settings and starts the thread that does the work
		private void StartExport()
		{
			exporting = true;
			cancelexport = false;
			progress.Value = 0;
			progress.IsVisible = true;
			lbPhase.Text = "";
			lbPhase.IsVisible = true;
			settingspanel.IsEnabled = false;
			close.IsEnabled = false;
			export.Content = "Cancel";

			General.Settings.WritePluginSetting("imageexportfullbright", Fullbright);
			General.Settings.WritePluginSetting("imageexportapplysectorcolors", ApplySectorColors);
			General.Settings.WritePluginSetting("imageexporttransparency", Transparency);
			General.Settings.WritePluginSetting("imageexportbrightmap", Brightmap);
			General.Settings.WritePluginSetting("imageexporttiles", Tiles);
			General.Settings.WritePluginSetting("imageexportscale", cbScale.SelectedIndex);

			ICollection<Sector> sectors = General.Map.Map.SelectedSectorsCount == 0 ? General.Map.Map.Sectors : General.Map.Map.GetSelectedSectors(true);
			ImageExportSettings settings = new ImageExportSettings(Path.GetDirectoryName(FilePath), Path.GetFileNameWithoutExtension(FilePath), Path.GetExtension(FilePath), Floor, Fullbright, ApplySectorColors, Brightmap, Transparency, Tiles, ImageScale, GetPixelFormat(), GetImageFormat());
			exportthread = new Thread(() => RunExport(sectors, settings)) { Name = "Image export", Priority = ThreadPriority.Normal };
			exportthread.Start();
		}

		private void RunExport(ICollection<Sector> sectors, ImageExportSettings settings)
		{
			ImageExporter exporter = new ImageExporter(sectors, settings, AddProgress, ShowPhase, () => cancelexport);
			try
			{
				exporter.Export();
			}
			catch(ArgumentException) { StopExport(ImageExportResult.OutOfMemory); return; }       // not enough consecutive memory for the image
			catch(ImageExportCanceledException) { StopExport(ImageExportResult.Canceled); return; }
			catch(ImageExportImageTooBigException) { StopExport(ImageExportResult.ImageTooBig); return; }
			StopExport(ImageExportResult.OK);
		}

		private void AddProgress() { Dispatcher.UIThread.Post(() => progress.Value = Math.Min(progress.Maximum, progress.Value + 1)); }
		private void ShowPhase(string text) { Dispatcher.UIThread.Post(() => lbPhase.Text = text); }

		private void StopExport(ImageExportResult result)
		{
			Dispatcher.UIThread.Post(() =>
			{
				progress.IsVisible = false;
				lbPhase.IsVisible = false;
				settingspanel.IsEnabled = true;
				close.IsEnabled = true;
				export.Content = "Export";
				exporting = false;

				switch(result)
				{
					case ImageExportResult.OK: Report("Export successful.", "Export to image", MessageBoxIcon.Information); break;
					case ImageExportResult.Canceled: Report("Export canceled.", "Export to image", MessageBoxIcon.Information); break;
					case ImageExportResult.OutOfMemory: Report("Exporting failed. There's likely not enough consecutive free memory to create the image. Try a lower color depth or file format", "Export failed", MessageBoxIcon.Error); break;
					case ImageExportResult.ImageTooBig: Report("Exporting failed. The image is likely too big for the current settings. Try a lower color depth or file format", "Export failed", MessageBoxIcon.Error); break;
				}
			});
		}

		private void Report(string text, string caption, MessageBoxIcon icon)
		{
			LastResult = text;
			MessageBox.Show(text, caption, MessageBoxButtons.OK, icon);
		}
	}
}
