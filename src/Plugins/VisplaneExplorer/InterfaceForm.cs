#region === Copyright (c) 2010 Pascal van der Heiden ===

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Collections.Generic;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Windows;

#endregion

namespace CodeImp.DoomBuilder.Plugins.VisplaneExplorer
{
	public class InterfaceForm : IDisposable
	{
		#region ================== Constants

		#endregion

		#region ================== mxd. Event handlers

		public event EventHandler OnVisplaneSettingsChanged;

		#endregion

		#region ================== Variables

		// The toolbar items (UDB's designer made them)
		private readonly ToolStripDropDownButton statsbutton = new ToolStripDropDownButton();
		private readonly ToolStripSeparator separator = new ToolStripSeparator();
		private readonly ToolStripCheckBox cbopendoors = new ToolStripCheckBox();
		private readonly ToolStripCheckBox cbheatmap = new ToolStripCheckBox();
		private readonly ToolStripDropDownButton heightbutton = new ToolStripDropDownButton();
		private readonly ToolStripMenuItem heightcustomitem = new ToolStripMenuItem();
		private readonly ToolStripMenuItem heightcustomadd = new ToolStripMenuItem();
		private readonly Windows.SetCustomHeightDialog customheightdialog = new Windows.SetCustomHeightDialog();

		private ViewStats viewstats;
		private Point oldttposition;
		private int viewheight;
		private int viewheightcustom;
		private int viewheightdefault;

		#endregion

		#region ================== Properties

		internal ViewStats ViewStats { get { return viewstats; } }
		internal bool OpenDoors { get { return cbopendoors.Checked; } }
		// For the tests
		internal ToolStripDropDownButton StatsButton { get { return statsbutton; } }
		internal ToolStripDropDownButton HeightButton { get { return heightbutton; } }
		internal ToolStripCheckBox OpenDoorsBox { get { return cbopendoors; } }
		internal ToolStripCheckBox HeatmapBox { get { return cbheatmap; } }
		internal ToolStripMenuItem CustomHeightItem { get { return heightcustomitem; } }
		internal ToolStripMenuItem CustomHeightAdd { get { return heightcustomadd; } }
		internal Windows.SetCustomHeightDialog CustomHeightDialog { get { return customheightdialog; } } //mxd
		internal bool ShowHeatmap { get { return cbheatmap.Checked; } } //mxd
		internal int ViewHeight { get { return viewheight; } }
		internal int ViewHeightDefault { get { return viewheightdefault; } }

		#endregion

		#region ================== Constructor / Destructor

		// Constructor
		public InterfaceForm()
		{
			viewheightdefault = General.Map.Config.VisplaneViewHeightDefault;
			BuildItems();
			cbopendoors.Checked = General.Settings.ReadPluginSetting("opendoors", false); //mxd
			cbheatmap.Checked = General.Settings.ReadPluginSetting("showheatmap", false); //mxd
			viewheight = General.Settings.ReadPluginSetting("viewheight", viewheightdefault);
			viewheightcustom = General.Settings.ReadPluginSetting("viewheightcustom", 0);

			RedrawViewHeightMenuItems();
		}

		#endregion

		#region ================== Methods

		public void Dispose() { }

		// The statistics and view height menus, as the designer made them
		private void BuildItems()
		{
			statsbutton.DisplayStyle = ToolStripItemDisplayStyle.Image;
			statsbutton.Image = Properties.Resources.Visplanes;
			statsbutton.ToolTipText = "Statistics to view";
			var stats = new[]
			{
				new { Name = "Visplanes", Image = Properties.Resources.Visplanes, Tag = "0" },
				new { Name = "Drawsegs", Image = Properties.Resources.Drawsegs, Tag = "1" },
				new { Name = "Solidsegs", Image = Properties.Resources.Solidsegs, Tag = "2" },
				new { Name = "Openings", Image = Properties.Resources.Openings, Tag = "3" },
			};
			foreach(var stat in stats)
			{
				var item = new ToolStripMenuItem { Text = stat.Name, Image = stat.Image, Tag = stat.Tag, DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Checked = (stat.Tag == "0") };
				item.Click += stats_Click;
				statsbutton.DropDownItems.Add(item);
			}

			cbopendoors.Text = "Open Doors";
			cbopendoors.CheckedChanged += cbopendoors_Click;
			cbheatmap.Text = "Heat Colors";
			cbheatmap.CheckedChanged += cbheatmap_Click;

			heightbutton.DisplayStyle = ToolStripItemDisplayStyle.Text;
			heightbutton.Text = "View Height";
			heightbutton.ToolTipText = "Position above the floor to calculate stats";
			foreach(KeyValuePair<string, string> viewheight in General.Map.Config.VisplaneViewHeights)
			{
				var heightitem = new ToolStripMenuItem
				{
					Tag = viewheight.Key,
					Text = viewheight.Key + " - " + viewheight.Value + (viewheight.Key == viewheightdefault.ToString() ? " (default)" : ""),
					DisplayStyle = ToolStripItemDisplayStyle.Text,
				};
				heightitem.Click += viewheight_Click;
				heightbutton.DropDownItems.Add(heightitem);
			}

			heightcustomitem.Tag = "0";
			heightcustomitem.Text = "0 - Custom";
			heightcustomitem.Visible = false;
			heightcustomitem.DisplayStyle = ToolStripItemDisplayStyle.Text;
			heightcustomitem.Click += viewheight_Click;
			heightbutton.DropDownItems.Add(heightcustomitem);

			heightcustomadd.Image = Properties.Resources.Add;
			heightcustomadd.Tag = "-1";
			heightcustomadd.Text = "Set custom height";
			heightcustomadd.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
			heightcustomadd.Click += heightcustomadd_Click;
			heightbutton.DropDownItems.Add(heightcustomadd);
		}

		// This adds the buttons to the toolbar
		public void AddToInterface()
		{
			General.Interface.BeginToolbarUpdate(); //mxd
			General.Interface.AddButton(statsbutton);
			General.Interface.AddButton(separator); //mxd
			General.Interface.AddButton(cbopendoors); //mxd
			General.Interface.AddButton(cbheatmap); //mxd
			General.Interface.AddButton(heightbutton);
			General.Interface.EndToolbarUpdate(); //mxd
		}

		// This removes the buttons from the toolbar
		public void RemoveFromInterface()
		{
			General.Interface.BeginToolbarUpdate(); //mxd
			General.Interface.RemoveButton(heightbutton);
			General.Interface.RemoveButton(cbheatmap); //mxd
			General.Interface.RemoveButton(cbopendoors); //mxd
			General.Interface.RemoveButton(separator); //mxd
			General.Interface.RemoveButton(statsbutton);
			General.Interface.EndToolbarUpdate(); //mxd

			//mxd. Save settings
			General.Settings.WritePluginSetting("opendoors", cbopendoors.Checked);
			General.Settings.WritePluginSetting("showheatmap", cbheatmap.Checked);
			General.Settings.WritePluginSetting("viewheight", viewheight);
			General.Settings.WritePluginSetting("viewheightcustom", viewheightcustom);
		}

		// This shows a tooltip over the display
		public void ShowTooltip(string text, Point p)
		{
			if(oldttposition != p)
			{
				General.Interface.Display.ShowToolTip("", text, p.X, p.Y);
				oldttposition = p;
			}
		}

		// This hides the tooltip
		public void HideTooltip()
		{
			General.Interface.Display.HideToolTip();
			oldttposition = new Point(int.MinValue, int.MinValue);
		}

		#endregion

		#region ================== Events

		// Selecting a type of stats to view
		private void stats_Click(object sender, EventArgs e)
		{
			foreach(ToolStripItem i in statsbutton.DropDownItems)
				((ToolStripMenuItem)i).Checked = false;
			
			ToolStripMenuItem item = (ToolStripMenuItem)sender;
			viewstats = (ViewStats)int.Parse(item.Tag.ToString(), CultureInfo.InvariantCulture);
			item.Checked = true;
			statsbutton.Image = item.Image;

			General.Interface.RedrawDisplay();
		}

		//mxd
		private void cbheatmap_Click(object sender, EventArgs e)
		{
			General.Interface.RedrawDisplay();
		}

		//mxd
		private void cbopendoors_Click(object sender, EventArgs e)
		{
			if(OnVisplaneSettingsChanged != null) OnVisplaneSettingsChanged(this, EventArgs.Empty);
		}

		// Select the height above the floor the Visplane Explorer renderer draws from.
		private void viewheight_Click(object sender, EventArgs e)
		{
			foreach (ToolStripItem i in heightbutton.DropDownItems)
				if(i is ToolStripMenuItem) ((ToolStripMenuItem)i).Checked = false;

			ToolStripMenuItem item = (ToolStripMenuItem)sender;
			viewheight = int.Parse(item.Tag.ToString(), CultureInfo.InvariantCulture);
			item.Checked = true;

			RedrawViewHeightButtonText();

			General.Interface.RedrawDisplay();

			if(OnVisplaneSettingsChanged != null) OnVisplaneSettingsChanged(this, EventArgs.Empty);
		}

		// Prompt user to enter a custom height, saving to the menu dropdown.
		private void heightcustomadd_Click(object sender, EventArgs e)
		{
			customheightdialog.CustomHeight = viewheightcustom;

			if (customheightdialog.ShowDialog(General.Interface) != DialogResult.OK)
				return;

			int oldviewheight = viewheight;
			viewheightcustom = customheightdialog.CustomHeight;

			viewheight = viewheightcustom > 0 ? viewheightcustom : viewheightdefault;

			if (General.Map.Config.VisplaneViewHeights.ContainsKey(viewheightcustom.ToString()))
				viewheightcustom = 0;

			RedrawViewHeightMenuItems();

			if (oldviewheight != viewheight)
			{
				General.Interface.RedrawDisplay();

				if (OnVisplaneSettingsChanged != null) OnVisplaneSettingsChanged(this, EventArgs.Empty);
			}
		}

		private void RedrawViewHeightButtonText()
		{
			heightbutton.Text = "View Height (" + viewheight.ToString() + ")";
		}

		private void RedrawViewHeightMenuItems()
		{
			RedrawViewHeightButtonText();

			heightcustomitem.Tag = viewheightcustom.ToString();
			heightcustomitem.Visible = viewheightcustom > 0;
			heightcustomitem.Text = viewheightcustom.ToString() + " - Custom";

			foreach (ToolStripItem heightitem in heightbutton.DropDownItems)
				if(heightitem is ToolStripMenuItem && (string)heightitem.Tag != "-1")
					((ToolStripMenuItem)heightitem).Checked = viewheight == int.Parse((string)heightitem.Tag, CultureInfo.InvariantCulture);
		}

		#endregion
	}
}
