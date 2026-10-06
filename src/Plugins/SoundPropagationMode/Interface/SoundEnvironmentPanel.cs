// The "Sound Environments" docker of the sound environment mode: every environment (the sectors that share a sound, split by the zone
// boundary lines) as a tree with its things and lines, warnings for the ones that are wrong, and a filter for those. UDB's
// SoundEnvironmentPanel; the logic is the original's, the tree is Avalonia's (the panel travels inside the Docker as the native control of a
// shim Control). The environments are found on a background worker: BeginUpdate / EndUpdate may be called from its thread.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.SoundPropagationMode
{
	/// <summary>A node of the tree of sound environments.</summary>
	public sealed class SoundTreeNode : System.ComponentModel.INotifyPropertyChanged
	{
		private bool expanded;

		public string Text { get; set; }
		public object Tag { get; set; }
		public int ImageIndex { get; set; }
		public string ToolTipText { get; set; }
		public bool Bold { get; set; }
		public SoundTreeNode Parent { get; set; }
		public List<SoundTreeNode> Nodes { get; } = new List<SoundTreeNode>();

		public bool IsExpanded
		{
			get { return expanded; }
			set
			{
				if(expanded == value) return;
				expanded = value;
				if(PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
			}
		}

		public SoundTreeNode(string text) { Text = text; ImageIndex = -1; }

		public void Add(SoundTreeNode child) { child.Parent = this; Nodes.Add(child); }

		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
		public override string ToString() { return Text; }
	}

	public class SoundEnvironmentPanel : System.Windows.Forms.Control
	{
		public event EventHandler OnShowWarningsOnlyChanged;

		private const string shownodewarningstext = "Show nodes with warnings only (N)";

		private readonly Grid root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
		private readonly TreeView soundenvironments = new TreeView();
		private readonly CheckBox showwarningsonly = new CheckBox { Content = "Show nodes with warnings only", Margin = new Thickness(6) };
		private readonly Dictionary<int, Avalonia.Media.Imaging.Bitmap> icons = new Dictionary<int, Avalonia.Media.Imaging.Bitmap>();
		private readonly List<SoundTreeNode> nodes = new List<SoundTreeNode>();
		private readonly int warningiconindex;
		private bool treeisupdating;
		private int nodewarningscount;
		private bool refreshpending;
		private bool refreshing;

		// For the tests
		internal TreeView Tree { get { return soundenvironments; } }
		internal IList<SoundTreeNode> Nodes { get { return nodes; } }
		internal CheckBox ShowWarningsOnly { get { return showwarningsonly; } }
		internal int WarningIconIndex { get { return warningiconindex; } }
		internal SoundTreeNode SelectedNode { get { return soundenvironments.SelectedItem as SoundTreeNode; } }
		internal void RefreshNow() { Refresh(); }
		internal void NodeClicked(SoundTreeNode node) { soundenvironments.SelectedItem = node; ProcessNodeClick(node); }

		public SoundEnvironmentPanel()
		{
			// The icons: one for each color of the environments, then the warning
			warningiconindex = BuilderPlug.Me.DistinctIcons.Count;

			soundenvironments.ItemTemplate = new FuncTreeDataTemplate<SoundTreeNode>((node, scope) => CreateRow(node), node => node.Nodes);
			soundenvironments.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
			{
				Setters = { new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(SoundTreeNode.IsExpanded)) { Mode = BindingMode.TwoWay }) }
			});
			showwarningsonly.IsCheckedChanged += (s, e) => showwarningsonly_CheckedChanged(s, EventArgs.Empty);

			Grid.SetRow(soundenvironments, 0);
			Grid.SetRow(showwarningsonly, 1);
			root.Children.Add(soundenvironments);
			root.Children.Add(showwarningsonly);
			NativeControl = root;
		}

		private Avalonia.Media.Imaging.Bitmap Icon(int index)
		{
			if(index < 0) return null;
			Avalonia.Media.Imaging.Bitmap icon;
			if(!icons.TryGetValue(index, out icon))
			{
				System.Drawing.Image image = index == warningiconindex ? (System.Drawing.Image)Properties.Resources.Warning : BuilderPlug.Me.DistinctIcons[index];
				icon = ImageConvert.ToAvalonia(image);
				icons[index] = icon;
			}
			return icon;
		}

		private Control CreateRow(SoundTreeNode node)
		{
			var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Background = Avalonia.Media.Brushes.Transparent, Tag = node };
			Avalonia.Media.Imaging.Bitmap icon = Icon(node.ImageIndex);
			if(icon != null) row.Children.Add(new Avalonia.Controls.Image { Source = icon, Width = 16, Height = 16 });
			row.Children.Add(new TextBlock { Text = node.Text, FontWeight = node.Bold ? FontWeight.Bold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center });
			if(node.ToolTipText != null) ToolTip.SetTip(row, node.ToolTipText);
			row.PointerPressed += (s, e) =>
			{
				if(!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed) return;
				ProcessNodeClick(node);
			};
			return row;
		}

		#region ================== Updating

		// What the tree shows is made again from the nodes (one refresh covers all the changes made before it runs)
		private void Refresh()
		{
			refreshpending = false;
			refreshing = true;
			SoundTreeNode selected = soundenvironments.SelectedItem as SoundTreeNode;
			soundenvironments.ItemsSource = null;
			soundenvironments.ItemsSource = nodes.ToArray();
			if(selected != null && SelectedStill(selected)) soundenvironments.SelectedItem = selected;
			refreshing = false;
		}

		private bool SelectedStill(SoundTreeNode node)
		{
			return nodes.Contains(node) || (node.Parent != null && SelectedStill(node.Parent));
		}

		private void RefreshSoon()
		{
			if(treeisupdating || refreshpending) return;
			refreshpending = true;
			Dispatcher.UIThread.Post(Refresh);
		}

		/// <summary>Removes all the environments from the tree.</summary>
		public void ClearSoundEnvironments()
		{
			RunOnUI(() => { nodes.Clear(); Refresh(); });
		}

		private static void RunOnUI(Action action)
		{
			if(Dispatcher.UIThread.CheckAccess()) action();
			else Dispatcher.UIThread.Invoke(action, DispatcherPriority.Normal);   // (in line with the progress reports of the worker, which are posted at this priority)
		}

		public void BeginUpdate()
		{
			if(treeisupdating) return;
			RunOnUI(() => { nodewarningscount = 0; });
		}

		public void EndUpdate()
		{
			RunOnUI(() =>
			{
				treeisupdating = false;
				nodes.Sort((a, b) => string.Compare(a.Text, b.Text, StringComparison.CurrentCulture));
				Refresh();
				showwarningsonly.Content = shownodewarningstext.Replace("(N)", "(" + nodewarningscount + ")");
				showwarningsonly.IsEnabled = (showwarningsonly.IsChecked == true || nodewarningscount > 0);
			});
		}

		#endregion

		#region ================== Methods

		/// <summary>Adds an environment (with its things and lines, and a warning for what is wrong) to the tree.</summary>
		public void AddSoundEnvironment(SoundEnvironment se)
		{
			SoundTreeNode topnode = new SoundTreeNode(se.Name) { Tag = se };
			SoundTreeNode thingsnode = new SoundTreeNode("Things (" + se.Things.Count + ")");
			SoundTreeNode linedefsnode = new SoundTreeNode("Linedefs (" + se.Linedefs.Count + ")");
			int notdormant = 0;
			int iconindex = BuilderPlug.Me.DistinctColors.IndexOf(se.Color);
			int topindex = iconindex;
			bool nodehaswarnings = false;

			thingsnode.ImageIndex = iconindex;
			linedefsnode.ImageIndex = iconindex;

			// Things
			foreach(Thing t in se.Things)
			{
				SoundTreeNode thingnode = new SoundTreeNode("Thing " + t.Index) { Tag = t, ImageIndex = iconindex };
				thingsnode.Add(thingnode);

				if(!BuilderPlug.ThingDormant(t))
				{
					notdormant++;
				}
				else
				{
					thingnode.Text += " (dormant)";
				}
			}

			// More than one active thing in an environment is a mistake
			if(notdormant > 1)
			{
				thingsnode.ImageIndex = warningiconindex;
				topindex = warningiconindex;

				foreach(SoundTreeNode tn in thingsnode.Nodes)
				{
					if(!BuilderPlug.ThingDormant((Thing)tn.Tag))
					{
						tn.ImageIndex = warningiconindex;
						tn.ToolTipText = "More than one thing in this\nsound environment is set to be\nactive. Set all but one thing\nto dormant.";
						nodewarningscount++;
						nodehaswarnings = true;
					}
				}
			}

			// Linedefs
			foreach(Linedef ld in se.Linedefs)
			{
				bool showwarning = false;
				SoundTreeNode linedefnode = new SoundTreeNode("Linedef " + ld.Index) { Tag = ld, ImageIndex = iconindex };

				if(ld.Back == null || ld.Front == null)
				{
					showwarning = true;
					linedefnode.ToolTipText = "This line is single-sided, but has\nthe sound boundary flag set.";
				}
				else if(se.Sectors.Contains(ld.Front.Sector) && se.Sectors.Contains(ld.Back.Sector))
				{
					showwarning = true;
					linedefnode.ToolTipText = "The sectors on both sides of\nthe line belong to the same\nsound environment.";
				}

				if(showwarning)
				{
					linedefnode.ImageIndex = warningiconindex;
					linedefsnode.ImageIndex = warningiconindex;
					topindex = warningiconindex;
					nodewarningscount++;
					nodehaswarnings = true;
				}

				linedefsnode.Add(linedefnode);
			}

			if(showwarningsonly.IsChecked != true || nodehaswarnings)
			{
				topnode.Add(thingsnode);
				topnode.Add(linedefsnode);
				topnode.ImageIndex = topindex;

				// Keep the environments in the order of their numbers
				int insertionplace = 0;
				foreach(SoundTreeNode tn in nodes)
				{
					if(se.ID < ((SoundEnvironment)tn.Tag).ID) break;
					insertionplace++;
				}

				nodes.Insert(insertionplace, topnode);
				RefreshSoon();
			}
		}

		/// <summary>Opens (and shows in bold) the environment that is highlighted in the map, and closes the others.</summary>
		public void HighlightSoundEnvironment(SoundEnvironment se)
		{
			if(soundenvironments.SelectedItem != null) return;

			foreach(SoundTreeNode tn in nodes)
			{
				bool match = (se != null && ((SoundEnvironment)tn.Tag).ID == se.ID);
				tn.Bold = match;
				tn.IsExpanded = match;
				foreach(SoundTreeNode child in tn.Nodes) child.IsExpanded = false;
			}

			Refresh();
		}

		/// <summary>Selects an environment (or clears the selection when it is the selected one already).</summary>
		public void SelectSoundEnvironment(SoundEnvironment se)
		{
			SoundTreeNode selected = soundenvironments.SelectedItem as SoundTreeNode;
			if((se == null && selected != null) || (se != null && selected != null && selected.Tag is SoundEnvironment && ((SoundEnvironment)selected.Tag).ID == se.ID))
			{
				soundenvironments.SelectedItem = null;
				return;
			}

			if(se == null) return;

			foreach(SoundTreeNode tn in nodes)
			{
				if(((SoundEnvironment)tn.Tag).ID == se.ID)
				{
					soundenvironments.SelectedItem = tn;
					return;
				}
			}
		}

		// Clicking an environment, a thing or a line centers the view on it
		private static void ProcessNodeClick(SoundTreeNode node)
		{
			if(node == null) return;

			List<Vector2D> points = new List<Vector2D>();
			RectangleF area = MapSet.CreateEmptyArea();

			if(node.Parent == null)
			{
				if(node.Tag is SoundEnvironment)
				{
					SoundEnvironment se = (SoundEnvironment)node.Tag;
					foreach(Sector s in se.Sectors)
					{
						foreach(Sidedef sd in s.Sidedefs)
						{
							points.Add(sd.Line.Start.Position);
							points.Add(sd.Line.End.Position);
						}
					}
				}
				else
				{
					return;
				}
			}
			else
			{
				if(node.Tag is Thing)
				{
					Thing t = (Thing)node.Tag;
					points.Add(t.Position - 200);
					points.Add(t.Position + 200);
				}
				else if(node.Tag is Linedef)
				{
					Linedef ld = (Linedef)node.Tag;
					points.Add(ld.Start.Position);
					points.Add(ld.End.Position);
				}
				else
				{
					return;
				}
			}

			area = MapSet.IncreaseArea(area, points);
			area.Inflate(100f, 100f);

			ClassicMode editmode = (General.Editing.Mode as ClassicMode);
			editmode.CenterOnArea(area, 0.0f);
		}

		public void Dispose() { }

		#endregion

		#region ================== Events

		private void showwarningsonly_CheckedChanged(object sender, EventArgs e)
		{
			if(OnShowWarningsOnlyChanged != null) OnShowWarningsOnlyChanged(this, EventArgs.Empty);
		}

		#endregion
	}
}
