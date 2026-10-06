// "Tag Selected Range": gives the selected sectors, linedefs or things a run of tags. UDB's TagRangeForm as a modal Avalonia dialog; the
// logic (Setup, CreateRange) is the original's, only the controls are new. ShowDialog answers OK once the tags were applied.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Types;
using DoomBuilder.UI;

namespace CodeImp.DoomBuilder.TagRange
{
	public class TagRangeForm : IDisposable, IWin32Window
	{
		private UniversalType selectiontype;
		private int selectioncount;
		Dictionary<int, bool> usedtags;
		private List<int> initialtags; //mxd

		//mxd. Persistent settings
		private static int storedstep = 1;
		private static bool storedrelative;

		private string title = "Tag Selected Range";
		private SimpleDialog dialog;
		private readonly NumberBox rangestart = new NumberBox { MinWidth = 110 };
		private readonly NumberBox rangestep = new NumberBox { MinWidth = 110 };
		private readonly TextBlock endtaglabel = new TextBlock { Text = "0", VerticalAlignment = VerticalAlignment.Center };
		private readonly CheckBox relativemode = new CheckBox { Content = "Relative to existing tags" };
		private readonly CheckBox skipdoubletags = new CheckBox { Content = "Skip over already used tags", IsVisible = false };
		private readonly TextBlock bglabel = new TextBlock { TextWrapping = TextWrapping.Wrap, FontStyle = FontStyle.Italic, Opacity = 0.7 };
		private readonly TextBlock doubletagwarning = new TextBlock { Text = "Warning: The tag range contains already used tags.", TextWrapping = TextWrapping.Wrap, IsVisible = false, Foreground = new SolidColorBrush(Color.FromRgb(200, 40, 40)) };
		private readonly TextBlock outoftagswarning = new TextBlock { Text = "The range exceeds the maximum or minimum allowed tags and cannot be created.", TextWrapping = TextWrapping.Wrap, IsVisible = false, Foreground = new SolidColorBrush(Color.FromRgb(200, 40, 40)) };

		public IntPtr Handle { get { return IntPtr.Zero; } }
		public int SelectionCount { get { return selectioncount; } }

		// For the tests
		internal SimpleDialog Dialog { get { return dialog; } }
		internal NumberBox RangeStart { get { return rangestart; } }
		internal NumberBox RangeStep { get { return rangestep; } }
		internal CheckBox RelativeMode { get { return relativemode; } }
		internal CheckBox SkipDoubleTags { get { return skipdoubletags; } }
		internal TextBlock EndTagLabel { get { return endtaglabel; } }
		internal TextBlock OutOfTagsWarning { get { return outoftagswarning; } }
		internal TextBlock DoubleTagWarning { get { return doubletagwarning; } }
		internal string Title { get { return title; } }

		// Constructor
		public TagRangeForm()
		{
			rangestart.WhenTextChanged += (s, e) => UpdateChanges();
			rangestep.WhenTextChanged += (s, e) => UpdateChanges();
			skipdoubletags.IsCheckedChanged += (s, e) => UpdateChanges();
			relativemode.IsCheckedChanged += (s, e) => { rangestart.AllowNegative = (relativemode.IsChecked == true); UpdateChanges(); };

			var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"), RowSpacing = 8, ColumnSpacing = 10, MinWidth = 300 };
			grid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto");
			Place(grid, new TextBlock { Text = "Start Tag:", VerticalAlignment = VerticalAlignment.Center }, 0, 0);
			Place(grid, rangestart, 0, 1);
			Place(grid, new TextBlock { Text = "End Tag:", VerticalAlignment = VerticalAlignment.Center }, 0, 2);
			Place(grid, endtaglabel, 0, 3);
			Place(grid, new TextBlock { Text = "Increment:", VerticalAlignment = VerticalAlignment.Center }, 1, 0);
			Place(grid, rangestep, 1, 1);
			Place(grid, relativemode, 2, 1, 3);
			var notes = new Avalonia.Controls.Panel { MinHeight = 50 };
			notes.Children.Add(bglabel);
			notes.Children.Add(outoftagswarning);
			notes.Children.Add(doubletagwarning);
			Place(grid, notes, 3, 0, 4);
			Place(grid, skipdoubletags, 4, 1, 3);
			var panel = new StackPanel();
			panel.Children.Add(grid);
			content = panel;
		}

		private readonly Avalonia.Controls.Control content;

		private static void Place(Grid grid, Avalonia.Controls.Control control, int row, int column, int columnspan = 1)
		{
			Grid.SetRow(control, row);
			Grid.SetColumn(control, column);
			Grid.SetColumnSpan(control, columnspan);
			grid.Children.Add(control);
		}

		// This sets up the form
		public void Setup()
		{
			General.Map.Map.ClearAllMarks(false);
			
			switch(General.Editing.Mode.GetType().Name)
			{
				case "SectorsMode":
				{
					General.Map.Map.MarkSelectedSectors(true, true); //mxd
					selectiontype = UniversalType.SectorTag;
					ICollection<Sector> list = General.Map.Map.GetSelectedSectors(true);
					initialtags = new List<int>(list.Count); //mxd
					foreach(Sector element in list) initialtags.Add(element.Tag); //mxd
					selectioncount = list.Count;
					title = "Create tag range for " + selectioncount + (selectioncount > 1 ? " sectors" : " sector");
				}
				break;

				case "LinedefsMode":
				{
					General.Map.Map.MarkSelectedLinedefs(true, true); //mxd
					selectiontype = UniversalType.LinedefTag;
					ICollection<Linedef> list = General.Map.Map.GetSelectedLinedefs(true);
					initialtags = new List<int>(list.Count); //mxd
					foreach(Linedef element in list) initialtags.Add(element.Tag); //mxd
					selectioncount = list.Count;
					title = "Create tag range for " + selectioncount + (selectioncount > 1 ? " linedefs" : " linedef");
				}
				break;

				case "ThingsMode":
				{
					General.Map.Map.MarkSelectedThings(true, true); //mxd
					selectiontype = UniversalType.ThingTag;
					ICollection<Thing> list = General.Map.Map.GetSelectedThings(true);
					initialtags = new List<int>(list.Count); //mxd
					foreach(Thing element in list) initialtags.Add(element.Tag); //mxd
					selectioncount = list.Count;
					title = "Create tag range for " + selectioncount + (selectioncount > 1 ? " things" : " thing");
				}
				break;
			}
			
			// Find out which tags are used
			usedtags = new Dictionary<int, bool>();
			General.Map.Map.ForAllTags(TagHandler, false, usedtags);
			
			// Find the first unused tag to use as range start
			int starttag = General.Map.Map.GetNewTag();
			rangestart.Text = starttag.ToString();

			//mxd. Apply saved settings
			rangestep.Text = storedstep.ToString();
			relativemode.IsChecked = storedrelative;

			//mxd. Do useless stuff
			if(General.Random(0, 255) > 230) bglabel.Text = "Creating tag ranges is fun! ^_^";
		}
		
		// Handler for finding a new tag
		private static void TagHandler(MapElement element, bool actionargument, UniversalType type, ref int value, Dictionary<int, bool> usedtags)
		{
			if(value > 0) usedtags[value] = true; //mxd
		}
		
		// This creates a range
		private List<int> CreateRange(int starttag, int increment, bool relative, bool skipusedtags, out bool tagsused, out bool outoftags) 
		{
			List<int> newtags = new List<int>(selectioncount);
			outoftags = false;
			tagsused = false;

			//mxd. Get relative tag range
			if(relative) 
			{
				int addtag = 0; // biwa

				// Go for the number of tags we need
				for(int i = 0; i < selectioncount; i++) 
				{
					int newtag = initialtags[i] + starttag + addtag;

					if(newtag > General.Map.FormatInterface.MaxTag || newtag < General.Map.FormatInterface.MinTag) 
					{
						outoftags = true;
						return newtags;
					}

					if(skipusedtags) 
					{
						// Find next unused tag
						while(usedtags.ContainsKey(newtag)) 
						{
							if(newtag >= General.Map.FormatInterface.MaxTag || newtag <= General.Map.FormatInterface.MinTag) 
							{
								outoftags = true;
								return newtags;
							}

							newtag += increment; //mxd // biwa
							addtag += increment; // biwa
						}
					} 
					else 
					{
						tagsused |= usedtags.ContainsKey(newtag);
					}

					newtags.Add(newtag);
					addtag += increment;
				}
			} 
			else //mxd. Get absolute tag range
			{
				// Go for the number of tags we need
				for(int i = 0; i < selectioncount; i++) 
				{
					if(starttag > General.Map.FormatInterface.MaxTag || starttag < General.Map.FormatInterface.MinTag) 
					{
						outoftags = true;
						return newtags;
					}

					if(skipusedtags) 
					{
						// Find next unused tag
						while(usedtags.ContainsKey(starttag)) 
						{
							if(starttag >= General.Map.FormatInterface.MaxTag || starttag <= General.Map.FormatInterface.MinTag) 
							{
								outoftags = true;
								return newtags;
							}

							starttag += increment; //mxd
						}
					} 
					else 
					{
						tagsused |= usedtags.ContainsKey(starttag);
					}

					newtags.Add(starttag);
					starttag += increment; //mxd
				}
			}
			
			return newtags;
		}
		
		// This updates the calculated range
		private void UpdateChanges()
		{
			if(usedtags == null) return;   // not set up yet

			bool outoftags, tagsused;
			int starttag = rangestart.GetResult(0);
			int step = rangestep.GetResult(1); //mxd

			List<int> tags = CreateRange(starttag, step, (relativemode.IsChecked == true), (skipdoubletags.IsChecked == true), out tagsused, out outoftags); //mxd

			outoftagswarning.IsVisible = outoftags;
			if(dialog != null) dialog.OkButton.IsEnabled = !outoftags;
			doubletagwarning.IsVisible = tagsused && !outoftags;
			skipdoubletags.IsVisible = tagsused && !outoftags;
			bglabel.IsVisible = !outoftags && !(tagsused && !outoftags);

			if(tags.Count > 0) endtaglabel.Text = tags[tags.Count - 1].ToString();
		}

		// OK clicked: false keeps the dialog open
		private bool Apply()
		{
			bool outoftags, tagsused;
			int starttag = rangestart.GetResult(0);
			int step = rangestep.GetResult(1);

			//mxd
			List<int> tags = CreateRange(starttag, step, (relativemode.IsChecked == true), (skipdoubletags.IsChecked == true), out tagsused, out outoftags);

			if(outoftags)
			{
				General.ShowWarningMessage("The range exceeds the maximum allowed tags and cannot be created.", MessageBoxButtons.OK);
				return false;
			}

			// Apply tags!
			if(selectiontype == UniversalType.SectorTag)
			{
				General.Map.UndoRedo.CreateUndo("Set " + selectioncount + " sector tags");
				ICollection<Sector> list = General.Map.Map.GetSelectedSectors(true);
				int index = 0;
				foreach(Sector s in list) s.Tag = tags[index++];
			}
			else if(selectiontype == UniversalType.LinedefTag)
			{
				General.Map.UndoRedo.CreateUndo("Set " + selectioncount + " linedef tags");
				ICollection<Linedef> list = General.Map.Map.GetSelectedLinedefs(true);
				int index = 0;
				foreach(Linedef l in list) l.Tag = tags[index++];
			}
			else if(selectiontype == UniversalType.ThingTag)
			{
				General.Map.UndoRedo.CreateUndo("Set " + selectioncount + " thing tags");
				ICollection<Thing> list = General.Map.Map.GetSelectedThings(true);
				int index = 0;
				foreach(Thing t in list) t.Tag = tags[index++];
			}

			//mxd. Store settings
			storedstep = rangestep.GetResult(1);
			storedrelative = (relativemode.IsChecked == true);
			return true;
		}

		public DialogResult ShowDialog()
		{
			dialog = new SimpleDialog(title, content);
			dialog.Validate = Apply;
			UpdateChanges();
			return DialogHost.ShowModal(dialog) ? DialogResult.OK : DialogResult.Cancel;
		}

		public DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); }
		public void Dispose() { }
	}
}
