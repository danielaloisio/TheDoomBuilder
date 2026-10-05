// The small dialogs of the BuilderModes plugin (paste properties, select similar, change index, filter things, make door).
// Same classes and members as UDB's forms, so the modes call them unchanged; each one builds an Avalonia SimpleDialog and shows it
// over the main window (DialogHost), answering synchronously like WinForms' ShowDialog.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;
using Control = Avalonia.Controls.Control;
using CheckBox = Avalonia.Controls.CheckBox;
using Button = Avalonia.Controls.Button;
using DialogResult = System.Windows.Forms.DialogResult;

namespace CodeImp.DoomBuilder.BuilderModes.Interface
{
	/// <summary>Base of the plugin's dialogs: shown modally, answers OK or Cancel (the Win32 handle only exists to satisfy IWin32Window).</summary>
	internal abstract class PluginDialog : IDisposable, IWin32Window
	{
		public IntPtr Handle { get { return IntPtr.Zero; } }

		/// <summary>The window to show.</summary>
		protected abstract SimpleDialog Window { get; }

		public DialogResult ShowDialog()
		{
			return DialogHost.ShowModal(Window) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public virtual void Dispose() { }
	}

	/// <summary>
	/// A list of check boxes, one per copy setting of an element type that the current map format supports (UDB's
	/// CheckboxArrayControl filled from the FieldDescription attributes). <see cref="Store"/> writes the choices back to the settings object.
	/// </summary>
	internal class CopySettingsChecklist
	{
		private readonly object settings;
		private readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
		private readonly List<CheckBox> boxes = new List<CheckBox>();
		private readonly WrapPanel panel = new WrapPanel { Orientation = Orientation.Vertical, MaxHeight = 360, Margin = new Thickness(6) };

		public CopySettingsChecklist(object settings)
		{
			this.settings = settings;
			foreach(FieldInfo field in settings.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				foreach(Attribute attr in Attribute.GetCustomAttributes(field))
				{
					if(attr.GetType() != typeof(FieldDescription)) continue;
					FieldDescription fd = (FieldDescription)attr;
					if(!fd.SupportsCurrentMapFormat) break;
					var box = new CheckBox { Content = fd.Description, Tag = field.Name, IsChecked = (bool)field.GetValue(settings), MinWidth = 200, Margin = new Thickness(0, 1, 12, 1) };
					fields[field.Name] = field;
					boxes.Add(box);
					panel.Children.Add(box);
					break;
				}
			}
		}

		public int Count { get { return boxes.Count; } }
		public Control View { get { return new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }; } }

		/// <summary>Turns everything on, or everything off when the first one is on already.</summary>
		public void Toggle()
		{
			if(boxes.Count == 0) return;
			bool enable = boxes[0].IsChecked != true;
			foreach(CheckBox box in boxes) box.IsChecked = enable;
		}

		public void Store()
		{
			foreach(CheckBox box in boxes) fields[(string)box.Tag].SetValue(settings, box.IsChecked == true);
		}
	}

	/// <summary>Choose which of the copied properties to paste.</summary>
	internal class PastePropertiesOptionsForm : PluginDialog
	{
		private readonly TabControl tabs = new TabControl();
		private readonly List<CopySettingsChecklist> lists = new List<CopySettingsChecklist>();
		private SimpleDialog window;

		protected override SimpleDialog Window { get { return window; } }

		public bool Setup(MapElementType targetmapelementtype) { return Setup(new List<MapElementType> { targetmapelementtype }); }
		public bool Setup(IEnumerable<MapElementType> targetmapelementtypes)
		{
			var pages = new List<KeyValuePair<string, CopySettingsChecklist>>();
			bool anycopied = false;
			foreach(MapElementType t in targetmapelementtypes)
			{
				switch(t)
				{
					case MapElementType.THING:
						if(BuilderPlug.Me.CopiedThingProps != null) { anycopied = true; pages.Add(Page("Things", ThingProperties.CopySettings)); }
						break;

					case MapElementType.SECTOR:
						if(BuilderPlug.Me.CopiedSectorProps != null) { anycopied = true; pages.Add(Page("Sectors", SectorProperties.CopySettings)); }
						break;

					case MapElementType.LINEDEF:
					case MapElementType.SIDEDEF:
						if(BuilderPlug.Me.CopiedSidedefProps != null || BuilderPlug.Me.CopiedLinedefProps != null)
						{
							anycopied = true;
							pages.Add(Page("Linedefs", LinedefProperties.CopySettings));
							pages.Add(Page("Sidedefs", SidedefProperties.CopySettings));
						}
						break;

					case MapElementType.VERTEX:
						if(BuilderPlug.Me.CopiedVertexProps != null) { anycopied = true; pages.Add(Page("Vertices", VertexProperties.CopySettings)); }
						break;

					default:
						throw new NotImplementedException("Unknown map element type: " + t);
				}
			}

			// Got anything to show?
			if(!anycopied)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "No copied properties to apply!");
				return false;
			}

			foreach(var page in pages.Where(p => p.Value.Count > 0))
			{
				lists.Add(page.Value);
				tabs.Items.Add(new TabItem { Header = page.Key, Content = page.Value.View });
			}

			if(lists.Count == 0)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "Current map format doesn't support any properties for selected map elements!");
				return false;
			}

			tabs.SelectedIndex = 0;
			window = new SimpleDialog("Paste Properties Special", tabs, 560);
			window.Validate = () => { foreach(CopySettingsChecklist list in lists) list.Store(); return true; };
			var toggle = new Button { Content = "Toggle All" };
			toggle.Click += (s, e) => { if(tabs.SelectedIndex >= 0) lists[tabs.SelectedIndex].Toggle(); };
			window.ExtraButtons.Children.Add(toggle);
			return true;
		}

		private static KeyValuePair<string, CopySettingsChecklist> Page(string title, object settings)
		{
			return new KeyValuePair<string, CopySettingsChecklist>(title, new CopySettingsChecklist(settings));
		}
	}

	/// <summary>Choose which properties must match to select the elements similar to the selected ones, then select them.</summary>
	internal class SelectSimilarElementOptionsPanel : PluginDialog
	{
		private static readonly object[] flags = {
			new SectorPropertiesCopySettings(), new LinedefPropertiesCopySettings(), new SidedefPropertiesCopySettings(),
			new ThingPropertiesCopySettings(), new VertexPropertiesCopySettings()
		};

		private BaseClassicMode mode;
		private readonly TabControl tabs = new TabControl();
		private readonly List<CopySettingsChecklist> lists = new List<CopySettingsChecklist>();
		private SimpleDialog window;

		protected override SimpleDialog Window { get { return window; } }

		public bool Setup(BaseClassicMode mode)
		{
			this.mode = mode;

			// Which pages should we display?
			var active = new List<KeyValuePair<string, object>>();
			if(General.Editing.Mode is ThingsMode)
			{
				if(General.Map.Map.GetSelectedThings(true).Count == 0) return SetupFailed("This action requires selection...");
				active.Add(new KeyValuePair<string, object>("Things", flags[3]));
			}
			else if(General.Editing.Mode is VerticesMode && General.Map.UDMF)
			{
				if(General.Map.Map.GetSelectedVertices(true).Count == 0) return SetupFailed("This action requires selection...");
				active.Add(new KeyValuePair<string, object>("Vertices", flags[4]));
			}
			else if(General.Editing.Mode is LinedefsMode)
			{
				if(General.Map.Map.GetSelectedLinedefs(true).Count == 0) return SetupFailed("This action requires selection...");
				active.Add(new KeyValuePair<string, object>("Linedefs", flags[1]));
				active.Add(new KeyValuePair<string, object>("Sidedefs", flags[2]));
			}
			else if(mode is SectorsMode)
			{
				if(General.Map.Map.GetSelectedSectors(true).Count == 0) return SetupFailed("This action requires selection...");
				active.Add(new KeyValuePair<string, object>("Sectors", flags[0]));
			}

			if(active.Count == 0) return SetupFailed("This action doesn't support current editing mode...");

			foreach(var page in active)
			{
				var list = new CopySettingsChecklist(page.Value);
				if(list.Count == 0) continue;
				lists.Add(list);
				tabs.Items.Add(new TabItem { Header = page.Key, Content = list.View });
			}

			// Got anything to show?
			if(lists.Count == 0) return SetupFailed("This action doesn't support current editing mode...");

			tabs.SelectedIndex = 0;
			window = new SimpleDialog("Selection Options", tabs, 560);
			window.Validate = () => { Apply(); return true; };
			var toggle = new Button { Content = "Toggle All" };
			toggle.Click += (s, e) => { if(tabs.SelectedIndex >= 0) lists[tabs.SelectedIndex].Toggle(); };
			window.ExtraButtons.Children.Add(toggle);
			return true;
		}

		private static bool SetupFailed(string message)
		{
			General.Interface.DisplayStatus(StatusType.Warning, message);
			return false;
		}

		// OK: remember the choices and select what matches
		private void Apply()
		{
			foreach(CopySettingsChecklist list in lists) list.Store();

			if(mode is ThingsMode)
			{
				ICollection<Thing> selected = General.Map.Map.GetSelectedThings(true);
				foreach(Thing target in General.Map.Map.GetSelectedThings(false))
					foreach(Thing source in selected)
						if(PropertiesComparer.PropertiesMatch((ThingPropertiesCopySettings)flags[3], source, target)) mode.SelectMapElement(target);
			}
			else if(mode is LinedefsMode)
			{
				ICollection<Linedef> selected = General.Map.Map.GetSelectedLinedefs(true);
				foreach(Linedef target in General.Map.Map.GetSelectedLinedefs(false))
					foreach(Linedef source in selected)
						if(PropertiesComparer.PropertiesMatch((LinedefPropertiesCopySettings)flags[1], (SidedefPropertiesCopySettings)flags[2], source, target)) mode.SelectMapElement(target);
			}
			else if(mode is SectorsMode)
			{
				ICollection<Sector> selected = General.Map.Map.GetSelectedSectors(true);
				foreach(Sector target in General.Map.Map.GetSelectedSectors(false))
					foreach(Sector source in selected)
						if(PropertiesComparer.PropertiesMatch((SectorPropertiesCopySettings)flags[0], source, target)) mode.SelectMapElement(target);
			}
			else if(mode is VerticesMode)
			{
				ICollection<Vertex> selected = General.Map.Map.GetSelectedVertices(true);
				foreach(Vertex target in General.Map.Map.GetSelectedVertices(false))
					foreach(Vertex source in selected)
						if(PropertiesComparer.PropertiesMatch((VertexPropertiesCopySettings)flags[4], source, target)) mode.SelectMapElement(target);
			}

			mode.UpdateSelectionInfo();
			General.Interface.RedrawDisplay();
		}
	}

	/// <summary>Asks for the new index of a map element.</summary>
	internal class ChangeMapElementIndexForm : PluginDialog
	{
		private readonly int currentindex;
		private readonly int maxindex;
		private readonly NumberBox newindex = new NumberBox { AllowDecimal = false, AllowNegative = false, AllowRelative = false, Text = "0" };
		private readonly TextBlock warning = new TextBlock { Foreground = Brushes.OrangeRed, IsVisible = false };
		private readonly SimpleDialog window;

		protected override SimpleDialog Window { get { return window; } }

		public ChangeMapElementIndexForm(string typetitle, int currentindex, int maxindex)
		{
			this.currentindex = currentindex;
			this.maxindex = maxindex;

			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 12, RowSpacing = 6 };
			Add(grid, new TextBlock { Text = "Current index:" }, 0, 0);
			Add(grid, new TextBlock { Text = currentindex.ToString() }, 1, 0);
			Add(grid, new TextBlock { Text = "Maximum index:" }, 0, 1);
			Add(grid, new TextBlock { Text = maxindex.ToString() }, 1, 1);
			Add(grid, new TextBlock { Text = "New index:", VerticalAlignment = VerticalAlignment.Center }, 0, 2);
			Add(grid, newindex, 1, 2);
			Add(grid, warning, 0, 3);
			Grid.SetColumnSpan(warning, 2);

			window = new SimpleDialog("Change " + typetitle + " index", grid);
			newindex.WhenTextChanged += (s, e) => Check();
			Check();
		}

		private static void Add(Grid grid, Control c, int column, int row)
		{
			Grid.SetColumn(c, column);
			Grid.SetRow(c, row);
			grid.Children.Add(c);
		}

		public int GetNewIndex() { return newindex.GetResult(0); }

		private void Check()
		{
			int target = GetNewIndex();
			if(target > maxindex) Show("The new index is too high");
			else if(target == currentindex) Show("The new and old indices are the same");
			else { window.OkButton.IsEnabled = true; warning.IsVisible = false; }
		}

		private void Show(string message)
		{
			warning.Text = message;
			warning.IsVisible = true;
			window.OkButton.IsEnabled = false;
		}
	}

	/// <summary>Choose which of the selected thing types stay selected.</summary>
	internal class FilterSelectedThingsForm : PluginDialog
	{
		private readonly ICollection<Thing> selection;
		private readonly ThingsMode mode;
		private readonly ListBox list = new ListBox { SelectionMode = SelectionMode.Multiple, MinHeight = 160, MaxHeight = 320 };
		private readonly SimpleDialog window;

		protected override SimpleDialog Window { get { return window; } }

		/// <summary>The thing types in the selection with their titles and counts, by type.</summary>
		internal static List<Tuple<int, string, int>> CountTypes(ICollection<Thing> selection)
		{
			var counts = new Dictionary<int, int>();
			var titles = new Dictionary<int, string>();
			foreach(Thing t in selection)
			{
				if(!counts.ContainsKey(t.Type))
				{
					counts.Add(t.Type, 1);
					titles.Add(t.Type, General.Map.Data.GetThingInfo(t.Type).Title);
				}
				else counts[t.Type]++;
			}
			return counts.OrderBy(c => c.Key).Select(c => Tuple.Create(c.Key, titles[c.Key], c.Value)).ToList();
		}

		public FilterSelectedThingsForm(ICollection<Thing> selection, ThingsMode mode)
		{
			this.selection = selection;
			this.mode = mode;

			var items = new List<ListBoxItem>();
			foreach(var row in CountTypes(selection))
			{
				var line = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*,60") };
				line.Children.Add(new TextBlock { Text = row.Item1.ToString() });
				var title = new TextBlock { Text = row.Item2 };
				Grid.SetColumn(title, 1);
				line.Children.Add(title);
				var count = new TextBlock { Text = row.Item3.ToString(), HorizontalAlignment = HorizontalAlignment.Right };
				Grid.SetColumn(count, 2);
				line.Children.Add(count);
				items.Add(new ListBoxItem { Content = line, Tag = row.Item1 });
			}
			list.ItemsSource = items;

			var header = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*,60"), Margin = new Thickness(8, 0) };
			header.Children.Add(new TextBlock { Text = "Type", FontWeight = FontWeight.SemiBold });
			var htitle = new TextBlock { Text = "Title", FontWeight = FontWeight.SemiBold };
			Grid.SetColumn(htitle, 1);
			header.Children.Add(htitle);
			var hcount = new TextBlock { Text = "Count", FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
			Grid.SetColumn(hcount, 2);
			header.Children.Add(hcount);

			var stack = new StackPanel { Spacing = 6 };
			stack.Children.Add(new TextBlock { Text = "Select thing types you want to keep selected" });
			stack.Children.Add(header);
			stack.Children.Add(list);

			window = new SimpleDialog("Filter Selected Things", stack, 420);
			window.Validate = () => { Apply(); return true; };
		}

		// Things whose type is not chosen are deselected (choosing nothing changes nothing)
		private void Apply()
		{
			var types = new List<int>();
			foreach(object item in list.SelectedItems) types.Add((int)((ListBoxItem)item).Tag);
			if(types.Count == 0) return;

			foreach(Thing t in selection)
				if(!types.Contains(t.Type)) t.Selected = false;

			mode.UpdateSelectionInfo();
			General.Interface.RedrawDisplay();
		}
	}

	/// <summary>The textures and options for turning the selected sectors into a door.</summary>
	internal class MakeDoorForm : PluginDialog
	{
		private readonly TextureSelector doortexture = new TextureSelector { Width = 80, Height = 100 };
		private readonly TextureSelector tracktexture = new TextureSelector { Width = 80, Height = 100 };
		private readonly FlatSelector ceilingtexture = new FlatSelector { Width = 80, Height = 100 };
		private readonly FlatSelector floortexture = new FlatSelector { Width = 80, Height = 100 };
		private readonly CheckBox resetoffsets = new CheckBox { Content = "Reset texture offsets" };
		private readonly CheckBox applyactionspecials = new CheckBox { Content = "Apply action specials" };
		private readonly CheckBox applytag = new CheckBox { Content = "Apply tag" };
		private readonly SimpleDialog window;

		protected override SimpleDialog Window { get { return window; } }

		public string DoorTexture { get { return doortexture.TextureName; } }
		public string TrackTexture { get { return tracktexture.TextureName; } }
		public string CeilingTexture { get { return ceilingtexture.TextureName; } }
		public string FloorTexture { get { return floortexture.TextureName; } }
		public bool ResetOffsets { get { return resetoffsets.IsChecked == true; } }
		public bool ApplyActionSpecials { get { return applyactionspecials.IsChecked == true; } }
		public bool ApplyTag { get { return applytag.IsChecked == true; } }

		public MakeDoorForm()
		{
			var selectors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
			selectors.Children.Add(Labelled("Door", doortexture));
			selectors.Children.Add(Labelled("Track", tracktexture));
			selectors.Children.Add(Labelled("Ceiling", ceilingtexture));
			selectors.Children.Add(Labelled("Floor", floortexture));

			var stack = new StackPanel { Spacing = 8 };
			stack.Children.Add(selectors);
			stack.Children.Add(resetoffsets);
			stack.Children.Add(applyactionspecials);
			stack.Children.Add(applytag);

			window = new SimpleDialog("Make Door", stack);
			window.Opened += (s, e) => { doortexture.Initialize(); tracktexture.Initialize(); ceilingtexture.Initialize(); floortexture.Initialize(); };
			window.Closed += (s, e) => { doortexture.StopUpdate(); tracktexture.StopUpdate(); ceilingtexture.StopUpdate(); floortexture.StopUpdate(); };
			window.Validate = () =>
			{
				if(doortexture.TextureName.Length > 0) return true;
				General.Interface.DisplayStatus(StatusType.Warning, "Please select a door texture!");
				General.ShowErrorMessage("Please select a door texture!", MessageBoxButtons.OK);
				return false;
			};
		}

		private static Control Labelled(string label, Control selector)
		{
			var stack = new StackPanel { Spacing = 2 };
			stack.Children.Add(new TextBlock { Text = label });
			stack.Children.Add(selector);
			return stack;
		}

		// Sets the values and shows the dialog (as UDB's form does)
		public DialogResult Show(IWin32Window owner, string doortex, string tracktex, string ceilingtex, string floortex, bool roffsets, bool applyactionspecials, bool applytag)
		{
			doortexture.TextureName = doortex;
			tracktexture.TextureName = tracktex;
			ceilingtexture.TextureName = ceilingtex;
			floortexture.TextureName = floortex;
			resetoffsets.IsChecked = roffsets;
			this.applyactionspecials.IsChecked = applyactionspecials;
			this.applytag.IsChecked = applytag;
			return ShowDialog(owner);
		}
	}
}
