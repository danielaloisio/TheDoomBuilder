// The dialog editor window (UDB's MainForm). Like in UDB this is the frame for the conversations of the DIALOGUE lump: a window with a tree.
// It remembers its position, size and state.
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;

namespace CodeImp.DoomBuilder.USDF
{
	public class MainForm : IDisposable
	{
		private readonly Window window = new Window { Title = "Dialog Editor", Width = 600, Height = 400 };
		private readonly TreeView tree = new TreeView();
		private bool disposed;
		private PixelPoint lastposition;
		private Size lastsize;

		// For the tests
		internal Window Window { get { return window; } }
		internal TreeView Tree { get { return tree; } }

		public bool IsDisposed { get { return disposed; } }

		public MainForm()
		{
			window.Content = tree;
			MemoryStream s = General.Map.GetLumpData("DIALOGUE");
			if(s != null)
			{
				// The conversations are not edited yet
			}

			// The window as it was
			window.Width = General.Settings.ReadPluginSetting("mainwindow.sizewidth", (int)window.Width);
			window.Height = General.Settings.ReadPluginSetting("mainwindow.sizeheight", (int)window.Height);
			window.WindowState = (WindowState)General.Settings.ReadPluginSetting("mainwindow.windowstate", (int)WindowState.Normal);
			window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			int x = General.Settings.ReadPluginSetting("mainwindow.positionx", int.MinValue);
			int y = General.Settings.ReadPluginSetting("mainwindow.positiony", int.MinValue);
			if(x != int.MinValue && y != int.MinValue)
			{
				window.WindowStartupLocation = WindowStartupLocation.Manual;
				window.Position = new PixelPoint(x, y);
			}

			window.PositionChanged += (s2, e) => Remember();
			window.SizeChanged += (s2, e) => Remember();
			window.Closing += (s2, e) => Store();
			window.Closed += (s2, e) => disposed = true;
		}

		private void Remember()
		{
			if(window.WindowState == WindowState.Normal)
			{
				lastposition = window.Position;
				lastsize = new Size(window.Width, window.Height);
			}
		}

		private void Store()
		{
			WindowState state = window.WindowState != WindowState.Minimized ? window.WindowState : WindowState.Normal;
			Remember();
			General.Settings.WritePluginSetting("mainwindow.positionx", lastposition.X);
			General.Settings.WritePluginSetting("mainwindow.positiony", lastposition.Y);
			General.Settings.WritePluginSetting("mainwindow.sizewidth", (int)lastsize.Width);
			General.Settings.WritePluginSetting("mainwindow.sizeheight", (int)lastsize.Height);
			General.Settings.WritePluginSetting("mainwindow.windowstate", (int)state);
			SaveData();
		}

		public void SaveData()
		{
		}

		/// <summary>Shows the window, over the main window when the owner is asked for.</summary>
		public void Show(bool ontop)
		{
			Window main = global::DoomBuilder.UI.DialogHost.Owner == null ? null : global::DoomBuilder.UI.DialogHost.Owner();
			if(ontop && main != null) window.Show(main);
			else window.Show();
		}

		public void Activate() { window.Activate(); }

		public void Dispose()
		{
			if(disposed) return;
			disposed = true;
			window.Close();
		}
	}
}
