// "Find and Replace": UDB's FindReplaceForm as a modeless Avalonia window. Same members as the original (Finder, Show, Hide,
// GetSelection), so FindReplaceMode and the finders work unchanged.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using DoomBuilder.UI;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.BuilderModes
{
	public class FindReplaceForm : IDisposable, System.Windows.Forms.IWin32Window
	{
		private FindReplaceMode mode;
		private FindReplaceType newfinder;
		private FindReplaceType finder;
		private readonly List<FindReplaceType> findtypeslist;
		private readonly List<FindReplaceObject> found = new List<FindReplaceObject>();    // what the list shows (the rows are their text)
		private bool suppressevents;

		private Window window;
		private readonly ComboBox searchtypes = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
		private readonly TextBlock labelfind = new TextBlock { Text = "Find:" };
		private readonly TextBox findinput = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
		private readonly AvButton browsefind = new AvButton { Content = "...", MinWidth = 32 };
		private readonly AvCheckBox doreplace = new AvCheckBox { Content = "Replace with:" };
		private readonly TextBox replaceinput = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false };
		private readonly AvButton browsereplace = new AvButton { Content = "...", MinWidth = 32, IsEnabled = false };
		private readonly AvCheckBox withinselection = new AvCheckBox { Content = "Within selection only" };
		private readonly AvButton findbutton = new AvButton { Content = "Find", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly StackPanel resultspanel = new StackPanel { Spacing = 6, IsVisible = false };
		private readonly TextBlock resultscount = new TextBlock();
		private readonly ListBox resultslist = new ListBox { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle, Height = 200 };
		private readonly AvButton editbutton = new AvButton { Content = "Edit Selection", IsEnabled = false };
		private readonly AvButton deletebutton = new AvButton { Content = "Delete Selection", IsEnabled = false, IsVisible = false };

		public IntPtr Handle { get { return IntPtr.Zero; } }

		internal FindReplaceType Finder { get { return finder; } }

		/// <summary>The window (null until it has been shown).</summary>
		public Window Window { get { return window; } }

		// For the tests
		internal ComboBox SearchTypes { get { return searchtypes; } }
		internal TextBox FindInput { get { return findinput; } }
		internal TextBox ReplaceInput { get { return replaceinput; } }
		internal AvCheckBox DoReplace { get { return doreplace; } }
		internal AvButton FindButton { get { return findbutton; } }
		internal ListBox ResultsList { get { return resultslist; } }
		internal string ResultsCount { get { return resultscount.Text; } }

		public FindReplaceForm()
		{
			Type[] findtypes = BuilderPlug.Me.FindClasses(typeof(FindReplaceType));
			findtypeslist = new List<FindReplaceType>(findtypes.Length);
			foreach(Type t in findtypes)
			{
				object[] attr = t.GetCustomAttributes(typeof(FindReplaceAttribute), true);
				if(attr.Length == 0) continue;

				try
				{
					findtypeslist.Add((FindReplaceType)Assembly.GetExecutingAssembly().CreateInstance(t.FullName, false, BindingFlags.Default, null, null, CultureInfo.CurrentCulture, new object[0]));
				}
				catch(TargetInvocationException ex)
				{
					General.ErrorLogger.Add(ErrorType.Error, "Failed to create class instance \"" + t.Name + "\"");
					General.WriteLogLine(ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
					throw;
				}
				catch(Exception ex)
				{
					General.ErrorLogger.Add(ErrorType.Error, "Failed to create class instance \"" + t.Name + "\"");
					General.WriteLogLine(ex.GetType().Name + ": " + ex.Message);
					throw;
				}
			}
		}

		#region ================== Window

		private void Build()
		{
			doreplace.IsCheckedChanged += (s, e) => OnReplaceChanged();
			searchtypes.SelectionChanged += (s, e) => OnSearchTypeChanged();
			browsefind.Click += (s, e) => { if(newfinder != null) findinput.Text = newfinder.Browse(findinput.Text); };
			browsereplace.Click += (s, e) => { if(newfinder != null) replaceinput.Text = newfinder.BrowseReplace(replaceinput.Text); };
			findinput.PropertyChanged += (s, e) => { if(e.Property == TextBox.TextProperty) findbutton.IsEnabled = !string.IsNullOrEmpty(findinput.Text); };
			findbutton.Click += (s, e) => Find();
			resultslist.SelectionChanged += (s, e) => OnResultSelectionChanged();
			resultslist.DoubleTapped += (s, e) => EditSelection();
			resultslist.PointerReleased += OnResultsPointerReleased;
			editbutton.Click += (s, e) => EditSelection();
			deletebutton.Click += (s, e) => DeleteSelection();

			var findrow = new DockPanel { LastChildFill = true };
			DockPanel.SetDock(browsefind, Dock.Right);
			findrow.Children.Add(browsefind);
			findrow.Children.Add(findinput);
			var replacerow = new DockPanel { LastChildFill = true };
			DockPanel.SetDock(browsereplace, Dock.Right);
			replacerow.Children.Add(browsereplace);
			replacerow.Children.Add(replaceinput);

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			buttons.Children.Add(editbutton);
			buttons.Children.Add(deletebutton);
			resultspanel.Children.Add(resultscount);
			resultspanel.Children.Add(resultslist);
			resultspanel.Children.Add(buttons);

			var close = new AvButton { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
			close.Click += (s, e) => RequestClose();

			var layout = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
			layout.Children.Add(new TextBlock { Text = "Search for:" });
			layout.Children.Add(searchtypes);
			layout.Children.Add(labelfind);
			layout.Children.Add(findrow);
			layout.Children.Add(doreplace);
			layout.Children.Add(replacerow);
			layout.Children.Add(withinselection);
			layout.Children.Add(findbutton);
			layout.Children.Add(resultspanel);
			layout.Children.Add(close);

			window = new Window
			{
				Title = "Find and Replace",
				Width = 420,
				SizeToContent = SizeToContent.Height,
				CanResize = false,
				ShowInTaskbar = false,
				WindowStartupLocation = WindowStartupLocation.Manual,
				Content = new ScrollViewer { Content = layout, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }
			};
			window.Closing += OnWindowClosing;
			window.KeyDown += (s, e) => { if(e.Key == Key.Escape) { RequestClose(); e.Handled = true; } };
		}

		/// <summary>Shows the window, near the top-left of the main window.</summary>
		public void Show(FindReplaceMode mode)
		{
			this.mode = mode;
			if(window == null) Build();

			Window owner = DialogHost.Owner == null ? null : DialogHost.Owner();
			if(owner != null) window.Position = new PixelPoint(owner.Position.X + 20, owner.Position.Y + 90);

			// Re-fill the search types list: some are only there for some map formats
			suppressevents = true;
			searchtypes.ItemsSource = findtypeslist.Where(t => t.DetermineVisiblity()).ToList();
			suppressevents = false;
			int index = searchtypes.Items.Cast<object>().ToList().IndexOf(newfinder);
			searchtypes.SelectedIndex = index >= 0 ? index : 0;
			OnSearchTypeChanged();

			resultspanel.IsVisible = false;
			if(!window.IsVisible)
			{
				if(owner != null) window.Show(owner);
				else window.Show();
			}
		}

		/// <summary>Hides the window and forgets the results.</summary>
		public void Hide()
		{
			if(window != null && window.IsVisible)
			{
				window.Closing -= OnWindowClosing;
				window.Hide();
				window.Closing += OnWindowClosing;
			}
			SetResults(new FindReplaceObject[0]);
		}

		public void Dispose()
		{
			if(window != null)
			{
				window.Closing -= OnWindowClosing;
				window.Close();
				window = null;
			}
		}

		// Closing it (the title bar or the Close button) leaves the mode, and the mode hides the window
		private void OnWindowClosing(object sender, WindowClosingEventArgs e)
		{
			e.Cancel = true;
			RequestClose();
		}

		private void RequestClose()
		{
			General.Interface.Focus();
			General.Editing.CancelMode();
		}

		#endregion

		#region ================== Events

		private void OnReplaceChanged()
		{
			findbutton.Content = doreplace.IsChecked == true ? "Replace" : "Find";
			replaceinput.IsEnabled = doreplace.IsChecked == true;
			browsereplace.IsEnabled = doreplace.IsChecked == true && newfinder != null && newfinder.Attributes.BrowseButton;
		}

		private void OnSearchTypeChanged()
		{
			if(suppressevents) return;
			newfinder = searchtypes.SelectedItem as FindReplaceType;
			if(newfinder == null) return;

			browsefind.IsEnabled = newfinder.Attributes.BrowseButton;
			browsereplace.IsEnabled = newfinder.Attributes.BrowseButton;
			if(!newfinder.CanReplace()) doreplace.IsChecked = false;
			OnReplaceChanged();
			doreplace.IsEnabled = newfinder.CanReplace();

			// The hint about what to type
			ToolTip.SetTip(labelfind, string.IsNullOrEmpty(newfinder.UsageHint) ? null : newfinder.UsageHint);
			labelfind.TextDecorations = string.IsNullOrEmpty(newfinder.UsageHint) ? null : Avalonia.Media.TextDecorations.Underline;
		}

		private void Find()
		{
			if(newfinder == null) return;

			suppressevents = true;
			General.Interface.HideInfo();
			finder = newfinder;
			deletebutton.IsVisible = finder.AllowDelete;

			FindReplaceObject[] list;
			if(doreplace.IsChecked == true)
			{
				General.Map.UndoRedo.CreateUndo("Replace " + searchtypes.SelectedItem);
				list = finder.Find(findinput.Text, withinselection.IsChecked == true, true, replaceinput.Text, false);
				resultscount.Text = list.Length + " items found and replaced.";

				// Withdraw the undo step if nothing was replaced
				if(list.Length < 1)
				{
					mode.Volatile = false;     // otherwise UndoManager.PerformUndo cancels the mode
					General.Map.UndoRedo.WithdrawUndo();
					mode.Volatile = true;
				}
			}
			else
			{
				list = finder.Find(findinput.Text, withinselection.IsChecked == true, false, string.Empty, false);
				resultscount.Text = list.Length + " items found.";
			}

			SetResults(list);
			General.Map.Map.ClearAllSelected();
			for(int i = 0; i < found.Count; i++) resultslist.Selection.Select(i);

			// Let the finder know about the selection
			finder.ObjectSelected(GetSelection());

			resultspanel.IsVisible = true;
			suppressevents = false;
			UpdateButtons();

			General.Map.Renderer2D.SetPresentation(finder.RenderPresentation);
			General.Interface.RedrawDisplay();
		}

		private void SetResults(FindReplaceObject[] list)
		{
			found.Clear();
			found.AddRange(list);
			bool before = suppressevents;
			suppressevents = true;
			resultslist.ItemsSource = found.Select(o => o.ToString()).ToList();
			suppressevents = before;
			UpdateButtons();
		}

		private void UpdateButtons()
		{
			bool any = GetSelection().Length > 0;
			editbutton.IsEnabled = any;
			deletebutton.IsEnabled = any;
		}

		private void OnResultSelectionChanged()
		{
			if(suppressevents || finder == null) return;
			finder.ObjectSelected(GetSelection());
			UpdateButtons();
			General.Interface.RedrawDisplay();
		}

		// Right-clicking a result edits the selected ones, like in UDB (the row is selected first when it was not)
		private void OnResultsPointerReleased(object sender, PointerReleasedEventArgs e)
		{
			if(e.InitialPressMouseButton != MouseButton.Right) return;
			EditSelection();
		}

		private void EditSelection()
		{
			FindReplaceObject[] items = GetSelection();
			if(items.Length == 0 || finder == null) return;

			suppressevents = true;
			finder.EditObjects(items);
			suppressevents = false;
			OnResultSelectionChanged();
			General.Interface.RedrawDisplay();
		}

		private void DeleteSelection()
		{
			FindReplaceObject[] items = GetSelection();
			if(items.Length == 0 || finder == null) return;

			suppressevents = true;
			finder.DeleteObjects(items);
			var remaining = found.Where(o => !items.Contains(o)).ToArray();
			SetResults(remaining);
			suppressevents = false;
			OnResultSelectionChanged();
			General.Interface.RedrawDisplay();
		}

		#endregion

		#region ================== Methods

		/// <summary>The results selected in the list.</summary>
		internal FindReplaceObject[] GetSelection()
		{
			var list = new List<FindReplaceObject>();
			if(resultslist.Selection != null)
				foreach(int i in resultslist.Selection.SelectedIndexes)
					if(i >= 0 && i < found.Count) list.Add(found[i]);
			return list.ToArray();
		}

		#endregion
	}
}
