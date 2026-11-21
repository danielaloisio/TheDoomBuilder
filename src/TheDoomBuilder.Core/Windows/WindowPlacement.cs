using System;
using System.Collections.Generic;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// Where the main window was last: position, size and whether it was maximized. Pure data, saved in the program settings
	/// ("mainwindow.*"); the shell applies it. <see cref="FitTo"/> keeps a window that was on a monitor which is gone reachable.
	/// </summary>
	public sealed class WindowPlacement
	{
		public const int MinWidth = 640;
		public const int MinHeight = 400;

		public int X, Y, Width, Height;
		public bool Maximized;

		public WindowPlacement(int x, int y, int width, int height, bool maximized)
		{
			X = x; Y = y; Width = width; Height = height; Maximized = maximized;
		}

		/// <summary>The saved placement, or null when none was saved (first run) or it is unusable.</summary>
		public static WindowPlacement Load()
		{
			if(General.Settings == null) return null;
			int width = General.Settings.ReadSetting("mainwindow.width", 0);
			int height = General.Settings.ReadSetting("mainwindow.height", 0);
			if(width < MinWidth || height < MinHeight) return null;
			return new WindowPlacement(General.Settings.ReadSetting("mainwindow.x", 0), General.Settings.ReadSetting("mainwindow.y", 0),
									   width, height, General.Settings.ReadSetting("mainwindow.maximized", false));
		}

		/// <summary>Stores this placement. A maximized window keeps the size it had before (the caller passes the restored bounds).</summary>
		public void Save()
		{
			if(General.Settings == null) return;
			General.Settings.WriteSetting("mainwindow.x", X);
			General.Settings.WriteSetting("mainwindow.y", Y);
			General.Settings.WriteSetting("mainwindow.width", Width);
			General.Settings.WriteSetting("mainwindow.height", Height);
			General.Settings.WriteSetting("mainwindow.maximized", Maximized);
		}

		/// <summary>
		/// Moves and shrinks the placement so that it lies on one of the screens (x, y, width, height of each work area).
		/// Without screens it stays as it is. A window whose title bar would be out of sight is moved onto the nearest screen.
		/// </summary>
		public WindowPlacement FitTo(IList<int[]> screens)
		{
			if(screens == null || screens.Count == 0) return this;

			// The screen that holds most of the window's top-left area, or else the first one
			int[] best = screens[0];
			long bestoverlap = -1;
			foreach(int[] s in screens)
			{
				long ox = Math.Max(0, Math.Min(X + Width, s[0] + s[2]) - Math.Max(X, s[0]));
				long oy = Math.Max(0, Math.Min(Y + Height, s[1] + s[3]) - Math.Max(Y, s[1]));
				if(ox * oy > bestoverlap) { bestoverlap = ox * oy; best = s; }
			}

			int width = Math.Max(MinWidth, Math.Min(Width, best[2]));
			int height = Math.Max(MinHeight, Math.Min(Height, best[3]));
			int x = Math.Max(best[0], Math.Min(X, best[0] + best[2] - width));
			int y = Math.Max(best[1], Math.Min(Y, best[1] + best[3] - height));
			return new WindowPlacement(x, y, width, height, Maximized);
		}
	}
}
