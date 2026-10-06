// "Map Analysis": UDB's ErrorCheckForm as a modeless Avalonia window. Same members as the original (SubmitResult, AddProgressValue,
// BlockMap, SelectedResults, Show, CloseWindow), so the error checkers and ErrorCheckMode work unchanged. The checkers run on their own
// threads and report back through the UI dispatcher.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using DoomBuilder.UI;
using AvCheckBox = Avalonia.Controls.CheckBox;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.BuilderModes
{
	public class ErrorCheckForm : IDisposable, System.Windows.Forms.IWin32Window
	{
		private const string INFO_NONE = "Select a result from the list to see more information.\nHold 'Ctrl' to select several results.\nHold 'Shift' to select a range of results.\nRight-click on a result to show context menu.";

		private volatile bool running;
		private Thread checksthread;
		private BlockMap<BlockEntry> blockmap;
		private List<ErrorResult> resultslist = new List<ErrorResult>();      // every result found
		private readonly List<ErrorResult> items = new List<ErrorResult>();   // the ones the list shows (the rows are their text)
		private readonly List<Type> hiddentresulttypes = new List<Type>();
		private bool batchselectioninprogress;
		private bool noerrors;

		private Window window;
		private readonly List<AvCheckBox> checks = new List<AvCheckBox>();
		private readonly AvCheckBox toggleall = new AvCheckBox { Content = "Toggle all" };
		private readonly AvButton buttoncheck = new AvButton { Content = "Start Analysis", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
		private readonly ProgressBar progress = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, Height = 14 };
		private readonly ListBox results = new ListBox { SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle, Height = 220 };
		private readonly TextBlock resultinfo = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 60 };
		private readonly AvButton fix1 = new AvButton { IsVisible = false };
		private readonly AvButton fix2 = new AvButton { IsVisible = false };
		private readonly AvButton fix3 = new AvButton { IsVisible = false };
		private readonly StackPanel resultspanel = new StackPanel { Spacing = 6, IsVisible = false };
		private readonly MenuItem resultshowall = new MenuItem { Header = "Show all results" };
		private readonly MenuItem resultselectcurrenttype = new MenuItem { Header = "Select all results of the current type" };
		private readonly MenuItem resultcopytoclipboard = new MenuItem { Header = "Copy results to clipboard" };
		private readonly MenuItem resulthidecurrent = new MenuItem { Header = "Hide selected results" };
		private readonly MenuItem resulthidecurrenttype = new MenuItem { Header = "Hide results of the selected type" };
		private readonly MenuItem resultshowonlycurrent = new MenuItem { Header = "Show only results of the selected type" };

		public IntPtr Handle { get { return IntPtr.Zero; } }

		/// <summary>The results selected in the list.</summary>
		public List<ErrorResult> SelectedResults
		{
			get
			{
				var indices = new List<int>();
				if(results.Selection != null) foreach(int i in results.Selection.SelectedIndexes) indices.Add(i);
				var list = new List<ErrorResult>();
				foreach(int i in indices) if(!noerrors && i >= 0 && i < items.Count) list.Add(items[i]);
				return list;
			}
		}

		public BlockMap<BlockEntry> BlockMap { get { return blockmap; } }

		/// <summary>True while the checkers are running.</summary>
		public bool IsRunning { get { return running; } }

		/// <summary>The window (null until it has been shown).</summary>
		public Window Window { get { return window; } }

		// For the tests
		internal ListBox ResultsList { get { return results; } }
		internal AvButton[] FixButtons { get { return new[] { fix1, fix2, fix3 }; } }
		internal IReadOnlyList<AvCheckBox> Checks { get { return checks; } }
		internal string InfoText { get { return resultinfo.Text; } }
		internal int ResultCount { get { return resultslist.Count; } }

		public ErrorCheckForm()
		{
			Type[] checkertypes = BuilderPlug.Me.FindClasses(typeof(ErrorChecker));
			var found = new List<KeyValuePair<string, AvCheckBox>>();
			foreach(Type t in checkertypes)
			{
				object[] attr = t.GetCustomAttributes(typeof(ErrorCheckerAttribute), true);
				if(attr.Length == 0) continue;

				ErrorChecker checker = CreateChecker(t);
				if(checker.SkipCheck) continue;

				ErrorCheckerAttribute checkerattr = (ErrorCheckerAttribute)attr[0];
				var box = new AvCheckBox { Content = checkerattr.DisplayName, Tag = t, Margin = new Thickness(0, 1) };
				box.IsChecked = General.Settings.ReadPluginSetting("errorchecks." + t.Name.ToLowerInvariant(), checkerattr.DefaultChecked);
				found.Add(new KeyValuePair<string, AvCheckBox>(checkerattr.DisplayName, box));
			}
			foreach(var pair in found.OrderBy(p => p.Key, StringComparer.CurrentCultureIgnoreCase)) checks.Add(pair.Value);
		}

		private static ErrorChecker CreateChecker(Type t)
		{
			try
			{
				return (ErrorChecker)Assembly.GetExecutingAssembly().CreateInstance(t.FullName, false, BindingFlags.Default, null, null, CultureInfo.CurrentCulture, new object[0]);
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

		#region ================== Window

		private void Build()
		{
			var checklist = new StackPanel();
			foreach(AvCheckBox box in checks) checklist.Children.Add(box);

			toggleall.IsCheckedChanged += (s, e) => { foreach(AvCheckBox cb in checks) cb.IsChecked = toggleall.IsChecked == true; };
			buttoncheck.Click += (s, e) => { if(running) checksthread.Interrupt(); else StartChecking(); };
			fix1.Click += (s, e) => Fix(1);
			fix2.Click += (s, e) => Fix(2);
			fix3.Click += (s, e) => Fix(3);
			results.SelectionChanged += (s, e) => OnSelectionChanged();
			results.ContextMenu = BuildMenu();
			resultinfo.Text = INFO_NONE;

			var fixes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
			fixes.Children.Add(fix1);
			fixes.Children.Add(fix2);
			fixes.Children.Add(fix3);
			resultspanel.Children.Add(new TextBlock { Text = "Results:" });
			resultspanel.Children.Add(results);
			resultspanel.Children.Add(resultinfo);
			resultspanel.Children.Add(fixes);

			var close = new AvButton { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
			close.Click += (s, e) => RequestClose();

			var layout = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
			layout.Children.Add(new TextBlock { Text = "Select the checks to run:" });
			layout.Children.Add(new Border { BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.Gray, Padding = new Thickness(6), Child = new ScrollViewer { MaxHeight = 280, Content = checklist } });
			layout.Children.Add(toggleall);
			layout.Children.Add(buttoncheck);
			layout.Children.Add(progress);
			layout.Children.Add(resultspanel);
			layout.Children.Add(close);

			window = new Window
			{
				Title = "Map Analysis",
				Width = 420,
				SizeToContent = SizeToContent.Height,
				CanResize = false,
				ShowInTaskbar = false,
				WindowStartupLocation = WindowStartupLocation.Manual,
				Content = new ScrollViewer { Content = layout, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }
			};
			window.Closing += OnWindowClosing;
			window.KeyDown += (s, e) => { if(e.Key == Avalonia.Input.Key.Escape) { RequestClose(); e.Handled = true; } };
		}

		private ContextMenu BuildMenu()
		{
			var menu = new ContextMenu();
			menu.Opening += (s, e) =>
			{
				bool haveresult = SelectedResults.Count > 0;
				resultshowall.IsEnabled = resultslist.Count > 0 && resultslist.Count > items.Count;
				resultselectcurrenttype.IsEnabled = haveresult;
				resultcopytoclipboard.IsEnabled = haveresult;
				resulthidecurrent.IsEnabled = haveresult;
				resulthidecurrenttype.IsEnabled = haveresult;
				resultshowonlycurrent.IsEnabled = haveresult;
			};
			resultshowall.Click += (s, e) => ShowAllResults();
			resultselectcurrenttype.Click += (s, e) => SelectCurrentType();
			resultcopytoclipboard.Click += (s, e) => System.Windows.Forms.Clipboard.SetText(string.Join(Environment.NewLine, SelectedResults.Select(r => r.ToString())));
			resulthidecurrent.Click += (s, e) => HideSelected();
			resulthidecurrenttype.Click += (s, e) => HideSelectedTypes();
			resultshowonlycurrent.Click += (s, e) => ShowOnlySelectedTypes();
			menu.Items.Add(resultshowall);
			menu.Items.Add(new Separator());
			menu.Items.Add(resultselectcurrenttype);
			menu.Items.Add(resultcopytoclipboard);
			menu.Items.Add(new Separator());
			menu.Items.Add(resulthidecurrent);
			menu.Items.Add(resulthidecurrenttype);
			menu.Items.Add(resultshowonlycurrent);
			return menu;
		}

		/// <summary>Shows the window, near the top-left of the main window.</summary>
		public void Show()
		{
			if(window == null) Build();
			window.Title = "Map Analysis";
			resultspanel.IsVisible = false;

			Window owner = DialogHost.Owner == null ? null : DialogHost.Owner();
			if(owner != null) window.Position = new PixelPoint(owner.Position.X + 20, owner.Position.Y + 90);
			if(!window.IsVisible)
			{
				if(owner != null) window.Show(owner);
				else window.Show();
			}
		}

		/// <summary>Stops the checking, forgets the results and hides the window.</summary>
		public void CloseWindow()
		{
			if(running) checksthread.Interrupt();
			ClearSelectedResult();

			resultslist.Clear();
			ResetList();

			foreach(AvCheckBox c in checks)
			{
				Type t = c.Tag as Type;
				if(t != null) General.Settings.WritePluginSetting("errorchecks." + t.Name.ToLowerInvariant(), c.IsChecked == true);
			}

			if(window != null && window.IsVisible)
			{
				window.Closing -= OnWindowClosing;
				window.Hide();
				window.Closing += OnWindowClosing;
			}
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

		#region ================== Results list

		private void ResetList()
		{
			items.Clear();
			noerrors = false;
			RefreshList();
		}

		private void RefreshList()
		{
			batchselectioninprogress = true;
			results.ItemsSource = noerrors ? new List<string> { "No errors found." } : items.Select(r => r.ToString()).ToList();
			batchselectioninprogress = false;
		}

		private void UpdateTitle()
		{
			if(window == null) return;
			int hiddencount = resultslist.Count - items.Count;
			string title = "Map Analysis [" + resultslist.Count + " results";
			if(hiddencount > 0) title += ", " + hiddencount + " hidden";
			title += ", " + SelectedResults.Count + " selected";
			window.Title = title + "]";
		}

		private void ClearSelectedResult()
		{
			batchselectioninprogress = true;
			results.Selection?.Clear();
			batchselectioninprogress = false;

			resultinfo.Text = (items.Count == 0 && resultslist.Count > 0) ? "All results are hidden. Use context menu to show them." : INFO_NONE;
			resultinfo.IsEnabled = false;
			fix1.IsVisible = false;
			fix2.IsVisible = false;
			fix3.IsVisible = false;

			UpdateTitle();
		}

		#endregion

		#region ================== Thread calls

		/// <summary>Called by the checkers, from their own threads.</summary>
		public void SubmitResult(ErrorResult result)
		{
			Dispatcher.UIThread.Post(() =>
			{
				if(!result.IsHidden && !hiddentresulttypes.Contains(result.GetType())) items.Add(result);
				resultslist.Add(result);
				RefreshList();
				UpdateTitle();
			});
		}

		/// <summary>Called by the checkers, from their own threads.</summary>
		public void AddProgressValue(int value)
		{
			Dispatcher.UIThread.Post(() => progress.Value += value);
		}

		#endregion

		#region ================== Checking

		/// <summary>Starts the analysis (nothing happens when it is running already).</summary>
		public void StartChecking()
		{
			if(running) return;

			RectangleF area = MapSet.CreateArea(General.Map.Map.Vertices);
			area = MapSet.IncreaseArea(area, General.Map.Map.Things);
			blockmap = new BlockMap<BlockEntry>(area);
			blockmap.AddLinedefsSet(General.Map.Map.Linedefs);
			blockmap.AddSectorsSet(General.Map.Map.Sectors);
			blockmap.AddThingsSet(General.Map.Map.Things);
			blockmap.AddVerticesSet(General.Map.Map.Vertices);

			resultspanel.IsVisible = true;
			progress.Value = 0;
			resultslist = new List<ErrorResult>();
			ResetList();
			results.IsEnabled = true;
			ClearSelectedResult();
			buttoncheck.Content = "Abort Analysis";
			General.Interface.RedrawDisplay();

			// The check boxes are read here, on the UI thread
			var types = checks.Where(c => c.IsChecked == true).Select(c => (Type)c.Tag).ToList();

			running = true;
			checksthread = new Thread(() => RunChecks(types));
			checksthread.Name = "Error Checking Management";
			checksthread.Priority = ThreadPriority.Normal;
			checksthread.Start();
		}

		// Only called from the checking management thread
		private void StopChecking()
		{
			Dispatcher.UIThread.Post(() =>
			{
				checksthread = null;
				progress.Value = 0;
				buttoncheck.Content = "Start Analysis";
				running = false;
				if(blockmap != null) blockmap.Dispose();
				blockmap = null;

				if(resultslist.Count == 0)
				{
					noerrors = true;
					RefreshList();
					results.IsEnabled = false;
					UpdateTitle();
				}
				else
				{
					ClearSelectedResult();
				}
			});
		}

		private void RunChecks(List<Type> types)
		{
			List<ErrorChecker> checkers = new List<ErrorChecker>();
			List<Thread> threads = new List<Thread>();
			int maxthreads = Environment.ProcessorCount;
			int totalprogress = 0;
			int nextchecker = 0;

			try
			{
				foreach(Type t in types)
				{
					ErrorChecker checker = CreateChecker(t);
					if(checker != null)
					{
						checkers.Add(checker);
						totalprogress += checker.TotalProgress;
					}
				}
			}
			catch(Exception)
			{
				StopChecking();
				throw;
			}

			checkers.Sort();
			Dispatcher.UIThread.Post(() => progress.Maximum = Math.Max(1, totalprogress));

			while((nextchecker < checkers.Count) || (threads.Count > 0))
			{
				while((threads.Count < maxthreads) && (nextchecker < checkers.Count))
				{
					ErrorChecker c = checkers[nextchecker++];
					Thread t = new Thread(c.Run);
					t.Name = "Error Checker '" + c.GetType().Name + "'";
					t.Priority = ThreadPriority.BelowNormal;
					t.Start();
					threads.Add(t);
				}

				for(int i = threads.Count - 1; i >= 0; i--)
					if(!threads[i].IsAlive) threads.RemoveAt(i);

				try { Thread.Sleep(1); }
				catch(ThreadInterruptedException) { break; }
			}

			// Aborted: stop the checkers that are still going
			foreach(Thread t in threads)
			{
				while(t.IsAlive)
				{
					try { t.Interrupt(); t.Join(1); }
					catch(ThreadInterruptedException) { }
				}
			}

			StopChecking();
		}

		#endregion

		#region ================== Selection and fixes

		private void OnSelectionChanged()
		{
			if(batchselectioninprogress) return;
			List<ErrorResult> selected = SelectedResults;

			if(selected.Count > 0)
			{
				ErrorResult first = selected[0];
				bool sametype = selected.All(r => r.Buttons == first.Buttons && r.Button1Text == first.Button1Text
					&& r.Button2Text == first.Button2Text && r.Button3Text == first.Button3Text);

				resultinfo.IsEnabled = true;
				if(sametype)
				{
					resultinfo.Text = first.Description;
					fix1.Content = first.Button1Text;
					fix2.Content = first.Button2Text;
					fix3.Content = first.Button3Text;
					fix1.IsVisible = first.Buttons > 0;
					fix2.IsVisible = first.Buttons > 1;
					fix3.IsVisible = first.Buttons > 2;
				}
				else
				{
					resultinfo.Text = "Several types of map analysis results are selected. To display fixes, make sure that only a single result type is selected.";
					fix1.IsVisible = false;
					fix2.IsVisible = false;
					fix3.IsVisible = false;
				}

				RectangleF zoomarea = first.GetZoomArea();
				foreach(ErrorResult result in selected) zoomarea = RectangleF.Union(zoomarea, result.GetZoomArea());
				ClassicMode editmode = General.Editing.Mode as ClassicMode;
				if(editmode != null) editmode.CenterOnArea(zoomarea, 0.6f);

				UpdateTitle();
			}
			else
			{
				ClearSelectedResult();
			}

			General.Interface.RedrawDisplay();
		}

		private void Fix(int number)
		{
			List<ErrorResult> selected = SelectedResults;
			if(selected.Count == 0) return;

			if(running)
			{
				General.ShowWarningMessage("You must stop the analysis before you can make changes to your map!", System.Windows.Forms.MessageBoxButtons.OK);
				return;
			}

			ErrorResult r = selected[0];
			bool done = number == 1 ? r.Button1Click(false) : number == 2 ? r.Button2Click(false) : r.Button3Click(false);
			if(done)
			{
				if(selected.Count > 1) FixSimilarErrors(selected, r, number);
				StartChecking();
			}
			else
			{
				General.Interface.RedrawDisplay();
			}
		}

		private static void FixSimilarErrors(List<ErrorResult> selected, ErrorResult first, int number)
		{
			foreach(ErrorResult r in selected)
			{
				if(r == first || r.GetType() != first.GetType()) continue;
				if(number == 1 && !r.Button1Click(true)) break;
				if(number == 2 && !r.Button2Click(true)) break;
				if(number == 3 && !r.Button3Click(true)) break;
			}
		}

		#endregion

		#region ================== Context menu

		private void ShowAllResults()
		{
			foreach(ErrorResult result in resultslist) result.Hide(false);
			items.Clear();
			items.AddRange(resultslist);
			hiddentresulttypes.Clear();
			RefreshList();
			ClearSelectedResult();
		}

		private void HideSelected()
		{
			List<ErrorResult> tohide = SelectedResults;
			foreach(ErrorResult r in tohide) r.Hide(true);
			items.RemoveAll(r => tohide.Contains(r));
			RefreshList();
			ClearSelectedResult();
		}

		private void HideSelectedTypes()
		{
			HashSet<Type> tohide = new HashSet<Type>(SelectedResults.Select(r => r.GetType()));
			hiddentresulttypes.AddRange(tohide);
			items.RemoveAll(r => tohide.Contains(r.GetType()));
			RefreshList();
			ClearSelectedResult();
		}

		private void ShowOnlySelectedTypes()
		{
			HashSet<Type> toshow = new HashSet<Type>(SelectedResults.Select(r => r.GetType()));
			hiddentresulttypes.Clear();
			foreach(ErrorResult result in items)
				if(!toshow.Contains(result.GetType())) hiddentresulttypes.Add(result.GetType());
			items.RemoveAll(r => !toshow.Contains(r.GetType()));
			RefreshList();
			ClearSelectedResult();
		}

		private void SelectCurrentType()
		{
			HashSet<Type> types = new HashSet<Type>(SelectedResults.Select(r => r.GetType()));
			batchselectioninprogress = true;
			results.Selection.Clear();
			for(int i = 0; i < items.Count; i++)
				if(types.Contains(items[i].GetType())) results.Selection.Select(i);
			batchselectioninprogress = false;
			OnSelectionChanged();
		}

		#endregion
	}
}
