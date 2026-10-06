// The "Tag Explorer" docker: every tag, action special and (UDMF) comment of the map as a tree, with the elements under them. UDB's TagExplorer;
// the tree logic (UpdateTree, sorting, sector effects, node click, export) is the original's, and its TreeView/ComboBox/... became Avalonia controls.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Windows;
using DoomBuilder.UI;
using AvControl = Avalonia.Controls.Control;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvButton = Avalonia.Controls.Button;
using AvComboBox = Avalonia.Controls.ComboBox;
using AvTextBox = Avalonia.Controls.TextBox;
using AvColor = Avalonia.Media.Color;

namespace CodeImp.DoomBuilder.TagExplorer
{
	#region ================== Structs

	internal struct SortMode
	{
		public const string SORT_BY_INDEX = "By Index";
		public const string SORT_BY_TAG = "By Tag";
		public const string SORT_BY_ACTION = "By Action Special";
		public const string SORT_BY_POLYOBJ_NUMBER = "By Polyobject Number";
		public static readonly object[] SORT_MODES = new object[] { SORT_BY_INDEX, SORT_BY_TAG, SORT_BY_ACTION };
	}

	internal struct SelectedNode
	{
		public NodeInfoType Type;
		public int Index;
	}

	/// <summary>A node of the tree: what UDB's TreeNode held (text, icon, the NodeInfo in Tag, tooltip, color, children).</summary>
	public sealed class TagTreeNode
	{
		public string Text { get; set; }
		public int ImageIndex { get; set; }
		public int SelectedImageIndex { get; set; }
		public object Tag { get; set; }
		public string ToolTipText { get; set; }
		public System.Drawing.Color ForeColor { get; set; } = System.Drawing.Color.Empty;
		public bool IsExpanded { get; set; }
		public List<TagTreeNode> Nodes { get; } = new List<TagTreeNode>();

		public TagTreeNode(string text, int imageindex, int selectedimageindex)
		{
			Text = text;
			ImageIndex = imageindex;
			SelectedImageIndex = selectedimageindex;
		}

		public TagTreeNode(string text, int imageindex, int selectedimageindex, TagTreeNode[] children) : this(text, imageindex, selectedimageindex)
		{
			Nodes.AddRange(children);
		}

		public override string ToString() { return Text; }
	}

	#endregion

	public sealed class TagExplorer : System.Windows.Forms.Control
	{
		private struct SectorEffectData
		{
			public int Effect; //1024
			public string CategoryName; // "Secret"
			public string EffectName; // "Secret: Yes"
			public bool IsGeneralized;
		}

		private const string DISPLAY_TAGS_AND_ACTIONS = "Tags and Action Specials";
		private const string DISPLAY_TAGS = "Tags";
		private const string DISPLAY_ACTIONS = "Action Specials";
		private const string DISPLAY_POLYOBJECTS = "Polyobjects";
		private readonly object[] DISPLAY_MODES = { DISPLAY_TAGS_AND_ACTIONS, DISPLAY_TAGS, DISPLAY_ACTIONS, DISPLAY_POLYOBJECTS };

		private string currentDisplayMode;
		private string currentSortMode;

		private const string CAT_THINGS = "Things:";
		private const string CAT_SECTORS = "Sectors:";
		private const string CAT_LINEDEFS = "Linedefs:";

		private readonly System.Drawing.Color commentColor = System.Drawing.Color.DarkMagenta;
		private SelectedNode selection;
		private readonly string nodetooltip;

		private static bool udmf;
		internal static bool UDMF { get { return udmf; } }

		// The tree: the top level nodes, and the Avalonia controls that show it
		private readonly List<TagTreeNode> roots = new List<TagTreeNode>();
		private readonly Grid root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
		private readonly TreeView treeView = new TreeView();
		private readonly AvComboBox cbDisplayMode = new AvComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
		private readonly AvComboBox cbSortMode = new AvComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
		private readonly AvCheckBox cbCenterOnSelected = new AvCheckBox { Content = "Center view on selected map element" };
		private readonly AvCheckBox cbSelectOnClick = new AvCheckBox { Content = "Select on click" };
		private readonly AvCheckBox cbCommentsOnly = new AvCheckBox { Content = "Hide elements without comments" };
		private readonly TextBlock labelSearch = new TextBlock { Text = "Filter:", TextDecorations = TextDecorations.Underline, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(AvColor.FromRgb(0, 102, 204)) };
		private readonly AvTextBox tbSearch = new AvTextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
		private readonly AvButton btnClearSearch = new AvButton { Padding = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
		private readonly AvButton bExportToFile = new AvButton { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(3) };
		private readonly DispatcherTimer updatetimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
		private readonly Dictionary<int, Avalonia.Media.Imaging.Bitmap> icons = new Dictionary<int, Avalonia.Media.Imaging.Bitmap>();
		private Window parentwindow;
		private bool disposed;
		private bool updating;

		// For the tests
		internal TreeView Tree { get { return treeView; } }
		internal IList<TagTreeNode> Roots { get { return roots; } }
		internal AvComboBox DisplayMode { get { return cbDisplayMode; } }
		internal AvComboBox SortModeBox { get { return cbSortMode; } }
		internal AvCheckBox CenterOnSelected { get { return cbCenterOnSelected; } }
		internal AvCheckBox SelectOnClick { get { return cbSelectOnClick; } }
		internal AvCheckBox CommentsOnly { get { return cbCommentsOnly; } }
		internal AvTextBox Search { get { return tbSearch; } }
		internal AvButton ClearSearchButton { get { return btnClearSearch; } }
		internal AvButton ExportButton { get { return bExportToFile; } }
		internal void UpdateTreeNow() { updatetimer.Stop(); UpdateTree(false); }

		#region ================== Constructor / Disposer

		public TagExplorer()
		{
			selection = new SelectedNode();

			cbDisplayMode.ItemsSource = DISPLAY_MODES;
			cbDisplayMode.SelectedIndex = General.Clamp(General.Settings.ReadPluginSetting("displaymode", 0), 0, DISPLAY_MODES.Length - 1);
			cbDisplayMode.SelectionChanged += cbDisplayMode_SelectedIndexChanged;
			currentDisplayMode = cbDisplayMode.SelectedItem.ToString();

			cbSortMode.ItemsSource = SortMode.SORT_MODES;
			cbSortMode.SelectedIndex = General.Clamp(General.Settings.ReadPluginSetting("sortmode", 0), 0, SortMode.SORT_MODES.Length - 1);
			cbSortMode.SelectionChanged += cbSortMode_SelectedIndexChanged;
			currentSortMode = cbSortMode.SelectedItem.ToString();

			cbCenterOnSelected.IsChecked = General.Settings.ReadPluginSetting("centeronselected", false);
			cbSelectOnClick.IsChecked = General.Settings.ReadPluginSetting("doselect", false);

			udmf = (General.Map.Config.FormatInterface == "UniversalMapSetIO");

			string searchhint = "Enter '#' + tag number to show only specified tag. Example: #667" + Environment.NewLine
				+ "Enter '$' + effect number to show only specified effect. Example: $80" + Environment.NewLine
				+ "Enter '^' + polyobject number to filter by polyobject number. Example: ^22" + Environment.NewLine
				+ "Several wildcards can be combined.";

			if(udmf)
			{
				cbCommentsOnly.IsChecked = General.Settings.ReadPluginSetting("commentsonly", false);
				ToolTip.SetTip(labelSearch, "Enter text to find comment" + Environment.NewLine + searchhint);
				nodetooltip = "Double-click item to edit item's comment\r\nRight-click item to open item's Properties";
			}
			else
			{
				cbCommentsOnly.IsEnabled = false;
				ToolTip.SetTip(labelSearch, searchhint);
				nodetooltip = "Right-click item to open item's Properties";
			}

			BuildView();
		}

		// Disposer
		public void Dispose()
		{
			if(disposed) return;
			disposed = true;

			General.Settings.WritePluginSetting("sortmode", cbSortMode.SelectedIndex);
			General.Settings.WritePluginSetting("displaymode", cbDisplayMode.SelectedIndex);
			General.Settings.WritePluginSetting("centeronselected", cbCenterOnSelected.IsChecked == true);
			General.Settings.WritePluginSetting("doselect", cbSelectOnClick.IsChecked == true);

			if(udmf) General.Settings.WritePluginSetting("commentsonly", cbCommentsOnly.IsChecked == true);
			updatetimer.Stop();
			DetachWindow();
		}

		#endregion

		#region ================== View

		// The controls, laid out as UDB's designer had them: options on top, the tree, the export button below
		private void BuildView()
		{
			if(Properties.Resources.Files.ContainsKey("SearchClear")) btnClearSearch.Content = new Avalonia.Controls.Image { Source = ImageConvert.ToAvalonia(Properties.Resources.SearchClear), Width = 16, Height = 16 };
			ToolTip.SetTip(btnClearSearch, "Clear Search");
			bExportToFile.Content = ExportContent();

			var options = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), RowSpacing = 6, ColumnSpacing = 6, Margin = new Thickness(6) };
			Place(options, new TextBlock { Text = "Show:", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }, 0, 0);
			Place(options, cbDisplayMode, 0, 1, 2);
			Place(options, new TextBlock { Text = "Sort:", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }, 1, 0);
			Place(options, cbSortMode, 1, 1, 2);
			labelSearch.HorizontalAlignment = HorizontalAlignment.Right;
			Place(options, labelSearch, 2, 0);
			Place(options, tbSearch, 2, 1);
			Place(options, btnClearSearch, 2, 2);
			var checks = new StackPanel { Spacing = 2, Margin = new Thickness(6, 0, 6, 6) };
			checks.Children.Add(cbCenterOnSelected);
			checks.Children.Add(cbSelectOnClick);
			checks.Children.Add(cbCommentsOnly);
			var top = new StackPanel();
			top.Children.Add(options);
			top.Children.Add(checks);

			treeView.ItemTemplate = new FuncTreeDataTemplate<TagTreeNode>((node, scope) => CreateRow(node), node => node.Nodes);
			treeView.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
			{
				Setters = { new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(TagTreeNode.IsExpanded)) { Mode = BindingMode.TwoWay }) }
			});

			Grid.SetRow(top, 0);
			Grid.SetRow(treeView, 1);
			Grid.SetRow(bExportToFile, 2);
			root.Children.Add(top);
			root.Children.Add(treeView);
			root.Children.Add(bExportToFile);

			btnClearSearch.Click += (s, e) => tbSearch.Text = "";
			tbSearch.PropertyChanged += (s, e) => { if(e.Property == AvTextBox.TextProperty) tbSearch_TextChanged(s, EventArgs.Empty); };
			cbCommentsOnly.IsCheckedChanged += (s, e) => UpdateTree(true);
			bExportToFile.Click += bExportToFile_Click;
			updatetimer.Tick += updatetimer_Tick;

			// The docker tabs only keep the selected panel in the visual tree: being attached means being shown (UDB's VisibleChanged)
			root.AttachedToVisualTree += (s, e) => { AttachWindow(); if(!disposed && General.Map != null) UpdateTree(true); };
			root.DetachedFromVisualTree += (s, e) => { DetachWindow(); };
			NativeControl = root;
		}

		private AvControl ExportContent()
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			row.Children.Add(new Avalonia.Controls.Image { Source = ImageConvert.ToAvalonia(Properties.Resources.Save), Width = 16, Height = 16 });
			row.Children.Add(new TextBlock { Text = "Export to file...", VerticalAlignment = VerticalAlignment.Center });
			return row;
		}

		private static void Place(Grid grid, AvControl control, int row, int column, int columnspan = 1)
		{
			Grid.SetRow(control, row);
			Grid.SetColumn(control, column);
			Grid.SetColumnSpan(control, columnspan);
			grid.Children.Add(control);
		}

		// The icon of a node (the images of UDB's imageList1)
		private Avalonia.Media.Imaging.Bitmap IconFor(int index)
		{
			Avalonia.Media.Imaging.Bitmap icon;
			if(!icons.TryGetValue(index, out icon))
			{
				System.Drawing.Image image;
				switch(index)
				{
					case 0: image = Properties.Resources.ThingsGroup; break;
					case 1: image = Properties.Resources.Things; break;
					case 2: image = Properties.Resources.SectorsGroup; break;
					case 3: image = Properties.Resources.Sectors; break;
					case 4: image = Properties.Resources.LinesGroup; break;
					default: image = Properties.Resources.Lines; break;
				}
				icon = ImageConvert.ToAvalonia(image);
				icons[index] = icon;
			}
			return icon;
		}

		// One line of the tree: the icon and the text
		private AvControl CreateRow(TagTreeNode node)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Background = Avalonia.Media.Brushes.Transparent, Tag = node };
			row.Children.Add(new Avalonia.Controls.Image { Source = IconFor(node.ImageIndex), Width = 16, Height = 16 });
			var text = new TextBlock { Text = node.Text, VerticalAlignment = VerticalAlignment.Center };
			if(!node.ForeColor.IsEmpty) text.Foreground = new SolidColorBrush(AvColor.FromRgb(node.ForeColor.R, node.ForeColor.G, node.ForeColor.B));
			row.Children.Add(text);
			if(node.ToolTipText != null) ToolTip.SetTip(row, node.ToolTipText);
			row.PointerReleased += (s, e) =>
			{
				if(editing != null) return;
				var kind = e.GetCurrentPoint(null).Properties.PointerUpdateKind;
				if(kind == PointerUpdateKind.LeftButtonReleased || kind == PointerUpdateKind.RightButtonReleased)
					NodeClick(node, kind == PointerUpdateKind.RightButtonReleased);
			};
			row.DoubleTapped += (s, e) => { NodeDoubleClick(node, row); e.Handled = true; };
			return row;
		}

		// Shows the tree; the node is the one selected afterwards
		private void ShowTree(TagTreeNode selectedNode)
		{
			updating = true;
			try
			{
				treeView.ItemsSource = null;
				treeView.ItemsSource = roots.ToArray();
				treeView.SelectedItem = selectedNode;
			}
			finally { updating = false; }
		}

		// The main window being activated again gives a good idea when something could have been changed
		// as it is called every time a dialog window closes.
		private void AttachWindow()
		{
			if(parentwindow != null) return;
			parentwindow = TopLevel.GetTopLevel(root) as Window;
			if(parentwindow != null) parentwindow.Activated += ParentForm_Activated;
		}

		private void DetachWindow()
		{
			if(parentwindow != null) parentwindow.Activated -= ParentForm_Activated;
			parentwindow = null;
		}

		#endregion

		#region ================== Methods

		public void Setup()
		{
			AttachWindow();

			if(TopLevel.GetTopLevel(root) != null)
				UpdateTree(true);
		}

		public void Terminate()
		{
			DetachWindow();
			updatetimer.Stop();
		}

		// This sets the timer to update the list very soon (because we'll have problems if we just call updateTree now)
		public void UpdateTreeSoon()
		{
			updatetimer.Stop();
			updatetimer.Start();
		}


		private void UpdateTree(bool focusDisplay) 
		{
			if (!General.Map.Map.IsSafeToAccess)
				return;

			bool showTags = (currentDisplayMode == DISPLAY_TAGS || currentDisplayMode == DISPLAY_TAGS_AND_ACTIONS);
			bool showActions = (currentDisplayMode == DISPLAY_ACTIONS || currentDisplayMode == DISPLAY_TAGS_AND_ACTIONS);
			bool hasComment;
			string comment = "";
			string serachStr = (tbSearch.Text ?? "").ToLowerInvariant();

			HashSet<int> filteredtags = new HashSet<int>();
			HashSet<int> filteredactions = new HashSet<int>();
			HashSet<int> filteredpolyobjects = new HashSet<int>();
			GetSpecialValues(serachStr, ref filteredtags, ref filteredactions, ref filteredpolyobjects);

			if(!udmf || filteredtags.Count > 0 || filteredactions.Count > 0 || filteredpolyobjects.Count > 0)
				serachStr = "";

			TagTreeNode selectedNode = null;

						roots.Clear();

			List<TagTreeNode> nodes = new List<TagTreeNode>();

			//add things
			if(General.Map.FormatInterface.HasThingAction || General.Map.FormatInterface.HasThingTag) 
			{
				ICollection<Thing> things = General.Map.Map.Things;

				if(!(things is MapElementCollection<Thing>)) //don't want to enumerate when array is locked
				{ 
					foreach(Thing t in things) 
					{
						if((showTags && t.Tag != 0) || (showActions && t.Action > 0)) 
						{
							if(filteredtags.Count > 0 && !filteredtags.Contains(t.Tag)) continue;
							if(filteredactions.Count > 0 && !filteredactions.Contains(t.Action)) continue;

							NodeInfo info = new NodeInfo(t);
							if(filteredpolyobjects.Count > 0 && !filteredpolyobjects.Contains(info.PolyobjectNumber)) continue;

							string name = info.GetName(ref comment, currentSortMode);
							hasComment = comment.Length > 0;

							if(!hasComment && (cbCommentsOnly.IsChecked == true)) continue;

							if(!udmf || serachStr.Length == 0 || (hasComment && comment.ToLowerInvariant().IndexOf(serachStr) != -1)) 
							{
								TagTreeNode node = new TagTreeNode(name, 1, 1) { Tag = info, ToolTipText = nodetooltip };
								if(hasComment) node.ForeColor = commentColor;
								nodes.Add(node);

								if(info.Index == selection.Index && info.Type == selection.Type)
									selectedNode = node;
							}
						}
						else if(currentDisplayMode == DISPLAY_POLYOBJECTS)
						{
							NodeInfo info = new NodeInfo(t);
							if(info.PolyobjectNumber != int.MinValue && (filteredpolyobjects.Count == 0 || filteredpolyobjects.Contains(info.PolyobjectNumber)))
							{
								string name = info.GetName(ref comment, SortMode.SORT_BY_POLYOBJ_NUMBER);
								TagTreeNode node = new TagTreeNode(name, 1, 1) { Tag = info, ToolTipText = nodetooltip };
								nodes.Add(node);
							}
						}
					}

					//sort nodes
					Sort(ref nodes, currentSortMode);

					//add "things" category
					if(nodes.Count > 0) 
					{
						switch(currentSortMode)
						{
							case SortMode.SORT_BY_ACTION:
							{ 
								Dictionary<int, TagTreeNode> categories = new Dictionary<int, TagTreeNode>();
								TagTreeNode noAction = new TagTreeNode("No Action", 0, 0);

								foreach(TagTreeNode node in nodes) 
								{
									NodeInfo nodeInfo = node.Tag as NodeInfo;

									if(nodeInfo.Action == 0) 
									{
										noAction.Nodes.Add(node);
										continue;
									}

									LinedefActionInfo lai = General.Map.Config.GetLinedefActionInfo(nodeInfo.Action);

									if(!categories.ContainsKey(lai.Index))
										categories.Add(lai.Index, new TagTreeNode(lai.Index + " - " + lai.Name, 0, 0, new[] { node }));
									else
										categories[lai.Index].Nodes.Add(node);
								}

								TagTreeNode[] catNodes = new TagTreeNode[categories.Values.Count];
								categories.Values.CopyTo(catNodes, 0);

								TagTreeNode category = new TagTreeNode(CAT_THINGS, 0, 0, catNodes);
								if(noAction.Nodes.Count > 0) category.Nodes.Add(noAction);

								roots.Add(category);

							}
							break;

							case SortMode.SORT_BY_INDEX:
							{
								if(currentDisplayMode == DISPLAY_POLYOBJECTS)
								{
									TagTreeNode category = new TagTreeNode(CAT_THINGS, 0, 0);
									category.Nodes.AddRange(nodes.ToArray());
									roots.Add(category);
								}
								else
								{
									Dictionary<string, TagTreeNode> categories = new Dictionary<string, TagTreeNode>(StringComparer.Ordinal);
									foreach(TagTreeNode node in nodes)
									{
										NodeInfo nodeInfo = node.Tag as NodeInfo;
										ThingTypeInfo tti = General.Map.Data.GetThingInfoEx(General.Map.Map.GetThingByIndex(nodeInfo.Index).Type);

										if(tti != null)
										{
											if(!categories.ContainsKey(tti.Category.Title)) categories.Add(tti.Category.Title, new TagTreeNode(tti.Category.Title, 0, 0, new[] {node}));
											else categories[tti.Category.Title].Nodes.Add(node);
										}
										else
										{
											if(!categories.ContainsKey("UNKNOWN")) categories.Add("UNKNOWN", new TagTreeNode("UNKNOWN", 0, 0, new[] {node}));
											else categories["UNKNOWN"].Nodes.Add(node);
										}
									}
									TagTreeNode[] catNodes = new TagTreeNode[categories.Values.Count];
									categories.Values.CopyTo(catNodes, 0);

									roots.Add(new TagTreeNode(CAT_THINGS, 0, 0, catNodes));
								}
							}
							break;

							case SortMode.SORT_BY_TAG:
							{ 
								Dictionary<int, TagTreeNode> categories = new Dictionary<int, TagTreeNode>();
								TagTreeNode noTag = new TagTreeNode("No Tag", 0, 0);

								foreach(TagTreeNode node in nodes) 
								{
									NodeInfo nodeInfo = node.Tag as NodeInfo;

									if(nodeInfo.Tag == 0) 
									{
										noTag.Nodes.Add(node);
										continue;
									}

									if(!categories.ContainsKey(nodeInfo.Tag)) 
									{
										string title = "Tag " + nodeInfo.Tag;
										if(General.Map.Options.TagLabels.ContainsKey(nodeInfo.Tag))
											title += ": " + General.Map.Options.TagLabels[nodeInfo.Tag];

										categories.Add(nodeInfo.Tag, new TagTreeNode(title, 0, 0, new[] { node }));
									} 
									else 
									{
										categories[nodeInfo.Tag].Nodes.Add(node);
									}
								}

								TagTreeNode[] catNodes = new TagTreeNode[categories.Values.Count];
								categories.Values.CopyTo(catNodes, 0);

								TagTreeNode category = new TagTreeNode(CAT_THINGS, 0, 0, catNodes);
								if(noTag.Nodes.Count > 0) category.Nodes.Add(noTag);

								roots.Add(category);
							}
							break;
						}
					}
				}
			}

			//add sectors
			nodes = new List<TagTreeNode>();
			ICollection<Sector> sectors = General.Map.Map.Sectors;

			if(currentDisplayMode != DISPLAY_POLYOBJECTS && !(sectors is MapElementCollection<Sector>)) //don't want to enumerate when array is locked
			{ 
				foreach(Sector s in sectors) 
				{
					if((showTags && s.Tag != 0) || (showActions && s.Effect > 0)) 
					{
						if(filteredactions.Count > 0 && !filteredactions.Contains(s.Effect)) continue;
						for(int i = 0; i < s.Tags.Count; i++)
						{
							if(filteredtags.Count > 0 && !filteredtags.Contains(s.Tags[i])) continue;

							NodeInfo info = new NodeInfo(s, i);
							string name = info.GetName(ref comment, currentSortMode);
							hasComment = comment.Length > 0;

							if(!hasComment && (cbCommentsOnly.IsChecked == true)) continue;
							if(!udmf || serachStr.Length == 0 || (hasComment && comment.ToLowerInvariant().IndexOf(serachStr) != -1))
							{
								TagTreeNode node = new TagTreeNode(name, 3, 3) { Tag = info, ToolTipText = nodetooltip };
								if(hasComment) node.ForeColor = commentColor;
								nodes.Add(node);

								if(info.Index == selection.Index && info.Type == selection.Type)
									selectedNode = node;
							}
						}
					}
				}

				// Add category
				if(nodes.Count > 0) 
				{
					switch(currentSortMode)
					{
						case SortMode.SORT_BY_ACTION:
						{
							Dictionary<int, TagTreeNode> categories = new Dictionary<int, TagTreeNode>();
							TagTreeNode noAction = new TagTreeNode("No Effect", 2, 2);

							foreach(TagTreeNode node in nodes) 
							{
								NodeInfo info = node.Tag as NodeInfo;

								if(info.Action == 0) 
								{
									noAction.Nodes.Add(node);
									continue;
								}

								List<SectorEffectData> effects = GetSectorEffects(info.Action);
								if(effects.Count == 1 && !effects[0].IsGeneralized)
								{
									// The first node is already has all relevant data. Just add it
									if(!categories.ContainsKey(effects[0].Effect))
									{
										TagTreeNode catnode = new TagTreeNode(effects[0].Effect + " - " + effects[0].CategoryName, 2, 2, new[] {node});
										catnode.Tag = effects[0].Effect;
										categories.Add(effects[0].Effect, catnode);
									}
									else
									{
										categories[effects[0].Effect].Nodes.Add(node);
									}
								}
								else
								{
									// Add generalized effects
									foreach(SectorEffectData data in effects)
									{
										// Create NodeInfo for each effect... 
										NodeInfo geninfo = new NodeInfo(info, data.Effect);
										
										// Copy the initial node, otherwise it won't be added several times
										string name = geninfo.GetName(ref comment, currentSortMode);
										TagTreeNode nodecopy = new TagTreeNode(name, node.ImageIndex, node.SelectedImageIndex)
										{
											Tag = geninfo,
											ToolTipText = node.ToolTipText
										};

										if(!categories.ContainsKey(data.Effect))
										{
											TagTreeNode catnode = new TagTreeNode(data.Effect + " - " + data.CategoryName, 2, 2, new[] {nodecopy});
											catnode.Tag = data.Effect;
											categories.Add(data.Effect, catnode);
										}
										else
										{
											categories[data.Effect].Nodes.Add(nodecopy);
										}
									}
								}
							}

							// Because of generalized effects shenanigans, category and nodes resortings are required...
							TagTreeNode[] catnodes = new TagTreeNode[categories.Values.Count];
							categories.Values.CopyTo(catnodes, 0);

							// Sort categories
							Array.Sort(catnodes, delegate(TagTreeNode t1, TagTreeNode t2)
							{
								int effect1 = (int)t1.Tag;
								int effect2 = (int)t2.Tag;

								if(effect1 > effect2)  return 1;
								if(effect1 == effect2) return 0;
								return -1;
							});

							// Sort nodes
							foreach(TagTreeNode n in catnodes)
							{
								if(n.Nodes.Count > 0)
								{
									List<TagTreeNode> tosort = new List<TagTreeNode>(n.Nodes.Count);
									foreach(TagTreeNode nn in n.Nodes) tosort.Add(nn);
									Sort(ref tosort, currentSortMode);

									n.Nodes.Clear();
									n.Nodes.AddRange(tosort.ToArray());
								}
							}

							TagTreeNode category = new TagTreeNode(CAT_SECTORS, 2, 2, catnodes);
							if(noAction.Nodes.Count > 0) category.Nodes.Add(noAction);

							roots.Add(category);
						}
						break;

						case SortMode.SORT_BY_TAG:
						{
							Dictionary<int, TagTreeNode> categories = new Dictionary<int, TagTreeNode>();
							TagTreeNode noTag = new TagTreeNode("No Tag", 2, 2);

							// Sort nodes
							Sort(ref nodes, currentSortMode);

							foreach(TagTreeNode node in nodes) 
							{
								NodeInfo nodeInfo = node.Tag as NodeInfo;

								if(nodeInfo.Tag == 0) 
								{
									noTag.Nodes.Add(node);
									continue;
								}

								if(!categories.ContainsKey(nodeInfo.Tag)) 
								{
									string title = "Tag " + nodeInfo.Tag;
									if(General.Map.Options.TagLabels.ContainsKey(nodeInfo.Tag))
										title += ": " + General.Map.Options.TagLabels[nodeInfo.Tag];

									categories.Add(nodeInfo.Tag, new TagTreeNode(title, 2, 2, new[] { node }));
								} 
								else 
								{
									categories[nodeInfo.Tag].Nodes.Add(node);
								}
							}
							TagTreeNode[] catNodes = new TagTreeNode[categories.Values.Count];
							categories.Values.CopyTo(catNodes, 0);

							TagTreeNode category = new TagTreeNode(CAT_SECTORS, 2, 2, catNodes);
							if(noTag.Nodes.Count > 0) category.Nodes.Add(noTag);

							roots.Add(category);
						}
						break;

						default:
							// Sort nodes
							Sort(ref nodes, currentSortMode);

							roots.Add(new TagTreeNode(CAT_SECTORS, 2, 2, nodes.ToArray()));
							break;
					}
				}
			}

			//add linedefs
			nodes = new List<TagTreeNode>();
			ICollection<Linedef> linedefs = General.Map.Map.Linedefs;

			if(!(linedefs is MapElementCollection<Linedef>)) //don't want to enumerate when array is locked
			{ 
				foreach(Linedef l in linedefs) 
				{
					if((showTags && l.Tag != 0) || (showActions && l.Action > 0)) 
					{
						if(filteredactions.Count > 0 && !filteredactions.Contains(l.Action)) continue;

						NodeInfo firstinfo = new NodeInfo(l, 0);
						if(filteredpolyobjects.Count > 0 && !filteredpolyobjects.Contains(firstinfo.PolyobjectNumber)) continue;

						for(int i = 0; i < l.Tags.Count; i++)
						{
							if(filteredtags.Count > 0 && !filteredtags.Contains(l.Tags[i])) continue;

							NodeInfo info = new NodeInfo(l, i);
							string name = info.GetName(ref comment, currentSortMode);
							hasComment = comment.Length > 0;

							if(!hasComment && (cbCommentsOnly.IsChecked == true)) continue;
							if(!udmf || serachStr.Length == 0 || (hasComment && comment.ToLowerInvariant().IndexOf(serachStr) != -1))
							{
								TagTreeNode node = new TagTreeNode(name, 5, 5) { Tag = info, ToolTipText = nodetooltip };
								if(hasComment) node.ForeColor = commentColor;
								nodes.Add(node);

								if(info.Index == selection.Index && info.Type == selection.Type)
									selectedNode = node;
							}
						}
					} 
					else if(currentDisplayMode == DISPLAY_POLYOBJECTS) 
					{
						NodeInfo info = new NodeInfo(l, 0);
						if(info.PolyobjectNumber != int.MinValue && (filteredpolyobjects.Count == 0 || filteredpolyobjects.Contains(info.PolyobjectNumber))) 
						{
							string name = info.GetName(ref comment, SortMode.SORT_BY_POLYOBJ_NUMBER);
							TagTreeNode node = new TagTreeNode(name, 1, 1) { Tag = info, ToolTipText = nodetooltip };
							nodes.Add(node);
						}
					}
				}

				//sort nodes
				Sort(ref nodes, currentSortMode);

				//add category
				if(nodes.Count > 0) 
				{
					switch(currentSortMode)
					{
						case SortMode.SORT_BY_ACTION:
						{
							Dictionary<int, TagTreeNode> categories = new Dictionary<int, TagTreeNode>();
							TagTreeNode noAction = new TagTreeNode("No Action", 4, 4);

							foreach(TagTreeNode node in nodes)
							{
								NodeInfo nodeInfo = node.Tag as NodeInfo;

								if(nodeInfo.Action == 0)
								{
									noAction.Nodes.Add(node);
									continue;
								}

								LinedefActionInfo lai = General.Map.Config.GetLinedefActionInfo(nodeInfo.Action);

								if(!categories.ContainsKey(lai.Index)) categories.Add(lai.Index, new TagTreeNode(lai.Index + " - " + lai.Title, 4, 4, new[] { node }));
								else categories[lai.Index].Nodes.Add(node);
							}
							TagTreeNode[] catNodes = new TagTreeNode[categories.Values.Count];
							categories.Values.CopyTo(catNodes, 0);

							TagTreeNode category = new TagTreeNode(CAT_LINEDEFS, 4, 4, catNodes);
							if(noAction.Nodes.Count > 0) category.Nodes.Add(noAction);

							roots.Add(category);
						}
						break;

						case SortMode.SORT_BY_TAG:
						{
							Dictionary<int, TagTreeNode> categories = new Dictionary<int, TagTreeNode>();
							TagTreeNode noTag = new TagTreeNode("No Tag", 4, 4);

							foreach(TagTreeNode node in nodes) 
							{
								NodeInfo nodeInfo = node.Tag as NodeInfo;

								if(nodeInfo.Tag == 0) 
								{
									noTag.Nodes.Add(node);
									continue;
								}

								if(!categories.ContainsKey(nodeInfo.Tag)) 
								{
									string title = "Tag " + nodeInfo.Tag;
									if(General.Map.Options.TagLabels.ContainsKey(nodeInfo.Tag))
										title += ": " + General.Map.Options.TagLabels[nodeInfo.Tag];

									categories.Add(nodeInfo.Tag, new TagTreeNode(title, 4, 4, new[] { node }));
								} 
								else 
								{
									categories[nodeInfo.Tag].Nodes.Add(node);
								}
							}
							TagTreeNode[] catNodes = new TagTreeNode[categories.Values.Count];
							categories.Values.CopyTo(catNodes, 0);

							TagTreeNode category = new TagTreeNode(CAT_LINEDEFS, 4, 4, catNodes);
							if(noTag.Nodes.Count > 0) category.Nodes.Add(noTag);

							roots.Add(category);
						}
						break;

						default:
							roots.Add(new TagTreeNode(CAT_LINEDEFS, 4, 4, nodes.ToArray()));
							break;
					}
				}
			}

			//expand top level nodes
			foreach(TagTreeNode t in roots) t.IsExpanded = true;

			ShowTree(selectedNode != null ? selectedNode : (roots.Count > 0 ? roots[0] : null));

			
			//update Export button
			bExportToFile.IsEnabled = (roots.Count > 0);

			// Loose focus when the main windows is active
			if(focusDisplay) General.Interface.FocusDisplay();
		}

//tag/action search
		private static void GetSpecialValues(string serachstr, ref HashSet<int> filteredtags, ref HashSet<int> filteredactions, ref HashSet<int> filteredpolyobjects) 
		{
			if(serachstr.Length == 0) return;

			string[] tags = serachstr.Split(new[]{ '#' });
			foreach(string tagstr in tags)
			{
				int tag = ReadNumber(tagstr);
				if(tag != int.MinValue) filteredtags.Add(tag);
			}

			string[] actions = serachstr.Split(new[] { '$' });
			foreach(string actionstr in actions) 
			{
				int action = ReadNumber(actionstr);
				if(action != int.MinValue) filteredactions.Add(action);
			}

			string[] ponums = serachstr.Split(new[] { '^' });
			foreach(string postr in ponums) 
			{
				int ponum = ReadNumber(postr);
				if(ponum != int.MinValue) filteredpolyobjects.Add(ponum);
			}
		}

		private static int ReadNumber(string str) 
		{
			string token = "";
			int pos = 0;

			while(pos < str.Length && Configuration.NUMBERS.IndexOf(str[pos]) != -1) 
			{
				token += str[pos];
				pos++;
			}

			if(token.Length > 0) 
			{
				int result;
				if(int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
					return result;
			}

			return int.MinValue;
		}

		#endregion

		#region ================== Sorting

		private void Sort(ref List<TagTreeNode> nodes, string sortMode)
		{
			switch(sortMode)
			{
				case SortMode.SORT_BY_ACTION:
					nodes.Sort(SortByAction);
					break;

				case SortMode.SORT_BY_TAG:
					nodes.Sort(SortByTag);
					break;

				case SortMode.SORT_BY_INDEX:
					if(currentDisplayMode == DISPLAY_POLYOBJECTS) 
						nodes.Sort(SortByPolyobjectNumber);
					else
						nodes.Sort(SortByIndex);
					break;

				default:
					throw new NotImplementedException("Tag Explorer: Sort mode \"" + sortMode + "\" is not implemented!");
			}
		}

		private static int SortByAction(TagTreeNode t1, TagTreeNode t2) 
		{
			NodeInfo i1 = t1.Tag as NodeInfo;
			NodeInfo i2 = t2.Tag as NodeInfo;

			if(i1.Action == i2.Action) return SortByTag(t1, t2);
			if(i1.Action == 0) return 1; //push items with no action to the end of the list
			if(i2.Action == 0) return -1; //push items with no action to the end of the list
			if(i1.Action > i2.Action) return 1;
			return -1; //should be i1 < i2
		}

		private static int SortByTag(TagTreeNode t1, TagTreeNode t2) 
		{
			NodeInfo i1 = t1.Tag as NodeInfo;
			NodeInfo i2 = t2.Tag as NodeInfo;

			if(i1.Tag == i2.Tag) return SortByIndex(t1, t2);
			if(i1.Tag == 0) return 1; //push items with no tag to the end of the list
			if(i2.Tag == 0) return -1; //push items with no tag to the end of the list
			if(i1.Tag > i2.Tag) return 1;
			return -1; //should be i1 < i2
		}

		private static int SortByIndex(TagTreeNode t1, TagTreeNode t2) 
		{
			NodeInfo i1 = t1.Tag as NodeInfo;
			NodeInfo i2 = t2.Tag as NodeInfo;

			if(i1.Index > i2.Index) return 1;
			if(i1.Index == i2.Index) return 0;
			return -1;
		}

		private static int SortByPolyobjectNumber(TagTreeNode t1, TagTreeNode t2) 
		{
			NodeInfo i1 = t1.Tag as NodeInfo;
			NodeInfo i2 = t2.Tag as NodeInfo;

			if(i1.PolyobjectNumber > i2.PolyobjectNumber) return 1;
			if(i1.PolyobjectNumber == i2.PolyobjectNumber)
				return String.CompareOrdinal(i1.DefaultName, i2.DefaultName);
			return -1;
		}

		// This returns information on a sector effect
		private static List<SectorEffectData> GetSectorEffects(int effect)
		{
			List<SectorEffectData> result = new List<SectorEffectData>(1);
			
			// No effect?
			if(effect == 0)
			{
				result.Add(new SectorEffectData { Effect = 0, CategoryName = "None", EffectName = "None" });
			}
			// Known effect?
			else if(General.Map.Config.SectorEffects.ContainsKey(effect))
			{
				string title = General.Map.Config.SectorEffects[effect].Title;
				result.Add(new SectorEffectData { Effect = effect, CategoryName = title, EffectName = title });
			}
			else
			{
				// Generalized effect(s)?
				int initialeffect = effect;

				// Check all effects, in bigger to smaller order
				for(int i = General.Map.Config.GenEffectOptions.Count - 1; i > -1; i--)
				{
					for(int j = General.Map.Config.GenEffectOptions[i].Bits.Count - 1; j > -1; j--)
					{
						GeneralizedBit bit = General.Map.Config.GenEffectOptions[i].Bits[j];
						if(bit.Index > 0 && (effect & bit.Index) == bit.Index)
						{
							initialeffect -= bit.Index;
							result.Add(new SectorEffectData
							           {
								           Effect = bit.Index, 
										   CategoryName = General.Map.Config.GenEffectOptions[i].Name, 
										   EffectName = General.Map.Config.GenEffectOptions[i].Name + ": " + bit.Title, 
										   IsGeneralized = true,
							           });
							break;
						}
					}
				}

				// Add combined effect?
				if(result.Count > 0 && initialeffect > 0)
				{
					string combinedeffect = General.Map.Config.GetGeneralizedSectorEffectName(effect);
					result.Add(new SectorEffectData { Effect = effect, CategoryName = combinedeffect, EffectName = combinedeffect + ": " + effect, });
				}

				if(initialeffect > 0)
				{
					// Insert non-generalized effect as the first one
					if(General.Map.Config.SectorEffects.ContainsKey(initialeffect))
					{
						string title = General.Map.Config.SectorEffects[initialeffect].Title;
						result.Insert(0, new SectorEffectData { Effect = initialeffect, CategoryName = title, EffectName = title });
					}
					else
					{
						// Unknown effect...
						result.Insert(0, new SectorEffectData { Effect = initialeffect, CategoryName = "Unknown", EffectName = "Unknown" });
					}
				}
				else if(result.Count == 0)
				{
					// Unknown effect...
					result.Add(new SectorEffectData { Effect = effect, CategoryName = "Unknown", EffectName = "Unknown" });
				}
			}

			return result;
		}

		#endregion


		#region ================== Events

		private void cbDisplayMode_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
		{
			if(cbDisplayMode.SelectedItem == null) return;
			currentDisplayMode = cbDisplayMode.SelectedItem.ToString();
			UpdateTree(true);
		}

		private void cbSortMode_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
		{
			if(cbSortMode.SelectedItem == null) return;
			currentSortMode = cbSortMode.SelectedItem.ToString();
			UpdateTree(true);
		}

		// A node was clicked with the left button (select / center), or with the right one (properties)
		internal void NodeClick(TagTreeNode node, bool rightclick)
		{
			NodeInfo info = node.Tag as NodeInfo;
			if(info == null) return;

			//store selection
			selection.Type = info.Type;
			selection.Index = info.Index;

			if(rightclick) //open element properties
			{ 
				bool updateDisplay = false;
				
				switch(info.Type) 
				{
					case NodeInfoType.THING:
						Thing t = General.Map.Map.GetThingByIndex(info.Index);
						updateDisplay = (t != null && General.Interface.ShowEditThings(new List<Thing>() { t }) == DialogResult.OK);
						break;

					case NodeInfoType.SECTOR:
						Sector s = General.Map.Map.GetSectorByIndex(info.Index);
						updateDisplay = (s != null && General.Interface.ShowEditSectors(new List<Sector>() { s }) == DialogResult.OK);
						break;

					case NodeInfoType.LINEDEF:
						Linedef l = General.Map.Map.GetLinedefByIndex(info.Index);
						updateDisplay = (l != null && General.Interface.ShowEditLinedefs(new List<Linedef>() { l }) == DialogResult.OK);
						break;

					default:
						General.ErrorLogger.Add(ErrorType.Warning, "Tag Explorer: got unknown category: " + info.Type);
						break;
				}

				if(updateDisplay) 
				{
					// Update entire display
					General.Map.Map.Update();
					General.Interface.RedrawDisplay();
					UpdateTree(true);
				}

			} 
			else 
			{
				//select element?
				if((cbSelectOnClick.IsChecked == true)) 
				{
					// Leave any volatile mode
					General.Editing.CancelVolatileMode();
					General.Map.Map.ClearAllSelected();

					//make selection
					switch(info.Type)
					{
						case NodeInfoType.THING:
						{
							if(General.Editing.Mode.GetType().Name != "ThingsMode") General.Editing.ChangeMode("ThingsMode");
							Thing t = General.Map.Map.GetThingByIndex(info.Index);
							if(t != null) t.Selected = true;
						}
						break;

						case NodeInfoType.LINEDEF:
						{
							if(General.Editing.Mode.GetType().Name != "LinedefsMode") General.Editing.ChangeMode("LinedefsMode");
							Linedef l = General.Map.Map.GetLinedefByIndex(info.Index);
							if(l != null) l.Selected = true;
						}
						break;

						default:
						{
							if(General.Editing.Mode.GetType().Name != "SectorsMode") General.Editing.ChangeMode("SectorsMode");
							Sector s = General.Map.Map.GetSectorByIndex(info.Index);
							if(s != null) 
							{
								((ClassicMode)General.Editing.Mode).SelectMapElement(s);
								foreach(Sidedef sd in s.Sidedefs) sd.Line.Selected = true;
							}
						}
						break;
					}
				}

				//focus on element?
				if((cbCenterOnSelected.IsChecked == true)) 
				{
					List<Vector2D> points = new List<Vector2D>();
					RectangleF area = MapSet.CreateEmptyArea();

					switch(info.Type)
					{
						case NodeInfoType.LINEDEF:
						{
							Linedef l = General.Map.Map.GetLinedefByIndex(info.Index);
							points.Add(l.Start.Position);
							points.Add(l.End.Position);
						}
						break;

						case NodeInfoType.SECTOR:
						{
							Sector s = General.Map.Map.GetSectorByIndex(info.Index);
							foreach(Sidedef sd in s.Sidedefs) 
							{
								points.Add(sd.Line.Start.Position);
								points.Add(sd.Line.End.Position);
							}
						}
						break;

						case NodeInfoType.THING:
						{
							Thing t = General.Map.Map.GetThingByIndex(info.Index);
							Vector2D p = t.Position;
							points.Add(p);
							points.Add(p + new Vector2D(t.Size * 2.0f, t.Size * 2.0f));
							points.Add(p + new Vector2D(t.Size * 2.0f, -t.Size * 2.0f));
							points.Add(p + new Vector2D(-t.Size * 2.0f, t.Size * 2.0f));
							points.Add(p + new Vector2D(-t.Size * 2.0f, -t.Size * 2.0f));
						}
						break;

						default:
							General.Fail("Tag Explorer: unknown object type given to zoom in on!");
							break;
					}

					// Make a view area from the points
					foreach(Vector2D p in points) area = MapSet.IncreaseArea(area, p);

					// Make the area square, using the largest side
					if(area.Width > area.Height) 
					{
						float delta = area.Width - area.Height;
						area.Y -= delta * 0.5f;
						area.Height += delta;
					} 
					else 
					{
						float delta = area.Height - area.Width;
						area.X -= delta * 0.5f;
						area.Width += delta;
					}

					// Add padding
					area.Inflate(100f, 100f);

					// Zoom to area
					ClassicMode editmode = (General.Editing.Mode as ClassicMode);
					editmode.CenterOnArea(area, 0.6f);
				}

				// Update info and view
				General.Editing.Mode.UpdateSelectionInfo();
				General.Interface.RedrawDisplay();
			}
		}

		// Double-click: edit the comment right in the tree
		private TextBox editing;

		internal void NodeDoubleClick(TagTreeNode node, StackPanel row)
		{
			//edit comment
			if(udmf && currentDisplayMode != DISPLAY_POLYOBJECTS)
			{
				NodeInfo info = node.Tag as NodeInfo;
				if(info == null) return;       //we don't want to edit categories

				var box = new TextBox { Text = info.Comment, MinWidth = 120 };   //set node text to comment
				editing = box;
				TextBlock text = (TextBlock)row.Children[1];
				row.Children[1] = box;
				bool done = false;
				Action<bool> finish = accept =>
				{
					if(done) return;
					done = true;
					editing = null;
					row.Children[1] = text;
					if(accept) EditComment(node, box.Text);
					else UpdateTree(false);
				};
				box.KeyDown += (s, e) =>
				{
					if(e.Key == Key.Enter) { finish(true); e.Handled = true; }
					else if(e.Key == Key.Escape) { finish(false); e.Handled = true; }
				};
				box.LostFocus += (s, e) => finish(true);
				box.AttachedToVisualTree += (s, e) => { box.Focus(); box.SelectAll(); };
			}
		}

		//map should be in UDMF format, or we wouldn't be here
		internal void EditComment(TagTreeNode node, string label)
		{
			NodeInfo info = node.Tag as NodeInfo;
			if(info == null) return;

			//apply comment
			if(label != null) info.Comment = label;

			// Because of multiple tags, several nodes can link to the same sector/linedef
			UpdateTree(false);
		}

		private void ParentForm_Activated(object sender, EventArgs e)
		{
			UpdateTreeSoon();
		}

		private void tbSearch_TextChanged(object sender, EventArgs e)
		{
			string text = tbSearch.Text ?? "";
			if(text.Length > 1 || text.Length == 0) UpdateTree(false);
		}

		private void updatetimer_Tick(object sender, EventArgs e)
		{
			updatetimer.Stop();

			if(!BuilderPlug.Me.IsDockerActive())
				return;

			UpdateTree(General.Interface.IsActiveWindow);
		}

		private void bExportToFile_Click(object sender, EventArgs e)
		{
			if(roots.Count == 0) return;

			// Show save dialog
			SaveFileDialog saveFileDialog = new SaveFileDialog { Filter = "Text files|*.txt", Title = "Choose save location:", OverwritePrompt = true };
			string path = Path.GetDirectoryName(General.Map.FilePathName);
			saveFileDialog.InitialDirectory = path;
			saveFileDialog.FileName = Path.GetFileNameWithoutExtension(General.Map.FileTitle) + "_info.txt";
			if(General.Dialogs.ShowFileDialog(saveFileDialog) != DialogResult.OK) return;

			// Generate stuff
			StringBuilder sb = new StringBuilder();

			//top level
			foreach(TagTreeNode n in roots) 
			{
				if(n.Nodes.Count == 0) continue;

				if(sb.Length > 0) sb.AppendLine(Environment.NewLine);
				sb.AppendLine(n.Text.Replace(":", " (" + currentSortMode.ToLowerInvariant() + "):"));

				//second level
				foreach(TagTreeNode cn in n.Nodes) 
				{
					//third level
					if(cn.Nodes.Count > 0) 
					{
						sb.AppendLine("  " + cn.Text + ":");
						foreach(TagTreeNode ccn in cn.Nodes) sb.AppendLine("    " + ccn.Text);
					} 
					else 
					{
						sb.AppendLine("  " + cn.Text);
					}
				}
			}
			
			// Save to file
			try
			{
				using(StreamWriter sw = File.CreateText(saveFileDialog.FileName)) sw.Write(sb.ToString());
				General.Interface.DisplayStatus(StatusType.Info, "Tag info successfully saved.");
			}
			catch(Exception ex)
			{
				General.ErrorLogger.Add(ErrorType.Error, "Failed to save tag info: " + ex.Message);
				General.Interface.DisplayStatus(StatusType.Info, "Failed to save tag info...");
			}
		}

		#endregion
	}
}
