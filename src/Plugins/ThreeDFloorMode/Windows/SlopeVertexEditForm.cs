// "Edit Slope Vertex": position and sectors of one or several slope vertices. UDB's SlopeVertexEditForm as a modal Avalonia dialog with
// the same Setup and ShowDialog; "indeterminate" values (the vertices differ) are blank boxes and three-state check boxes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvCheckBox = Avalonia.Controls.CheckBox;
using Control = Avalonia.Controls.Control;

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
	public class SlopeVertexEditForm : IDisposable, IWin32Window
	{
		private List<SlopeVertex> vertices;
		private List<Sector> sectors;
		private string undodescription;
		private string title = "Edit Slope Vertex";
		private bool canaddsectors;
		private bool canremovesectors;

		private readonly NumberBox positionx = new NumberBox { AllowDecimal = true, AllowNegative = true, MinWidth = 110 };
		private readonly NumberBox positiony = new NumberBox { AllowDecimal = true, AllowNegative = true, MinWidth = 110 };
		private readonly NumberBox positionz = new NumberBox { AllowDecimal = true, AllowNegative = true, MinWidth = 110 };
		private readonly AvCheckBox reposition = new AvCheckBox { Content = "Reposition after dragging sectors", IsThreeState = true };
		private readonly AvCheckBox spline = new AvCheckBox { Content = "Spline", IsThreeState = true };
		private readonly AvCheckBox addselectedsectorsceiling = new AvCheckBox { Content = "Apply to selected sectors" };
		private readonly AvCheckBox removeselectedsectorsceiling = new AvCheckBox { Content = "Remove from selected sectors" };
		private readonly AvCheckBox addselectedsectorsfloor = new AvCheckBox { Content = "Apply to selected sectors" };
		private readonly AvCheckBox removeselectedsectorsfloor = new AvCheckBox { Content = "Remove from selected sectors" };
		private readonly StackPanel sectorlist = new StackPanel();
		private readonly List<KeyValuePair<Sector, AvCheckBox>> sectorboxes = new List<KeyValuePair<Sector, AvCheckBox>>();
		private SimpleDialog dialog;

		public IntPtr Handle { get { return IntPtr.Zero; } }

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal string Title { get { return title; } }
		internal NumberBox PositionX { get { return positionx; } }
		internal NumberBox PositionY { get { return positiony; } }
		internal NumberBox PositionZ { get { return positionz; } }
		internal AvCheckBox Reposition { get { return reposition; } }
		internal AvCheckBox Spline { get { return spline; } }
		internal AvCheckBox AddToCeiling { get { return addselectedsectorsceiling; } }
		internal AvCheckBox RemoveFromCeiling { get { return removeselectedsectorsceiling; } }
		internal AvCheckBox AddToFloor { get { return addselectedsectorsfloor; } }
		internal AvCheckBox RemoveFromFloor { get { return removeselectedsectorsfloor; } }
		internal IList<KeyValuePair<Sector, AvCheckBox>> SectorBoxes { get { return sectorboxes; } }

		public SlopeVertexEditForm()
		{
			// Adding and removing the selected sectors at the same time makes no sense: at most one of each pair is checked
			Exclusive(addselectedsectorsceiling, removeselectedsectorsceiling);
			Exclusive(addselectedsectorsfloor, removeselectedsectorsfloor);
		}

		private static void Exclusive(AvCheckBox a, AvCheckBox b)
		{
			a.IsCheckedChanged += (s, e) => { if(a.IsChecked == true) b.IsChecked = false; };
			b.IsCheckedChanged += (s, e) => { if(b.IsChecked == true) a.IsChecked = false; };
		}

		public void Setup(List<SlopeVertex> vertices)
		{
			this.vertices = vertices;

			SlopeVertex fv = vertices[0];
			SlopeVertexGroup fsvg = BuilderPlug.Me.GetSlopeVertexGroup(fv);
			sectors = new List<Sector>();

			undodescription = "Edit slope vertex";
			if(vertices.Count > 1)
				undodescription = "Edit " + vertices.Count + " slope vertices";

			positionx.Text = fv.Pos.x.ToString();
			positiony.Text = fv.Pos.y.ToString();
			positionz.Text = fv.Z.ToString();

			foreach(Sector s in fsvg.Sectors)
				if(!sectors.Contains(s))
					sectors.Add(s);

			reposition.IsChecked = fsvg.Reposition;
			spline.IsChecked = fsvg.Spline;

			canaddsectors = true;
			canremovesectors = true;

			if(vertices.Count > 1)
			{
				List<SlopeVertexGroup> listsvgs = new List<SlopeVertexGroup>();

				title = "Edit slope vertices (" + vertices.Count.ToString() + ")";

				foreach(SlopeVertex sv in vertices)
				{
					SlopeVertexGroup svg = BuilderPlug.Me.GetSlopeVertexGroup(sv);

					if(!listsvgs.Contains(svg))
						listsvgs.Add(svg);

					if(sv.Pos.x.ToString() != positionx.Text)
						positionx.Text = "";

					if(sv.Pos.y.ToString() != positiony.Text)
						positiony.Text = "";

					if(sv.Z.ToString() != positionz.Text)
						positionz.Text = "";

					if(svg.Reposition != (reposition.IsChecked == true))
						reposition.IsChecked = null;

					if(svg.Spline)
						spline.IsEnabled = true;

					if(svg.Spline != (spline.IsChecked == true))
						spline.IsChecked = null;

					foreach(Sector s in svg.Sectors)
						if(!sectors.Contains(s))
							sectors.Add(s);
				}

				if(listsvgs.Count > 2)
				{
					canaddsectors = false;
					canremovesectors = false;
				}
			}

			foreach(Sector s in sectors.OrderBy(x => x.Index))
			{
				var box = new AvCheckBox { Content = s.ToString() };
				sectorboxes.Add(new KeyValuePair<Sector, AvCheckBox>(s, box));
				sectorlist.Children.Add(box);
			}

			if(General.Map.Map.SelectedSectorsCount == 0)
			{
				addselectedsectorsceiling.IsEnabled = false;
				removeselectedsectorsceiling.IsEnabled = false;
				addselectedsectorsfloor.IsEnabled = false;
				removeselectedsectorsfloor.IsEnabled = false;
			}
			else
			{
				addselectedsectorsceiling.IsEnabled = canaddsectors;
				removeselectedsectorsceiling.IsEnabled = canremovesectors;
				addselectedsectorsfloor.IsEnabled = canaddsectors;
				removeselectedsectorsfloor.IsEnabled = canremovesectors;
			}
		}

		// OK: applies everything to the vertices and their groups
		internal bool Apply()
		{
			List<SlopeVertexGroup> groups = new List<SlopeVertexGroup>();

			// undodescription was set in the Setup method
			General.Map.UndoRedo.CreateUndo(undodescription);

			foreach(SlopeVertex sv in vertices)
			{
				SlopeVertexGroup svg = BuilderPlug.Me.GetSlopeVertexGroup(sv);

				double x = positionx.GetResultFloat(sv.Pos.x);
				double y = positiony.GetResultFloat(sv.Pos.y);

				sv.Pos = new Vector2D(x, y);
				sv.Z = positionz.GetResultFloat(sv.Z);

				if(!groups.Contains(svg))
					groups.Add(svg);
			}

			foreach(SlopeVertexGroup svg in groups)
			{
				if(reposition.IsChecked != null)
					svg.Reposition = reposition.IsChecked == true;

				if(spline.IsChecked != null && svg.Vertices.Count == 3)
					svg.Spline = spline.IsChecked == true;

				// Ceiling
				if(addselectedsectorsceiling.IsChecked == true)
					foreach(Sector s in General.Map.Map.GetSelectedSectors(true).ToList())
						svg.AddSector(s, PlaneType.Ceiling);

				if(removeselectedsectorsceiling.IsChecked == true)
					foreach(Sector s in General.Map.Map.GetSelectedSectors(true).ToList())
						if(svg.Sectors.Contains(s))
							svg.RemoveSector(s, PlaneType.Ceiling);

				// Floor
				if(addselectedsectorsfloor.IsChecked == true)
					foreach(Sector s in General.Map.Map.GetSelectedSectors(true).ToList())
						svg.AddSector(s, PlaneType.Floor);

				if(removeselectedsectorsfloor.IsChecked == true)
					foreach(Sector s in General.Map.Map.GetSelectedSectors(true).ToList())
						if(svg.Sectors.Contains(s))
							svg.RemoveSector(s, PlaneType.Floor);

				// The sectors checked in the list are removed from the group
				foreach(var pair in sectorboxes)
				{
					if(pair.Value.IsChecked != true) continue;
					if(svg.Sectors.Contains(pair.Key))
					{
						svg.RemoveSector(pair.Key, PlaneType.Floor);
						svg.RemoveSector(pair.Key, PlaneType.Ceiling);
					}
				}

				svg.ApplyToSectors();
			}

			BuilderPlug.Me.StoreSlopeVertexGroupsInSector();
			return true;
		}

		private Control Build()
		{
			var position = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
			void Row(int row, string label, Control box)
			{
				var text = new TextBlock { Text = label, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
				Grid.SetRow(text, row);
				Grid.SetRow(box, row);
				Grid.SetColumn(box, 1);
				box.Margin = new Thickness(0, 3);
				position.Children.Add(text);
				position.Children.Add(box);
			}
			Row(0, "X:", positionx);
			Row(1, "Y:", positiony);
			Row(2, "Z:", positionz);

			StackPanel Group(string header, params Control[] children)
			{
				var group = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
				group.Children.Add(new TextBlock { Text = header, FontWeight = Avalonia.Media.FontWeight.SemiBold });
				foreach(Control c in children) group.Children.Add(c);
				return group;
			}

			var panel = new StackPanel { MinWidth = 320 };
			panel.Children.Add(Group("Position", position));
			panel.Children.Add(reposition);
			panel.Children.Add(spline);
			panel.Children.Add(new Border { Height = 8 });
			panel.Children.Add(Group("Ceiling", addselectedsectorsceiling, removeselectedsectorsceiling));
			panel.Children.Add(Group("Floor", addselectedsectorsfloor, removeselectedsectorsfloor));
			panel.Children.Add(Group("Sectors to remove", new ScrollViewer { Content = sectorlist, MaxHeight = 140, MinHeight = 60 }));
			return panel;
		}

		public DialogResult ShowDialog()
		{
			dialog = new SimpleDialog(title, Build());
			dialog.Validate = Apply;
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
