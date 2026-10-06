// The window of the nodes viewer mode: the numbers of the BSP tree (Overview), one split at a time (Splits) and one subsector with its
// segs (Subsectors). UDB's NodesForm as a window without a modal loop; closing it leaves the mode. The logic is the original's.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvControl = Avalonia.Controls.Control;
using AvButton = Avalonia.Controls.Button;
using AvCheckBox = Avalonia.Controls.CheckBox;

namespace CodeImp.DoomBuilder.Plugins.NodesViewer
{
	public class NodesForm : IDisposable
	{
		#region ================== Variables

		private readonly NodesViewerMode mode;
		private readonly Window window = new Window { Width = 360, SizeToContent = SizeToContent.Height, CanResize = false, ShowInTaskbar = false, Title = "Nodes Viewer" };
		private bool closing;
		private bool updating;

		private readonly TabControl tabs = new TabControl();
		private readonly TabItem taboverview = new TabItem { Header = "Overview" };
		private readonly TabItem tabsplits = new TabItem { Header = "Splits" };
		private readonly TabItem tabsubsectors = new TabItem { Header = "Subsectors" };

		// Overview
		private readonly TextBlock numsegs = V(), numsplits = V(), numssectors = V(), numvertices = V(), treedepth = V(), treebalance = V();
		private readonly AvCheckBox showsegsvertices = new AvCheckBox { Content = "Show additional vertices (seg splits)" };
		private readonly AvButton buildnodesbutton = new AvButton { Content = "Rebuild nodes" };

		// Splits
		private readonly NumericUpDown splitindex = Spinner();
		private readonly AvButton rootbutton = new AvButton { Content = "Go to root" };
		private readonly TextBlock parentsplit = V();
		private readonly AvButton parentbutton = new AvButton { Content = "Go to parent" };
		private readonly TextBlock lefttype = new TextBlock { Text = "Subsector:" }, leftindex = V(), leftarea = V("0, 0 - 0, 0");
		private readonly AvButton leftbutton = new AvButton { Content = "Go to subsector" };
		private readonly TextBlock righttype = new TextBlock { Text = "Subsector:" }, rightindex = V(), rightarea = V("0, 0 - 0, 0");
		private readonly AvButton rightbutton = new AvButton { Content = "Go to subsector" };

		// Subsectors
		private readonly NumericUpDown ssectorindex = Spinner();
		private readonly AvButton ssparentbutton = new AvButton { Content = "Go to parent split" };
		private readonly TextBlock ssectornumsegs = V();
		private readonly AvCheckBox viewsegbox = new AvCheckBox { Content = "View segment:", IsChecked = true };
		private readonly NumericUpDown segindex = Spinner();
		private readonly TextBlock lineindex = V(), startvertex = V(), endvertex = V(), segangle = V(), segside = V(), segoffset = V(), sideindex = V(), sectorindex = V();

		#endregion

		#region ================== Properties

		public string Text { get { return window.Title; } set { window.Title = value; } }
		public int SelectedTab { get { return tabs.SelectedIndex; } }
		public int ViewSplitIndex { get { return (int)(splitindex.Value ?? 0); } }
		public int ViewSubsectorIndex { get { return (int)(ssectorindex.Value ?? 0); } }
		public int ViewSegIndex { get { return viewsegbox.IsChecked == true ? (int)(segindex.Value ?? 0) : -1; } }
		public bool ShowSegsVertices { get { return showsegsvertices.IsChecked == true; } set { showsegsvertices.IsChecked = value; } }
		public PixelPoint Location { get { return window.Position; } set { window.Position = value; } }

		// For the tests
		internal Window Window { get { return window; } }
		internal TabControl Tabs { get { return tabs; } }
		internal NumericUpDown SplitIndex { get { return splitindex; } }
		internal NumericUpDown SubsectorIndex { get { return ssectorindex; } }
		internal NumericUpDown SegIndex { get { return segindex; } }
		internal AvCheckBox ViewSegBox { get { return viewsegbox; } }
		internal AvButton RootButton { get { return rootbutton; } }
		internal AvButton ParentButton { get { return parentbutton; } }
		internal AvButton LeftButton { get { return leftbutton; } }
		internal AvButton RightButton { get { return rightbutton; } }
		internal AvButton SubsectorParentButton { get { return ssparentbutton; } }
		internal AvButton BuildNodesButton { get { return buildnodesbutton; } }
		internal string Segs { get { return numsegs.Text; } }
		internal string Splits { get { return numsplits.Text; } }
		internal string Subsectors { get { return numssectors.Text; } }
		internal string Vertices { get { return numvertices.Text; } }
		internal string Depth { get { return treedepth.Text; } }
		internal string Balance { get { return treebalance.Text; } }
		internal string ParentSplitText { get { return parentsplit.Text; } }
		internal string LeftType { get { return lefttype.Text; } }
		internal string RightType { get { return righttype.Text; } }
		internal string LeftIndex { get { return leftindex.Text; } }
		internal string RightIndex { get { return rightindex.Text; } }
		internal string LeftArea { get { return leftarea.Text; } }
		internal string RightArea { get { return rightarea.Text; } }
		internal string SubsectorSegs { get { return ssectornumsegs.Text; } }
		internal string LineIndex { get { return lineindex.Text; } }
		internal string StartVertex { get { return startvertex.Text; } }
		internal string EndVertex { get { return endvertex.Text; } }
		internal string SegSide { get { return segside.Text; } }
		internal string SideIndex { get { return sideindex.Text; } }
		internal string SectorIndex { get { return sectorindex.Text; } }

		#endregion

		#region ================== Constructor / Destructor

		private static TextBlock V(string text = "0") { return new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Left }; }

		private static NumericUpDown Spinner()
		{
			return new NumericUpDown { Minimum = 0, Maximum = 0, Value = 0, Increment = 1, FormatString = "0", MinWidth = 110, ParsingNumberStyle = System.Globalization.NumberStyles.Integer };
		}

		// A column of "label: value" lines
		private static Grid Rows(params (string, AvControl)[] rows)
		{
			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 4, ColumnSpacing = 12 };
			for(int i = 0; i < rows.Length; i++)
			{
				grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
				var label = new TextBlock { Text = rows[i].Item1, VerticalAlignment = VerticalAlignment.Center };
				Grid.SetRow(label, i);
				Grid.SetRow(rows[i].Item2, i);
				Grid.SetColumn(rows[i].Item2, 1);
				grid.Children.Add(label);
				grid.Children.Add(rows[i].Item2);
			}
			return grid;
		}

		private static AvControl Box(string title, AvControl content)
		{
			var panel = new StackPanel { Spacing = 4 };
			panel.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.Bold });
			panel.Children.Add(content);
			return new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, Padding = new Thickness(8), Child = panel, CornerRadius = new CornerRadius(3) };
		}

		// Constructor
		public NodesForm(NodesViewerMode mode)
		{
			this.mode = mode;
			BuildView();

			// Counts
			numsegs.Text = mode.Segs.Length.ToString();
			numsplits.Text = mode.Nodes.Length.ToString();
			numssectors.Text = mode.Subsectors.Length.ToString();
			numvertices.Text = mode.Vertices.Length.ToString();
			showsegsvertices.Content = "Show additional vertices (" + (mode.Vertices.Length - General.Map.Map.Vertices.Count) + " seg splits)";

			// Create stats on the tree
			List<int> leafdepths = new List<int>();
			DiveTree(mode.Nodes.Length - 1, 0, leafdepths);
			int maxdepth = 0, mindepth = int.MaxValue;
			foreach(int d in leafdepths)
			{
				if(d < mindepth) mindepth = d;
				if(d > maxdepth) maxdepth = d;
			}

			treedepth.Text = maxdepth.ToString();

			// Calculate the tree balance. The balance is 100% when all leafs are equal depth and
			// 0% when 1 leaf equals peakdepth and the others are at level 1.
			int balance = (int)(((float)mindepth / maxdepth) * 100f);
			treebalance.Text = balance + "%";

			// Start viewing root split
			splitindex.Maximum = mode.Nodes.Length - 1;
			splitindex.Value = mode.Nodes.Length - 1;
			splitindex_ValueChanged(null, EventArgs.Empty);

			// Viewing subsector
			ssectorindex.Maximum = mode.Subsectors.Length - 1;
			ssectorindex.Value = 0;
			ssectorindex_ValueChanged(null, EventArgs.Empty);
			segindex_ValueChanged(null, EventArgs.Empty);

			// The events are only wired now: filling in the values above is not a change of what is viewed
			splitindex.ValueChanged += splitindex_ValueChanged;
			ssectorindex.ValueChanged += ssectorindex_ValueChanged;
			segindex.ValueChanged += segindex_ValueChanged;
		}

		private void BuildView()
		{
			// Overview
			var overview = new StackPanel { Spacing = 8, Margin = new Thickness(10) };
			overview.Children.Add(Rows(("Subsectors:", numssectors), ("Splits:", numsplits), ("Segs:", numsegs), ("Vertices:", numvertices), ("Depth:", treedepth), ("Balance:", treebalance)));
			overview.Children.Add(showsegsvertices);
			overview.Children.Add(buildnodesbutton);
			overview.Children.Add(new TextBlock { Text = "NOTE: This will rebuild the nodes using the settings configured for \"testing\" in the map configuration, and restart this mode.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.7 });
			taboverview.Content = overview;

			// Splits
			var splits = new StackPanel { Spacing = 8, Margin = new Thickness(10) };
			var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			top.Children.Add(new TextBlock { Text = "Split:", VerticalAlignment = VerticalAlignment.Center });
			top.Children.Add(splitindex);
			top.Children.Add(rootbutton);
			splits.Children.Add(top);
			var parent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			parent.Children.Add(new TextBlock { Text = "Parent split:", VerticalAlignment = VerticalAlignment.Center });
			parent.Children.Add(parentsplit);
			parent.Children.Add(parentbutton);
			splits.Children.Add(parent);
			var left = new StackPanel { Spacing = 4 };
			left.Children.Add(Rows(("Area:", leftarea)));
			var leftline = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			leftline.Children.Add(lefttype);
			leftline.Children.Add(leftindex);
			left.Children.Add(leftline);
			left.Children.Add(leftbutton);
			splits.Children.Add(Box("Left", left));
			var right = new StackPanel { Spacing = 4 };
			right.Children.Add(Rows(("Area:", rightarea)));
			var rightline = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			rightline.Children.Add(righttype);
			rightline.Children.Add(rightindex);
			right.Children.Add(rightline);
			right.Children.Add(rightbutton);
			splits.Children.Add(Box("Right", right));
			tabsplits.Content = splits;

			// Subsectors
			var subsectors = new StackPanel { Spacing = 8, Margin = new Thickness(10) };
			var sstop = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			sstop.Children.Add(new TextBlock { Text = "Subsector:", VerticalAlignment = VerticalAlignment.Center });
			sstop.Children.Add(ssectorindex);
			sstop.Children.Add(ssparentbutton);
			subsectors.Children.Add(sstop);
			subsectors.Children.Add(Rows(("Number of segs:", ssectornumsegs)));
			var seg = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
			seg.Children.Add(viewsegbox);
			seg.Children.Add(segindex);
			subsectors.Children.Add(seg);
			subsectors.Children.Add(Box("Seg", Rows(("Linedef:", lineindex), ("Start vertex:", startvertex), ("End vertex:", endvertex), ("Angle:", segangle), ("Side:", segside), ("Offset:", segoffset), ("Sidedef:", sideindex), ("Sector:", sectorindex))));
			tabsubsectors.Content = subsectors;

			tabs.Items.Add(taboverview);
			tabs.Items.Add(tabsplits);
			tabs.Items.Add(tabsubsectors);
			var close = new AvButton { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, Margin = new Thickness(10) };
			close.Click += (s, e) => window.Close();
			var layout = new DockPanel();
			DockPanel.SetDock(close, Dock.Bottom);
			layout.Children.Add(close);
			layout.Children.Add(tabs);
			window.Content = layout;

			window.Closing += NodesForm_FormClosing;
			tabs.SelectionChanged += (s, e) => { if(e.Source == tabs) General.Interface.RedrawDisplay(); };
			showsegsvertices.IsCheckedChanged += (s, e) => General.Interface.RedrawDisplay();
			viewsegbox.IsCheckedChanged += (s, e) => General.Interface.RedrawDisplay();
			buildnodesbutton.Click += buildnodesbutton_Click;
			rootbutton.Click += rootbutton_Click;
			parentbutton.Click += parentbutton_Click;
			leftbutton.Click += leftbutton_Click;
			rightbutton.Click += rightbutton_Click;
			ssparentbutton.Click += ssparentsplit_Click;
		}

		public void Dispose()
		{
			closing = true;           // the mode is the one that ends: closing the window must not cancel it again
			window.Close();
		}

		#endregion

		#region ================== Methods

		// This calculates the tree depth recursively
		private void DiveTree(int node, int level, List<int> leafdepths)
		{
			level++;

			// Process left side
			if(!mode.Nodes[node].leftsubsector)
				DiveTree(mode.Nodes[node].leftchild, level, leafdepths);
			else
				leafdepths.Add(level);

			// Process right side
			if(!mode.Nodes[node].rightsubsector)
				DiveTree(mode.Nodes[node].rightchild, level, leafdepths);
			else
				leafdepths.Add(level);
		}

		// Show this form
		public void Show(IWin32Window owner)
		{
			Window main = DialogHost.Owner == null ? null : DialogHost.Owner();

			// Position at left-top of owner
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			if(main != null) window.Position = new PixelPoint(main.Position.X + 20, main.Position.Y + 90);

			// Show window
			if(main != null) window.Show(main); else window.Show();
		}

		// Switch to a subsector
		public void ShowSubsector(int ssindex)
		{
			ssectorindex.Value = ssindex;
			tabs.SelectedItem = tabsubsectors;
		}

		#endregion

		#region ================== Form / Overview Events

		// Exit mode when dialog is closed
		private void NodesForm_FormClosing(object sender, WindowClosingEventArgs e)
		{
			if(!closing) General.Editing.CancelMode();
		}

		// (Re)build the nodes
		private void buildnodesbutton_Click(object sender, EventArgs e)
		{
			General.Map.RebuildNodes(General.Map.ConfigSettings.NodebuilderSave, true);
			bool showvertices = this.ShowSegsVertices;
			PixelPoint position = this.Location;

			// Restart the mode so that the new structures are loaded in.
			// This will automatically close and re-open this window.
			General.Editing.CancelMode();
			NodesViewerMode newmode = new NodesViewerMode();
			General.Editing.ChangeMode(newmode);

			// If something went wrong while engaging the mode (for example an unsupported node format was detected)
			// the mode will be disposed, so we need to check for it here
			if(!newmode.IsDisposed)
			{
				newmode.Form.ShowSegsVertices = showvertices;
				newmode.Form.Location = position; //mxd
			}
		}

		#endregion

		#region ================== Splits Events

		// Go to the root split
		private void rootbutton_Click(object sender, EventArgs e)
		{
			splitindex.Value = splitindex.Maximum;
		}

		// Go to the parent split
		private void parentbutton_Click(object sender, EventArgs e)
		{
			Node n = mode.Nodes[ViewSplitIndex];
			if(n.parent > -1) splitindex.Value = n.parent;
		}

		// Go to the left split/subsector
		private void leftbutton_Click(object sender, EventArgs e)
		{
			Node n = mode.Nodes[ViewSplitIndex];
			if(n.leftsubsector)
				ShowSubsector(n.leftchild);
			else
				splitindex.Value = n.leftchild;
		}

		// Go to the right split/subsector
		private void rightbutton_Click(object sender, EventArgs e)
		{
			Node n = mode.Nodes[ViewSplitIndex];
			if(n.rightsubsector)
				ShowSubsector(n.rightchild);
			else
				splitindex.Value = n.rightchild;
		}

		// Split changes
		private void splitindex_ValueChanged(object sender, EventArgs e)
		{
			Node n = mode.Nodes[ViewSplitIndex];
			if(n.parent == -1)
			{
				parentsplit.Text = "(root split)";
				parentsplit.IsEnabled = false;
				parentbutton.IsEnabled = false;
			}
			else
			{
				parentsplit.Text = n.parent.ToString();
				parentsplit.IsEnabled = true;
				parentbutton.IsEnabled = true;
			}

			leftarea.Text = "(" + n.leftbox.Left + ", " + n.leftbox.Top + ") - (" + n.leftbox.Right + ", " + n.leftbox.Bottom + ")";
			rightarea.Text = "(" + n.rightbox.Left + ", " + n.rightbox.Top + ") - (" + n.rightbox.Right + ", " + n.rightbox.Bottom + ")";
			leftindex.Text = n.leftchild.ToString();
			rightindex.Text = n.rightchild.ToString();

			if(n.leftsubsector)
			{
				lefttype.Text = "Subsector:";
				leftbutton.Content = "Go to subsector";
			}
			else
			{
				lefttype.Text = "Split:";
				leftbutton.Content = "Go to split";
			}

			if(n.rightsubsector)
			{
				righttype.Text = "Subsector:";
				rightbutton.Content = "Go to subsector";
			}
			else
			{
				righttype.Text = "Split:";
				rightbutton.Content = "Go to split";
			}

			General.Interface.RedrawDisplay();
		}

		#endregion

		#region ================== Subsectors Events

		// Go to parent split for this subsector
		private void ssparentsplit_Click(object sender, EventArgs e)
		{
			// Find parent split
			int ss = ViewSubsectorIndex;
			int parentsplit = -1;
			for(int i = 0; i < mode.Nodes.Length; i++)
			{
				Node n = mode.Nodes[i];
				if((n.leftsubsector && (n.leftchild == ss)) || (n.rightsubsector && (n.rightchild == ss)))
				{
					parentsplit = i;
					break;
				}
			}
			if(parentsplit == -1) return;
			splitindex.Value = parentsplit;
			tabs.SelectedItem = tabsplits;
		}

		// Subsector changes
		private void ssectorindex_ValueChanged(object sender, EventArgs e)
		{
			Subsector s = mode.Subsectors[ViewSubsectorIndex];

			ssectornumsegs.Text = s.numsegs.ToString();
			segindex.Minimum = s.firstseg;
			segindex.Maximum = s.firstseg + s.numsegs - 1;
			segindex.Value = s.firstseg;

			General.Interface.RedrawDisplay();
		}

		// Segment changes
		private void segindex_ValueChanged(object sender, EventArgs e)
		{
			Seg sg = mode.Segs[(int)(segindex.Value ?? 0)];
			Linedef ld = null; //mxd
			if(sg.lineindex != -1) ld = General.Map.Map.GetLinedefByIndex(sg.lineindex); //mxd

			lineindex.Text = sg.lineindex.ToString();
			startvertex.Text = sg.startvertex + "  (" + mode.Vertices[sg.startvertex].x + ", " + mode.Vertices[sg.startvertex].y + ")";
			endvertex.Text = sg.endvertex + "  (" + mode.Vertices[sg.endvertex].x + ", " + mode.Vertices[sg.endvertex].y + ")";
			segside.Text = sg.leftside ? "Back" : "Front";
			segangle.Text = Angle2D.RealToDoom(sg.angle) + "°";
			segoffset.Text = sg.offset + " mp";

			if(ld != null) //mxd
			{
				sideindex.Text = sg.leftside ? ld.Back.Index.ToString() : ld.Front.Index.ToString();
				sectorindex.Text = sg.leftside ? ld.Back.Sector.Index.ToString() : ld.Front.Sector.Index.ToString();
			}
			else
			{
				sideindex.Text = "None";
				sectorindex.Text = "None";
			}

			General.Interface.RedrawDisplay();
		}

		#endregion
	}
}
