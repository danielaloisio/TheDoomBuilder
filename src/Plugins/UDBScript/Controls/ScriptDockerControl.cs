// The "Scripts" docker: the scripts of the UDBScript folder as a tree (with a filter), the description and the options of the selected one,
// and the buttons that run it or put its options back to the defaults. UDB's ScriptDockerControl; the logic is the original's, the controls
// are Avalonia's (the panel travels inside the Docker as the native control of a shim Control).
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.UDBScript
{
	/// <summary>A node of the tree of scripts: a folder (a ScriptDirectoryStructure) or a script (a ScriptInfo).</summary>
	internal sealed class ScriptNode : INotifyPropertyChanged
	{
		private bool expanded;

		public string Text { get; set; }
		public object Tag { get; set; }
		public bool IsFolder { get { return Tag is ScriptDirectoryStructure; } }
		public List<ScriptNode> Nodes { get; } = new List<ScriptNode>();
		public Action<ScriptNode> ExpandedChanged;

		public bool IsExpanded
		{
			get { return expanded; }
			set
			{
				if(expanded == value) return;
				expanded = value;
				if(PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
				if(ExpandedChanged != null) ExpandedChanged(this);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		public override string ToString() { return Text; }
	}

	public class ScriptDockerControl : System.Windows.Forms.Control
	{
		#region ================== Variables

		private readonly Grid root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
		private readonly TreeView filetree = new TreeView();
		private readonly TextBox tbFilter = new TextBox { Watermark = "Filter" };
		private readonly AvButton btnClearFilter = new AvButton { VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(3) };
		private readonly TextBox tbDescription = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 50 };
		private readonly ScriptOptionsControl scriptoptions = new ScriptOptionsControl();
		private readonly AvButton btnRunScript = new AvButton { Content = "Run", MinWidth = 70, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly AvButton btnResetToDefaults = new AvButton { Content = "Reset", MinWidth = 70, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> images = new Dictionary<string, Avalonia.Media.Imaging.Bitmap>();
		private List<ScriptNode> roots = new List<ScriptNode>();
		private bool updating;
		private bool disposed;

		// For the tests
		internal TreeView Tree { get { return filetree; } }
		internal IReadOnlyList<ScriptNode> Roots { get { return roots; } }
		internal TextBox Filter { get { return tbFilter; } }
		internal AvButton ClearFilterButton { get { return btnClearFilter; } }
		internal TextBox Description { get { return tbDescription; } }
		internal ScriptOptionsControl Options { get { return scriptoptions; } }
		internal AvButton RunButton { get { return btnRunScript; } }
		internal AvButton ResetButton { get { return btnResetToDefaults; } }

		#endregion

		#region ================== Constructor

		public ScriptDockerControl(string foldername)
		{
			if(Properties.Resources.Files.ContainsKey("SearchClear"))
				btnClearFilter.Content = new Avalonia.Controls.Image { Source = ImageConvert.ToAvalonia(Properties.Resources.SearchClear), Width = 16, Height = 16 };

			var filter = new DockPanel { Margin = new Thickness(4) };
			DockPanel.SetDock(btnClearFilter, Dock.Right);
			btnClearFilter.Margin = new Thickness(4, 0, 0, 0);
			filter.Children.Add(btnClearFilter);
			filter.Children.Add(tbFilter);

			filetree.ItemTemplate = new FuncTreeDataTemplate<ScriptNode>((node, scope) => CreateRow(node), node => node.Nodes);
			filetree.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
			{
				Setters = { new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(ScriptNode.IsExpanded)) { Mode = BindingMode.TwoWay }) }
			});
			filetree.SelectionChanged += filetree_AfterSelect;

			var description = Titled("Script description", tbDescription);
			var options = Titled("Script options", scriptoptions);
			var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4) };
			buttons.Children.Add(btnResetToDefaults);
			buttons.Children.Add(btnRunScript);

			Grid.SetRow(filter, 0);
			Grid.SetRow(filetree, 1);
			Grid.SetRow(description, 2);
			Grid.SetRow(options, 3);
			Grid.SetRow(buttons, 4);
			root.Children.Add(filter);
			root.Children.Add(filetree);
			root.Children.Add(description);
			root.Children.Add(options);
			root.Children.Add(buttons);

			btnClearFilter.Click += (s, e) => tbFilter.Text = "";
			tbFilter.PropertyChanged += (s, e) => { if(e.Property == TextBox.TextProperty && !updating) FillTree(); };
			btnRunScript.Click += btnRunScript_Click;
			btnResetToDefaults.Click += btnResetToDefaults_Click;

			// The docker tabs only keep the selected panel in the visual tree: being attached means being shown (UDB's VisibleChanged)
			root.AttachedToVisualTree += (s, e) => { if(!disposed && BuilderPlug.Me != null && BuilderPlug.Me.ScriptDirectoryStructure != null) FillTree(); };
			NativeControl = root;
		}

		private static Control Titled(string title, Control content)
		{
			var panel = new StackPanel { Margin = new Thickness(4, 2) };
			panel.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.Bold });
			panel.Children.Add(content);
			return panel;
		}

		public void Dispose() { disposed = true; }

		#endregion

		#region ================== Methods

		private Avalonia.Media.Imaging.Bitmap Image(string name)
		{
			Avalonia.Media.Imaging.Bitmap bitmap;
			if(!images.TryGetValue(name, out bitmap))
			{
				bitmap = ImageConvert.ToAvalonia(name == "Folder" ? Properties.Resources.Folder : Properties.Resources.Script);
				images[name] = bitmap;
			}
			return bitmap;
		}

		// One line of the tree: the icon and the text, with the menu of a folder or of a script
		private Control CreateRow(ScriptNode node)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Background = Avalonia.Media.Brushes.Transparent, Tag = node };
			row.Children.Add(new Avalonia.Controls.Image { Source = Image(node.IsFolder ? "Folder" : "Script"), Width = 16, Height = 16 });
			row.Children.Add(new TextBlock { Text = node.Text, VerticalAlignment = VerticalAlignment.Center });
			row.ContextMenu = node.IsFolder ? CreateFolderContextMenu(node) : CreateFileContextMenu(node);
			row.PointerPressed += (s, e) =>
			{
				// A right click selects what it is about
				if(e.GetCurrentPoint(row).Properties.IsRightButtonPressed) filetree.SelectedItem = node;
			};
			return row;
		}

		private static string GetHotkeyText(int slot)
		{
			string actionname = "udbscript_udbscriptexecuteslot" + slot;
			string keytext = "no hotkey";

			Actions.Action action = General.Actions.GetActionByName(actionname);
			if(action != null && action.ShortcutKey != 0)
				keytext = Actions.Action.GetShortcutKeyDesc(actionname);

			return keytext;
		}

		private ContextMenu CreateFileContextMenu(ScriptNode node)
		{
			var edit = new MenuItem { Header = "Edit" };
			edit.Click += (s, e) => EditScript(node);

			var setslot = new MenuItem { Header = "Set slot" };
			var items = new List<object>();
			var clear = new MenuItem { Header = "Clear slot" };
			clear.Click += (s, e) => ClearSlot(node);
			items.Add(clear);
			items.Add(new Separator());
			for(int i = 0; i < BuilderPlug.NUM_SCRIPT_SLOTS; i++)
			{
				int slot = i + 1;
				ScriptInfo si = BuilderPlug.Me.GetScriptSlot(slot);
				string text = "Slot " + slot + ": " + (si != null ? si.Name : "not assigned") + " [" + GetHotkeyText(slot) + "]";
				var item = new MenuItem { Header = text, Tag = slot };
				item.Click += (s, e) => SetSlot(node, slot);
				items.Add(item);
			}
			setslot.ItemsSource = items;
			return new ContextMenu { ItemsSource = new object[] { edit, setslot } };
		}

		private ContextMenu CreateFolderContextMenu(ScriptNode node)
		{
			var open = new MenuItem { Header = "Open in file manager" };
			open.Click += (s, e) => OpenFolder(((ScriptDirectoryStructure)node.Tag).Path);
			return new ContextMenu { ItemsSource = new object[] { open } };
		}

		private static void OpenFolder(string path)
		{
			try
			{
				if(OperatingSystem.IsWindows()) Process.Start("explorer.exe", "\"" + path + "\"");
				else if(OperatingSystem.IsMacOS()) Process.Start("open", "\"" + path + "\"");
				else Process.Start("xdg-open", "\"" + path + "\"");
			}
			catch(Exception) { }
		}

		internal void EditScript(ScriptNode node)
		{
			ScriptInfo si = node.Tag as ScriptInfo;
			if(si == null) return;
			BuilderPlug.Me.EditScript(si.ScriptFile);
		}

		internal void ClearSlot(ScriptNode node)
		{
			ScriptInfo si = node.Tag as ScriptInfo;
			if(si == null) return;
			int slot = BuilderPlug.Me.GetScriptSlotByScriptInfo(si);
			if(slot != 0) BuilderPlug.Me.SetScriptSlot(slot, null);
			FillTree();
		}

		internal void SetSlot(ScriptNode node, int slot)
		{
			ScriptInfo si = node.Tag as ScriptInfo;
			if(si == null) return;
			BuilderPlug.Me.SetScriptSlot(slot, si);
			FillTree();
		}

		/// <summary>Fills the tree with the scripts (the ones that match the filter), keeping the selected script selected.</summary>
		public void FillTree()
		{
			string previousscriptfile = string.Empty;
			string filtertext = (tbFilter.Text ?? "").ToLowerInvariant().Trim();

			ScriptNode selected = filetree.SelectedItem as ScriptNode;
			if(selected != null && selected.Tag is ScriptInfo) previousscriptfile = ((ScriptInfo)selected.Tag).ScriptFile;

			scriptoptions.Clear();

			roots = AddToTree(filtertext, BuilderPlug.Me.ScriptDirectoryStructure);

			updating = true;
			filetree.ItemsSource = null;
			filetree.ItemsSource = roots.ToArray();
			updating = false;

			foreach(ScriptNode node in roots)
			{
				ScriptNode result = FindScriptNode(previousscriptfile, node);
				if(result != null)
				{
					filetree.SelectedItem = result;
					break;
				}
			}
		}

		private ScriptNode FindScriptNode(string name, ScriptNode root)
		{
			if(root.Tag is ScriptInfo && ((ScriptInfo)root.Tag).ScriptFile == name) return root;

			foreach(ScriptNode node in root.Nodes)
			{
				ScriptNode next = FindScriptNode(name, node);
				if(next != null) return next;
			}

			return null;
		}

		private List<ScriptNode> AddToTree(string filtertext, ScriptDirectoryStructure sds)
		{
			List<ScriptNode> newnodes = new List<ScriptNode>();
			if(sds == null) return newnodes;

			foreach(ScriptDirectoryStructure subsds in sds.Directories.OrderBy(s => s.Name))
			{
				ScriptNode tn = new ScriptNode { Text = subsds.Name, Tag = subsds };
				tn.Nodes.AddRange(AddToTree(filtertext, subsds));
				tn.IsExpanded = subsds.Expanded;
				tn.ExpandedChanged = FolderExpandedChanged;
				newnodes.Add(tn);
			}

			foreach(ScriptInfo si in sds.Scripts.OrderBy(s => s.Name))
			{
				if(!string.IsNullOrWhiteSpace(filtertext))
				{
					if(!si.Name.ToLowerInvariant().Contains(filtertext) && !si.Description.ToLowerInvariant().Contains(filtertext))
						continue;
				}

				int slot = BuilderPlug.Me.GetScriptSlotByScriptInfo(si);
				newnodes.Add(new ScriptNode { Text = slot == 0 ? si.Name : si.Name + " [" + GetHotkeyText(slot) + "]", Tag = si });
			}

			return newnodes;
		}

		/// <summary>Takes what is being typed in the options.</summary>
		public void EndEdit()
		{
			scriptoptions.EndEdit();
		}

		#endregion

		#region ================== Events

		private void filetree_AfterSelect(object sender, SelectionChangedEventArgs e)
		{
			ScriptNode node = filetree.SelectedItem as ScriptNode;
			if(node == null || node.Tag == null)
			{
				tbDescription.Text = string.Empty;
				scriptoptions.Clear();
				return;
			}

			if(node.Tag is ScriptInfo)
			{
				ScriptInfo si = (ScriptInfo)node.Tag;
				BuilderPlug.Me.CurrentScript = si;
				scriptoptions.SetOptions(si.Options);
				scriptoptions.EndAddingOptions();
				tbDescription.Text = si.Description;
			}
			else
			{
				scriptoptions.Clear();
			}
		}

		private void btnRunScript_Click(object sender, EventArgs e)
		{
			BuilderPlug.Me.ScriptExecute();
		}

		private void btnResetToDefaults_Click(object sender, EventArgs e)
		{
			scriptoptions.ResetToDefaults();
			if(BuilderPlug.Me.CurrentScript != null)
			{
				foreach(ScriptOption so in BuilderPlug.Me.CurrentScript.Options)
					General.Settings.DeletePluginSetting("scriptoptions." + BuilderPlug.Me.CurrentScript.GetScriptPathHash() + "." + so.name);
			}
		}

		// A folder opened or closed: it is remembered
		private void FolderExpandedChanged(ScriptNode node)
		{
			if(updating) return;
			ScriptDirectoryStructure sds = node.Tag as ScriptDirectoryStructure;
			if(sds != null) sds.Expanded = node.IsExpanded;
			BuilderPlug.Me.SaveScriptDirectoryExpansionStatus(BuilderPlug.Me.ScriptDirectoryStructure);
		}

		#endregion
	}
}
