// The window that runs a script: a progress bar, the status and the log of the script, and a button to cancel it (or to close the window when
// it is done). UDB's ScriptRunnerForm. It stays invisible while the script runs fast (it is only shown when the script logs something, sets the
// progress, or takes more than a second) and closes by itself when the script is done without having shown itself.
//
// The script runs on its own thread; everything that touches the screen goes through the UI thread (InvokePaused / RunAction, which the script
// thread calls). An Avalonia window can only be shown once, so each ShowDialog makes a new one.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using DoomBuilder.UI;
using AvButton = Avalonia.Controls.Button;

namespace CodeImp.DoomBuilder.UDBScript
{
	public class ScriptRunnerForm
	{
		#region ================== Variables

		private Window window;
		// (made again for every window: a control can only be in one window)
		private TextBlock lbStatus = new TextBlock();
		private ProgressBar progressbar = new ProgressBar { Minimum = 0, Maximum = 100, MinWidth = 440 };
		private TextBox tbLog = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 60, MinWidth = 440 };
		private AvButton btnAction = new AvButton { MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };

		private CancellationTokenSource cancellationtokensource;
		private bool running;
		private double runningseconds;
		private bool autoclose;
		private Stopwatch stopwatch = new Stopwatch();
		private DispatcherTimer timer;
		private bool visible;

		#endregion

		#region ================== Properties (for the tests)

		internal Window Window { get { return window; } }
		internal bool IsRunning { get { return running; } }
		internal bool IsShown { get { return visible; } }
		internal string Status { get { return lbStatus.Text; } }
		internal string Log { get { return tbLog.Text ?? ""; } }
		internal ProgressBar Progress { get { return progressbar; } }
		internal AvButton ActionButton { get { return btnAction; } }

		#endregion

		#region ================== Methods

		/// <summary>Runs a method on the UI thread with the clock of the script stopped (a dialog the script asked for does not count as run time).</summary>
		public object InvokePaused(Delegate method)
		{
			if(!Dispatcher.UIThread.CheckAccess())
				return Dispatcher.UIThread.Invoke(() => InvokePaused(method));

			stopwatch.Stop();
			try
			{
				return method.DynamicInvoke();
			}
			catch(TargetInvocationException e)
			{
				// What the method threw is what the script has to see (UserScriptAbortException...)
				ExceptionDispatchInfo.Capture(e.InnerException).Throw();
				return null;
			}
			finally
			{
				stopwatch.Start();
			}
		}

		/// <summary>Runs an action on the UI thread.</summary>
		public void RunAction(Action action)
		{
			if(Dispatcher.UIThread.CheckAccess()) action();
			else Dispatcher.UIThread.Invoke(action);
		}

		private void SetProgress(int value)
		{
			progressbar.IsIndeterminate = false;
			progressbar.Value = Math.Max(progressbar.Minimum, Math.Min(progressbar.Maximum, value));
			MakeVisible();
		}

		private void SetProgressStatus(string status)
		{
			lbStatus.Text = status;
		}

		private void LogText(string text)
		{
			autoclose = false;
			if(!string.IsNullOrEmpty(tbLog.Text)) tbLog.Text += Environment.NewLine;
			tbLog.Text += text;
			MakeVisible();
		}

		private async Task RunScript(CancellationToken cancellationtoken)
		{
			Progress<int> progress = new Progress<int>(SetProgress);
			Progress<string> status = new Progress<string>(SetProgressStatus);
			Progress<string> log = new Progress<string>(LogText);

			running = true;
			bool started = false;
			try
			{
				started = BuilderPlug.Me.ScriptRunner.PreRun(cancellationtoken, progress, status, log);
				if(started)
				{
					await Task.Run(() => BuilderPlug.Me.ScriptRunner.Run());
					stopwatch.Stop();
				}
			}
			catch(Exception ex)
			{
				stopwatch.Stop();
				BuilderPlug.Me.ScriptRunner.HandleExceptions(ex);
			}

			if(started) BuilderPlug.Me.ScriptRunner.PostRun();
			running = false;

			window.Title = "Script finished";
			lbStatus.Text = "Script finished. Runtime: " + BuilderPlug.Me.ScriptRunner.GetRuntimeString();
			btnAction.Content = "Close";
			btnAction.IsEnabled = true;
			SetProgress(0);

			if(autoclose || !started)
			{
				MakeInvisible();
				Finish();
			}
		}

		private void MakeVisible()
		{
			if(window != null) window.Opacity = 1.0;
			visible = true;
			btnAction.IsEnabled = true;
		}

		private void MakeInvisible()
		{
			if(window != null) window.Opacity = 0.0;
			visible = false;
		}

		private void Finish()
		{
			if(timer != null) timer.Stop();
			if(window != null) window.Close();
		}

		/// <summary>Runs the script of the plugin in a window and returns when it is done and the window is closed.</summary>
		public void ShowDialog()
		{
			lbStatus = new TextBlock();
			progressbar = new ProgressBar { Minimum = 0, Maximum = 100, MinWidth = 440 };
			tbLog = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 60, MinWidth = 440 };
			btnAction = new AvButton { MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
			window = new Window { Title = "Running script", SizeToContent = SizeToContent.WidthAndHeight, CanResize = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
			var layout = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
			layout.Children.Add(lbStatus);
			layout.Children.Add(progressbar);
			layout.Children.Add(tbLog);
			btnAction.HorizontalAlignment = HorizontalAlignment.Right;
			layout.Children.Add(btnAction);
			window.Content = layout;

			// The window is shown, then the script starts (UDB's Shown event)
			cancellationtokensource = new CancellationTokenSource();
			autoclose = true;
			runningseconds = 0;
			progressbar.Value = 0;
			progressbar.IsIndeterminate = true;
			lbStatus.Text = "Running script...";
			btnAction.Content = "Cancel";
			btnAction.IsEnabled = false;
			tbLog.Text = "";
			stopwatch = new Stopwatch();
			stopwatch.Start();
			MakeInvisible();

			btnAction.Click += btnAction_Click;
			timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
			timer.Tick += timerShow_Tick;
			window.Opened += async (s, e) =>
			{
				timer.Start();
				await RunScript(cancellationtokensource.Token);
			};
			window.Closing += (s, e) =>
			{
				// The close button of the window while the script runs is a Cancel
				if(running) { e.Cancel = true; btnAction_Click(null, EventArgs.Empty); }
			};
			window.Closed += (s, e) => { timer.Stop(); };

			DialogHost.ShowModal(window);
		}

		#endregion

		#region ================== Events

		private void btnAction_Click(object sender, EventArgs e)
		{
			if(running)
			{
				btnAction.IsEnabled = false;
				cancellationtokensource.Cancel();
			}
			else
			{
				MakeInvisible();
				Finish();
			}
		}

		private void timerShow_Tick(object sender, EventArgs e)
		{
			// A script that takes long shows its window
			if(!visible && stopwatch.ElapsedMilliseconds > 1000) MakeVisible();

			double newrunningsecods = Math.Floor(stopwatch.Elapsed.TotalSeconds);
			if(newrunningsecods > runningseconds)
			{
				runningseconds = newrunningsecods;
				window.Title = "Running script (" + string.Format("{0:D2}:{1:D2}:{2:D2}", stopwatch.Elapsed.Hours, stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds) + ")";
			}
		}

		#endregion
	}
}
