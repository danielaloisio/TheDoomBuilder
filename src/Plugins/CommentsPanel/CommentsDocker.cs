// The "Comments" docker (UDMF only): every comment of the map, once, with the elements that carry it. UDB's CommentsDocker; the logic is the
// original's (comments gathered per kind of element, view / select / remove / edit), the DataGridView became a list of rows.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using DoomBuilder.UI;
using AvControl = Avalonia.Controls.Control;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvButton = Avalonia.Controls.Button;
using AvMenuItem = Avalonia.Controls.MenuItem;

namespace CodeImp.DoomBuilder.CommentsPanel
{
	public class CommentsDocker : System.Windows.Forms.Control
	{
		#region ================== Variables

		private readonly Dictionary<string, CommentInfo> v_comments = new Dictionary<string, CommentInfo>(StringComparer.Ordinal);
		private readonly Dictionary<string, CommentInfo> l_comments = new Dictionary<string, CommentInfo>(StringComparer.Ordinal);
		private readonly Dictionary<string, CommentInfo> s_comments = new Dictionary<string, CommentInfo>(StringComparer.Ordinal);
		private readonly Dictionary<string, CommentInfo> t_comments = new Dictionary<string, CommentInfo>(StringComparer.Ordinal);
		private bool preventupdate;

		private readonly Grid root = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
		private readonly ListBox grid = new ListBox { SelectionMode = SelectionMode.Single };
		private readonly AvCheckBox filtermode = new AvCheckBox { Content = "Comments from this mode only" };
		private readonly AvCheckBox clickselects = new AvCheckBox { Content = "Select on click" };
		private readonly TextBox addcommenttext = new TextBox();
		private readonly AvButton addcomment = new AvButton { Content = "Set Selection Comment", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly StackPanel addcommentgroup = new StackPanel { Spacing = 6, Margin = new Thickness(6), IsEnabled = false };
		private readonly ContextMenu contextmenu = new ContextMenu();
		private readonly DispatcherTimer updatetimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
		private readonly DispatcherTimer enabledtimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(333) };
		private Window parentwindow;
		private bool disposed;

		// For the tests
		internal ListBox List { get { return grid; } }
		internal AvCheckBox FilterMode { get { return filtermode; } }
		internal AvCheckBox ClickSelects { get { return clickselects; } }
		internal TextBox AddCommentText { get { return addcommenttext; } }
		internal AvButton AddCommentButton { get { return addcomment; } }
		internal StackPanel AddCommentGroup { get { return addcommentgroup; } }
		internal ContextMenu ContextMenu { get { return contextmenu; } }
		internal int CommentCount { get { return v_comments.Count + l_comments.Count + s_comments.Count + t_comments.Count; } }
		internal IEnumerable<CommentInfo> Comments
		{
			get
			{
				foreach(var d in new[] { v_comments, l_comments, s_comments, t_comments })
					foreach(CommentInfo c in d.Values) yield return c;
			}
		}

		#endregion

		#region ================== Constructor

		// Constructor
		public CommentsDocker()
		{
			filtermode.IsChecked = General.Settings.ReadPluginSetting("filtermode", false);
			clickselects.IsChecked = General.Settings.ReadPluginSetting("clickselects", false);

			filtermode.IsCheckedChanged += (s, e) => { UpdateList(); LoseFocus(); };
			clickselects.IsCheckedChanged += (s, e) => LoseFocus();
			addcomment.Click += addcomment_Click;
			addcommenttext.KeyDown += addcommenttext_KeyDown;
			contextmenu.Closed += (s, e) => LoseFocus();
			updatetimer.Tick += updatetimer_Tick;
			enabledtimer.Tick += enabledtimer_Tick;
			grid.LostFocus += (s, e) => preventupdate = false;

			addcommentgroup.Children.Add(addcommenttext);
			addcommentgroup.Children.Add(addcomment);
			var options = new StackPanel { Spacing = 4, Margin = new Thickness(6) };
			options.Children.Add(filtermode);
			options.Children.Add(clickselects);
			Grid.SetRow(grid, 0);
			Grid.SetRow(addcommentgroup, 1);
			Grid.SetRow(options, 2);
			root.Children.Add(grid);
			root.Children.Add(addcommentgroup);
			root.Children.Add(options);
			// The docker tabs only keep the selected panel in the visual tree: being attached means being shown (UDB's VisibleChanged)
			root.AttachedToVisualTree += (s, e) => { AttachWindow(); if(!disposed && General.Map != null) UpdateList(); };
			root.DetachedFromVisualTree += (s, e) => { DetachWindow(); };
			NativeControl = root;
		}

		// Disposer
		public void Dispose()
		{
			if(disposed) return;
			disposed = true;
			General.Settings.WritePluginSetting("filtermode", filtermode.IsChecked == true);
			General.Settings.WritePluginSetting("clickselects", clickselects.IsChecked == true);
			updatetimer.Stop();
			enabledtimer.Stop();
			DetachWindow();
		}

		#endregion

		#region ================== Methods

		// The main window being activated again gives a good idea when comments could have been changed
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

		// When attached to the docker
		public void Setup()
		{
			AttachWindow();
			grid.SelectedItem = null;
			updatetimer.Start();
			enabledtimer.Start();
		}

		// Before detached from the docker
		public void Terminate()
		{
			preventupdate = true; //mxd
			DetachWindow();
			updatetimer.Tick -= updatetimer_Tick; //mxd
			enabledtimer.Tick -= enabledtimer_Tick; //mxd
			updatetimer.Stop();
			enabledtimer.Stop();
		}

		// This sends the focus back to the display and removes grid selection
		private void LoseFocus()
		{
			preventupdate = false;
			grid.SelectedItem = null;
			General.Interface.FocusDisplay();
		}

		// This updates the list for a specific kind of group
		private void UpdateGroupList(Dictionary<string, CommentInfo> newcomments, Dictionary<string, CommentInfo> comments, System.Drawing.Image icon)
		{
			// Remove old comments
			List<CommentInfo> commentslist = new List<CommentInfo>(comments.Values);
			foreach(CommentInfo c in commentslist)
			{
				if(!newcomments.ContainsKey(c.Comment))
					comments.Remove(c.Comment);
			}

			// Update the list with comments
			foreach(KeyValuePair<string, CommentInfo> c in newcomments)
			{
				CommentInfo cc = c.Value;

				if(!comments.ContainsKey(c.Key))
				{
					// Create the row
					cc.Row = CreateRow(cc, icon);
					comments.Add(c.Key, cc);
				}
				else
				{
					cc = comments[c.Key];
					cc.ReplaceElements(c.Value);
				}
			}
		}

		// A row: the icon of the kind of element and the comment (wrapped)
		private AvControl CreateRow(CommentInfo info, System.Drawing.Image icon)
		{
			var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Background = Avalonia.Media.Brushes.Transparent, Tag = info };
			var picture = new Avalonia.Controls.Image { Source = ImageConvert.ToAvalonia(icon), Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(2, 4, 6, 0) };
			var text = new TextBlock { Text = info.Comment, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 2, 5) };
			Grid.SetColumn(text, 1);
			row.Children.Add(picture);
			row.Children.Add(text);
			row.PointerPressed += (s, e) => { preventupdate = true; };
			row.PointerReleased += (s, e) => RowReleased(info, e);
			return row;
		}

		// This sets the timer to update the list very soon
		public void UpdateListSoon()
		{
			updatetimer.Stop();
			updatetimer.Start();
		}

		// This finds all comments and updates the list
		public void UpdateList()
		{
			if(!preventupdate && General.Map.Map.IsSafeToAccess)
			{
				// Update vertices
				Dictionary<string, CommentInfo> newcomments = new Dictionary<string, CommentInfo>(StringComparer.Ordinal);
				if(filtermode.IsChecked != true || (General.Editing.Mode.GetType().Name == "VerticesMode"))
				{
					foreach(Vertex v in General.Map.Map.Vertices) AddComments(v, newcomments);
				}
				UpdateGroupList(newcomments, v_comments, Properties.Resources.VerticesMode);

				// Update linedefs/sidedefs
				newcomments.Clear();
				if(filtermode.IsChecked != true || (General.Editing.Mode.GetType().Name == "LinedefsMode"))
				{
					foreach(Linedef l in General.Map.Map.Linedefs) AddComments(l, newcomments);
					foreach(Sidedef sd in General.Map.Map.Sidedefs) AddComments(sd, newcomments);
				}
				UpdateGroupList(newcomments, l_comments, Properties.Resources.LinesMode);

				// Update sectors
				newcomments.Clear();
				if(filtermode.IsChecked != true || (General.Editing.Mode.GetType().Name == "SectorsMode"))
				{
					foreach(Sector s in General.Map.Map.Sectors) AddComments(s, newcomments);
				}
				UpdateGroupList(newcomments, s_comments, Properties.Resources.SectorsMode);

				// Update things
				newcomments.Clear();
				if(filtermode.IsChecked != true || (General.Editing.Mode.GetType().Name == "ThingsMode"))
				{
					foreach(Thing t in General.Map.Map.Things) AddComments(t, newcomments);
				}
				UpdateGroupList(newcomments, t_comments, Properties.Resources.ThingsMode);

				// Sort the list by comment
				List<CommentInfo> all = new List<CommentInfo>(Comments);
				all.Sort((c1, c2) => string.Compare(c1.Comment, c2.Comment, StringComparison.OrdinalIgnoreCase));
				List<AvControl> rows = new List<AvControl>(all.Count);
				foreach(CommentInfo c in all) rows.Add((AvControl)c.Row);
				grid.ItemsSource = rows;
				grid.SelectedItem = null;
			}
		}

		// This adds comments from a MapElement
		private static void AddComments(MapElement e, Dictionary<string, CommentInfo> comments)
		{
			if(e.Fields.ContainsKey("comment"))
			{
				string c = e.Fields["comment"].Value.ToString();
				if(comments.ContainsKey(c))
					comments[c].AddElement(e);
				else
					comments[c] = new CommentInfo(c, e);
			}
		}
		
		// This changes the view to see the objects of a comment
		private static void ViewComment(CommentInfo c)
		{
			List<Vector2D> points = new List<Vector2D>();
			RectangleF area = MapSet.CreateEmptyArea();
			
			// Add all points to a list
			foreach(MapElement obj in c.Elements)
			{
				if(obj is Vertex)
				{
					points.Add((obj as Vertex).Position);
				}
				else if(obj is Linedef)
				{
					points.Add((obj as Linedef).Start.Position);
					points.Add((obj as Linedef).End.Position);
				}
				else if(obj is Sidedef)
				{
					points.Add((obj as Sidedef).Line.Start.Position);
					points.Add((obj as Sidedef).Line.End.Position);
				}
				else if(obj is Sector)
				{
					Sector s = (obj as Sector);
					foreach(Sidedef sd in s.Sidedefs)
					{
						points.Add(sd.Line.Start.Position);
						points.Add(sd.Line.End.Position);
					}
				}
				else if(obj is Thing)
				{
					Thing t = (obj as Thing);
					Vector2D p = t.Position;
					points.Add(p);
					points.Add(p + new Vector2D(t.Size * 2.0f, t.Size * 2.0f));
					points.Add(p + new Vector2D(t.Size * 2.0f, -t.Size * 2.0f));
					points.Add(p + new Vector2D(-t.Size * 2.0f, t.Size * 2.0f));
					points.Add(p + new Vector2D(-t.Size * 2.0f, -t.Size * 2.0f));
				}
				else
				{
					General.Fail("Unknown object given to zoom in on.");
				}
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
		
		// This selects the elements in a comment
		private static void SelectComment(CommentInfo c, bool clear)
		{
			//string editmode = "";

			// Leave any volatile mode
			General.Editing.CancelVolatileMode();

			if(clear)
			{
				General.Map.Map.ClearAllSelected();

				if(c.Elements[0] is Thing)
					General.Editing.ChangeMode("ThingsMode");
				else if(c.Elements[0] is Vertex)
					General.Editing.ChangeMode("VerticesMode");
				else if((c.Elements[0] is Linedef) || (c.Elements[0] is Sidedef))
					General.Editing.ChangeMode("LinedefsMode");
				else if(c.Elements[0] is Sector)
					General.Editing.ChangeMode("SectorsMode");
			}
			else
			{
				if(!(c.Elements[0] is Thing))
				{
					// Sectors mode is a bitch because it deals with selections somewhat aggressively
					// so we have to switch to linedefs to make this work right
					if((General.Editing.Mode.GetType().Name == "VerticesMode") ||
					   (General.Editing.Mode.GetType().Name == "SectorsMode") ||
					   (General.Editing.Mode.GetType().Name == "MakeSectorMode"))
						General.Editing.ChangeMode("LinedefsMode");
				}
			}
			
			// Select the map elements
			foreach(MapElement obj in c.Elements)
			{
				if(obj is SelectableElement)
				{
					(obj as SelectableElement).Selected = true;
					
					if(obj is Sector)
					{
						foreach(Sidedef sd in (obj as Sector).Sidedefs)
							sd.Line.Selected = true;
					}
				}
			}

			General.Editing.Mode.UpdateSelectionInfo(); //mxd
			General.Interface.RedrawDisplay();
		}
		
		// This removes the comments from elements in a comment
		private void RemoveComment(CommentInfo c)
		{
			// Create undo
			if(c.Elements.Count == 1)
				General.Map.UndoRedo.CreateUndo("Remove comment");
			else
				General.Map.UndoRedo.CreateUndo("Remove " + c.Elements.Count + " comments");
			
			// Erase comment fields
			foreach(MapElement obj in c.Elements)
			{
				obj.Fields.BeforeFieldsChange();
				obj.Fields.Remove("comment");
			}
			
			UpdateList();
			General.Interface.RedrawDisplay();
		}
		

		#endregion

		#region ================== Events

		private void ParentForm_Activated(object sender, EventArgs e)
		{
			UpdateListSoon();
		}

		// Update regulary
		private void updatetimer_Tick(object sender, EventArgs e)
		{
			updatetimer.Stop();

			if(!BuilderPlug.Me.IsDockerActive())
				return;

			UpdateList();
		}

		// The name of what a comment is on, for the menu ("Edit Vertices...")
		private static string ObjectName(CommentInfo c)
		{
			if((c.Elements[0] is Vertex) && (c.Elements.Count > 1)) return "Vertices";
			if((c.Elements[0] is Vertex) && (c.Elements.Count == 1)) return "Vertex";
			if(((c.Elements[0] is Linedef) || (c.Elements[0] is Sidedef)) && (c.Elements.Count > 1)) return "Linedefs";
			if(((c.Elements[0] is Linedef) || (c.Elements[0] is Sidedef)) && (c.Elements.Count == 1)) return "Linedef";
			if((c.Elements[0] is Sector) && (c.Elements.Count > 1)) return "Sectors";
			if((c.Elements[0] is Sector) && (c.Elements.Count == 1)) return "Sector";
			if((c.Elements[0] is Thing) && (c.Elements.Count > 1)) return "Things";
			if((c.Elements[0] is Thing) && (c.Elements.Count == 1)) return "Thing";
			return "";
		}

		// Mouse released on a row
		private void RowReleased(CommentInfo c, PointerReleasedEventArgs e)
		{
			var point = e.GetCurrentPoint(null).Properties;
			if(point.PointerUpdateKind == PointerUpdateKind.RightButtonReleased)
			{
				grid.SelectedItem = c.Row;
				ShowMenu(c, (AvControl)c.Row);
			}
			else
			{
				ClickComment(c);
			}
		}

		// The popup menu of a comment
		internal void ShowMenu(CommentInfo c, AvControl target)
		{
			var edit = new AvMenuItem { Header = "Edit " + ObjectName(c) + "..." };
			var select = new AvMenuItem { Header = "Select" };
			var selectadditive = new AvMenuItem { Header = "Select Additive" };
			var remove = new AvMenuItem { Header = "Remove Comment" };
			edit.Click += (s, e) => EditObjects(c);
			select.Click += (s, e) => SelectComment(c, true);
			selectadditive.Click += (s, e) => SelectComment(c, false);
			remove.Click += (s, e) => RemoveComment(c);
			contextmenu.ItemsSource = new AvControl[] { edit, new Separator(), select, selectadditive, new Separator(), remove };
			contextmenu.Tag = c;
			contextmenu.Open(target);
		}

		// A comment was clicked: view it, and maybe select it
		internal void ClickComment(CommentInfo c)
		{
			// View selected comment
			ViewComment(c);

			if(clickselects.IsChecked == true)
				SelectComment(c, true);

			LoseFocus();
		}

		// Edit objects
		internal void EditObjects(CommentInfo c)
		{
			if(c != null)
			{
				if(c.Elements[0] is Vertex)
				{
					List<Vertex> vertices = new List<Vertex>();
					foreach(MapElement m in c.Elements) vertices.Add((Vertex)m);
					General.Interface.ShowEditVertices(vertices);
				}
				else if((c.Elements[0] is Linedef) || (c.Elements[0] is Sidedef))
				{
					List<Linedef> linedefs = new List<Linedef>();
					foreach(MapElement m in c.Elements)
					{
						if(m is Sidedef)
							linedefs.Add((m as Sidedef).Line);
						else
							linedefs.Add((Linedef)m);
					}
					General.Interface.ShowEditLinedefs(linedefs);
				}
				else if(c.Elements[0] is Sector)
				{
					List<Sector> sectors = new List<Sector>();
					foreach(MapElement m in c.Elements) sectors.Add((Sector)m);
					General.Interface.ShowEditSectors(sectors);
				}
				else if(c.Elements[0] is Thing)
				{
					List<Thing> things = new List<Thing>();
					foreach(MapElement m in c.Elements) things.Add((Thing)m);
					General.Interface.ShowEditThings(things);
				}

				General.Map.Map.Update();
				UpdateList();
				LoseFocus();
				General.Interface.RedrawDisplay();
			}
		}

		// This sets the comment on the current selection
		private void addcomment_Click(object sender, EventArgs e)
		{
			List<MapElement> elements = new List<MapElement>();

			if(General.Editing.Mode.GetType().Name == "VerticesMode")
			{
				ICollection<Vertex> vs = General.Map.Map.GetSelectedVertices(true);
				foreach(Vertex v in vs) elements.Add(v);
			}

			if(General.Editing.Mode.GetType().Name == "LinedefsMode")
			{
				ICollection<Linedef> ls = General.Map.Map.GetSelectedLinedefs(true);
				foreach(Linedef l in ls) elements.Add(l);
			}

			if(General.Editing.Mode.GetType().Name == "SectorsMode")
			{
				ICollection<Sector> ss = General.Map.Map.GetSelectedSectors(true);
				foreach(Sector s in ss) elements.Add(s);
			}

			if(General.Editing.Mode.GetType().Name == "ThingsMode")
			{
				ICollection<Thing> ts = General.Map.Map.GetSelectedThings(true);
				foreach(Thing t in ts) elements.Add(t);
			}

			if(elements.Count > 0)
			{
				// Create undo
				if(elements.Count == 1)
					General.Map.UndoRedo.CreateUndo("Add comment");
				else
					General.Map.UndoRedo.CreateUndo("Add " + elements.Count + " comments");

				// Set comment on elements
				foreach(MapElement el in elements)
				{
					el.Fields.BeforeFieldsChange();
					el.Fields["comment"] = new UniValue((int)UniversalType.String, addcommenttext.Text ?? "");
				}
			}

			addcommenttext.Text = "";
			UpdateList();
			LoseFocus();
			General.Interface.RedrawDisplay();
		}

		// Text typed in add comment box
		private void addcommenttext_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
		{
			if((e.Key == Key.Enter) && (e.KeyModifiers == KeyModifiers.None) && addcomment.IsEnabled)
			{
				addcomment_Click(addcomment, EventArgs.Empty);
				e.Handled = true;
			}
		}

		// Check if the add comment box should be enabled
		internal void enabledtimer_Tick(object sender, EventArgs e)
		{
			if(General.Editing.Mode == null) return; //mxd
			switch(General.Editing.Mode.GetType().Name)
			{
				case "VerticesMode":
					addcommentgroup.IsEnabled = (General.Map.Map.SelectedVerticessCount > 0);
					break;
				case "LinedefsMode":
					addcommentgroup.IsEnabled = (General.Map.Map.SelectedLinedefsCount > 0);
					break;
				case "SectorsMode":
					addcommentgroup.IsEnabled = (General.Map.Map.SelectedSectorsCount > 0);
					break;
				case "ThingsMode":
					addcommentgroup.IsEnabled = (General.Map.Map.SelectedThingsCount > 0);
					break;
			}
		}

		#endregion
	}
}
