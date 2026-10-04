using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The "recent maps" list of the File menu: most recent first, no duplicates (paths compare case-insensitively, as the
	/// user's file systems are mostly case-insensitive), kept in the program settings as recentfiles.file0, file1...
	/// </summary>
	public sealed class RecentFiles
	{
		private readonly List<string> files = new List<string>();

		public IReadOnlyList<string> Files { get { return files; } }
		public int Maximum { get { return General.Settings.MaxRecentFiles; } }

		/// <summary>Reads the list from the settings, dropping files that no longer exist.</summary>
		public void Load()
		{
			files.Clear();
			for(int i = 0; i < Maximum; i++)
			{
				string filename = General.Settings.ReadSetting("recentfiles.file" + i, "");
				if(!string.IsNullOrEmpty(filename) && File.Exists(filename) && !Contains(filename)) files.Add(filename);
			}
		}

		private bool Contains(string filename)
		{
			return files.Any(f => string.Compare(f, filename, true) == 0);
		}

		/// <summary>Puts a file at the top (moving it up when it was already listed) and saves.</summary>
		public void Add(string filename)
		{
			if(string.IsNullOrEmpty(filename)) return;

			files.RemoveAll(f => string.Compare(f, filename, true) == 0);
			files.Insert(0, filename);
			if(files.Count > Maximum) files.RemoveRange(Maximum, files.Count - Maximum);
			Save();
		}

		public void Save()
		{
			for(int i = 0; i < Maximum; i++)
				General.Settings.WriteSetting("recentfiles.file" + i, i < files.Count ? files[i] : "");
			General.SaveSettings();
		}

		/// <summary>
		/// The file as written in a menu: the whole path when it fits, else the start and the end with "..." in between.
		/// </summary>
		public static string MenuText(string filename, int maxlength = 60)
		{
			if(filename.Length <= maxlength) return filename;
			int tail = maxlength - 6;
			return filename.Substring(0, 3) + "..." + filename.Substring(filename.Length - tail, tail);
		}

		/// <summary>
		/// The exact path of a listed file: the case may differ when it was renamed. Null when it cannot be found any more.
		/// </summary>
		public static string FindExistingFile(string filename)
		{
			string folder = Path.GetDirectoryName(filename);
			if(string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;

			string[] possible = Directory.GetFiles(folder, Path.GetFileName(filename));
			if(possible.Contains(filename)) return filename;
			return possible.Length > 0 ? possible[0] : null;
		}
	}
}
