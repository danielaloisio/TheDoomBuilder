// The toolbar buttons of the plugin (the slope tools and "Relocate control sectors") and the context menu of the slope mode. UDB's MenusForm
// built from the Core's toolbar item objects; the context menu is an Avalonia ContextMenu shown over the main window.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Avalonia.Controls;
using CodeImp.DoomBuilder.Controls;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvMenuItem = Avalonia.Controls.MenuItem;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class MenusForm
	{
		private readonly ToolStripButton floorslope;
		private readonly ToolStripButton ceilingslope;
		private readonly ToolStripButton floorandceilingslope;
		private readonly ToolStripButton updateslopes;
		private readonly ToolStripActionButton relocatecontrolsectors;
		private readonly AddSectorsMenu addsectorscontextmenu = new AddSectorsMenu();

		public ToolStripButton FloorSlope { get { return floorslope; } }
		public ToolStripButton CeilingSlope { get { return ceilingslope; } }
		public ToolStripButton FloorAndCeilingSlope { get { return floorandceilingslope; } }
		public ToolStripButton UpdateSlopes { get { return updateslopes; } }
		public ToolStripActionButton RelocateControlSectors { get { return relocatecontrolsectors; } }
		public AddSectorsMenu AddSectorsContextMenu { get { return addsectorscontextmenu; } }

		public MenusForm()
		{
			floorslope = new ToolStripButton { Image = Properties.Resources.Floor, Tag = "drawfloorslope", Text = "Apply drawn slope to floor" };
			floorslope.Click += floorslope_Click;
			ceilingslope = new ToolStripButton { Image = Properties.Resources.Ceiling, Tag = "drawceilingslope", Text = "Apply drawn slope to ceiling" };
			ceilingslope.Click += ceilingslope_Click;
			floorandceilingslope = new ToolStripButton { Image = Properties.Resources.FloorAndCeiling, Tag = "drawfloorandceilingslope", Text = "Apply drawn slope to floor and ceiling" };
			floorandceilingslope.Click += floorandceilingslope_Click;

			updateslopes = new ToolStripButton { Image = Properties.Resources.UpdateSlopes, Text = "Update slopes", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
			updateslopes.Click += (s, e) => BuilderPlug.Me.UpdateSlopes();

			relocatecontrolsectors = new ToolStripActionButton { Image = Properties.Resources.RelocateControlSectors, Tag = "relocate3dfloorcontrolsectors", Text = "Relocate control sectors", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
			relocatecontrolsectors.Click += (s, e) => General.Interface.InvokeTaggedAction(s, e);
		}

		public void UpdateToolTips()
		{
			relocatecontrolsectors.UpdateToolTip();
		}

		// A tool that is on already does nothing when clicked again
		private void floorslope_Click(object sender, EventArgs e)
		{
			if(floorslope.Checked) return;
			General.Interface.InvokeTaggedAction(sender, e);
		}

		private void ceilingslope_Click(object sender, EventArgs e)
		{
			if(ceilingslope.Checked) return;
			General.Interface.InvokeTaggedAction(sender, e);
		}

		private void floorandceilingslope_Click(object sender, EventArgs e)
		{
			if(floorandceilingslope.Checked) return;
			General.Interface.InvokeTaggedAction(sender, e);
		}
	}

	/// <summary>
	/// The context menu of the slope mode ("Add slope to floor/ceiling", "Remove slope from floor/ceiling") for the sectors in <see cref="Tag"/>.
	/// The mode is told when the menu closed without a choice (<see cref="SlopeMode.ContextMenuClosing"/>).
	/// </summary>
	public class AddSectorsMenu
	{
		private readonly ContextMenu menu = new ContextMenu();
		private readonly AvMenuItem addslopeceiling = new AvMenuItem { Header = "Add slope to ceiling" };
		private readonly AvMenuItem addslopefloor = new AvMenuItem { Header = "Add slope to floor" };
		private readonly AvMenuItem removeslopeceiling = new AvMenuItem { Header = "Remove slope from ceiling" };
		private readonly AvMenuItem removeslopefloor = new AvMenuItem { Header = "Remove slope from floor" };
		private bool itemclicked;

		/// <summary>The sectors the menu works on (a List of Sector).</summary>
		public object Tag { get; set; }

		// For the tests
		internal ContextMenu Menu { get { return menu; } }
		internal AvMenuItem AddToCeiling { get { return addslopeceiling; } }
		internal AvMenuItem AddToFloor { get { return addslopefloor; } }
		internal AvMenuItem RemoveFromCeiling { get { return removeslopeceiling; } }
		internal AvMenuItem RemoveFromFloor { get { return removeslopefloor; } }

		public AddSectorsMenu()
		{
			addslopeceiling.Click += (s, e) => { itemclicked = true; AddSlope(PlaneType.Ceiling); };
			addslopefloor.Click += (s, e) => { itemclicked = true; AddSlope(PlaneType.Floor); };
			removeslopeceiling.Click += (s, e) => { itemclicked = true; RemoveSlope(PlaneType.Ceiling); };
			removeslopefloor.Click += (s, e) => { itemclicked = true; RemoveSlope(PlaneType.Floor); };
			menu.Items.Add(addslopeceiling);
			menu.Items.Add(addslopefloor);
			menu.Items.Add(new Separator());
			menu.Items.Add(removeslopeceiling);
			menu.Items.Add(removeslopefloor);
			menu.Opening += (s, e) => UpdateEnabled();
			menu.Closed += (s, e) => OnClosed();
		}

		/// <summary>Disables adding when not exactly one slope vertex group is selected: sectors can only join one group.</summary>
		internal void UpdateEnabled()
		{
			List<SlopeVertexGroup> svgs = ((SlopeMode)General.Editing.Mode).GetSelectedSlopeVertexGroups();
			addslopefloor.IsEnabled = svgs.Count == 1;
			addslopeceiling.IsEnabled = svgs.Count == 1;
		}

		/// <summary>Shows the menu over the main window, where the pointer is.</summary>
		public void Show(System.Drawing.Point screenposition)
		{
			Window owner = DialogHost.Owner == null ? null : DialogHost.Owner();
			if(owner == null) return;
			itemclicked = false;
			UpdateEnabled();
			menu.Open(owner);
		}

		// Closing it by any other way than choosing an item tells the mode, which would otherwise take the click as its own
		private void OnClosed()
		{
			if(!itemclicked) ((SlopeMode)General.Editing.Mode).ContextMenuClosing = true;
		}

		internal void AddSlope(PlaneType plane)
		{
			List<SlopeVertexGroup> svgs = ((SlopeMode)General.Editing.Mode).GetSelectedSlopeVertexGroups();

			// Can only add sectors to one slope vertex group
			if(svgs.Count != 1) return;

			foreach(Sector s in (List<Sector>)Tag)
			{
				SlopeVertexGroup rsvg = BuilderPlug.Me.GetSlopeVertexGroup(s);

				if(rsvg != null)
					rsvg.RemoveSector(s, plane);

				svgs[0].AddSector(s, plane);
				BuilderPlug.Me.UpdateSlopes(s);
			}

			General.Interface.RedrawDisplay();
		}

		internal void RemoveSlope(PlaneType plane)
		{
			foreach(Sector s in (List<Sector>)Tag)
			{
				SlopeVertexGroup svg = BuilderPlug.Me.GetSlopeVertexGroup(s);

				if(svg != null)
					svg.RemoveSector(s, plane);
			}

			General.Interface.RedrawDisplay();
		}
	}
}
