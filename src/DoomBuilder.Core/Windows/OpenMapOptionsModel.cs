#region ================== Namespaces

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The logic of UDB's "Open Map" dialog without any UI: given a WAD, which game configuration fits it, which maps it contains
	/// for that configuration, the resources (IWAD...) to load with it and the checks that must pass before the map is opened.
	/// The Avalonia dialog is a view over this class (it was OpenMapOptionsForm's code-behind in WinForms).
	/// </summary>
	public sealed class OpenMapOptionsModel : IMapOptionsChoices, IDisposable
	{
		#region ================== Variables

		private static readonly Regex episodemapregex = new Regex("^E[1-9]M[1-9]$");
		private static readonly Regex noepisodemapregex = new Regex("^MAP[0-9][0-9]$");

		private readonly string filepathname;
		private readonly MapOptions initial;
		private WAD wadfile;
		private Configuration mapsettings;
		private ConfigurationInfo selectedconfig;
		private readonly List<string> maps = new List<string>();

		#endregion

		#region ================== Properties

		public string FilePathName { get { return filepathname; } }

		/// <summary>Why the WAD could not be read (null when it could).</summary>
		public string LoadError { get; private set; }

		/// <summary>True when the WAD has no map for any game configuration: nothing to open.</summary>
		public bool NothingToOpen { get; private set; }

		public bool IsIwad { get; private set; }

		/// <summary>All game configurations, enabled or not (the user can pick a disabled one when it is the one that fits).</summary>
		public IReadOnlyList<ConfigurationInfo> Configs { get; private set; }

		/// <summary>Script compilers a map can use, by key (the name stored in the map options).</summary>
		public IReadOnlyDictionary<string, ScriptConfiguration> ScriptCompilers { get { return General.CompiledScriptConfigs; } }

		public ConfigurationInfo SelectedConfig
		{
			get { return selectedconfig; }
			set
			{
				selectedconfig = value;
				RebuildMaps();
				SelectDefaultScriptCompiler();
			}
		}

		/// <summary>The maps of the WAD that match the selected game configuration, sorted.</summary>
		public IReadOnlyList<string> Maps { get { return maps; } }

		public string SelectedMap { get; private set; }

		/// <summary>Script compiler key chosen for the selected map, or null when the configuration has none.</summary>
		public string SelectedScriptCompiler { get; set; }

		public bool StrictPatches { get; set; }

		/// <summary>Only configurations that support long texture names allow this.</summary>
		public bool LongTextureNamesAvailable { get { return selectedconfig != null && selectedconfig.Configuration.ReadSetting("longtexturenames", false); } }
		public bool UseLongTextureNames { get; set; }

		/// <summary>The resources the game configuration always loads (its IWAD...): shown but not editable here.</summary>
		public DataLocationList FixedResources { get { return selectedconfig != null ? selectedconfig.Resources : new DataLocationList(); } }

		/// <summary>Extra resources for this map (the user's own wads...). Editable.</summary>
		public DataLocationList Resources { get; private set; } = new DataLocationList();

		#endregion

		#region ================== Constructor / Disposer

		/// <param name="initial">Options to start from (opening again from the options of an open map), or null.</param>
		public OpenMapOptionsModel(string filepathname, MapOptions initial)
		{
			this.filepathname = filepathname;
			this.initial = initial;
			Load();
		}

		public void Dispose()
		{
			if(wadfile != null) { wadfile.Dispose(); wadfile = null; }
		}

		#endregion

		#region ================== Loading

		private void Load()
		{
			if(!File.Exists(filepathname))
			{
				LoadError = "Could not open the WAD file: The file does not exist.";
				return;
			}

			try
			{
				wadfile = new WAD(filepathname, true);
			}
			catch(Exception)
			{
				LoadError = "Could not open the WAD file for reading. Please make sure the file you selected is valid and is not in use by any other application.";
				if(wadfile != null) wadfile.Dispose();
				wadfile = null;
				return;
			}

			IsIwad = wadfile.IsIWAD;
			Configs = General.Configs.ToList();

			// Settings stored with the map the last time
			string dbsfile = Path.ChangeExtension(filepathname, ".dbs");
			if(File.Exists(dbsfile))
				try { mapsettings = new Configuration(dbsfile, true); }
				catch(Exception) { mapsettings = new Configuration(true); }
			else
				mapsettings = new Configuration(true);

			string gameconfig;
			if(initial != null)
			{
				StrictPatches = initial.StrictPatches;
				gameconfig = initial.ConfigFile;
			}
			else
			{
				StrictPatches = mapsettings.ReadSetting("strictpatches", false);
				gameconfig = mapsettings.ReadSetting("gameconfig", "");
			}

			// 1. the configuration stored with the map, 2. then the best fit for what the WAD contains, in the order UDB preferred:
			// enabled with resources, disabled with resources, enabled without resources, disabled without resources
			ConfigurationInfo chosen = Configs.FirstOrDefault(c => c.Filename == gameconfig);
			if(chosen == null) chosen = FirstMatch(true, true);
			if(chosen == null) chosen = FirstMatch(false, true);
			if(chosen == null) chosen = FirstMatch(true, false);
			if(chosen == null) chosen = FirstMatch(false, false);

			if(chosen != null)
			{
				SelectedConfig = chosen;
				if(initial != null)
				{
					Resources = new DataLocationList(initial.Resources);
					UseLongTextureNames = LongTextureNamesAvailable && initial.UseLongTextureNames;
					if(!string.IsNullOrEmpty(initial.ScriptCompiler) && General.CompiledScriptConfigs.ContainsKey(initial.ScriptCompiler))
						SelectedScriptCompiler = initial.ScriptCompiler;
				}
			}

			NothingToOpen = (chosen == null || maps.Count == 0);
		}

		private ConfigurationInfo FirstMatch(bool enabled, bool withresources)
		{
			return Configs.FirstOrDefault(c => c.Enabled == enabled && (c.Resources.Count > 0) == withresources && MatchConfiguration(c.Configuration, wadfile));
		}

		#endregion

		#region ================== Maps

		// Does this WAD contain maps in the format the configuration describes?
		private static bool MatchConfiguration(Configuration cfg, WAD wadfile)
		{
			string mapnameformat = cfg.ReadSetting("mapnameformat", "");
			IDictionary maplumpnames = cfg.ReadSetting("maplumpnames", new Hashtable());
			int lumpsrequired = CountRequiredLumps(cfg, maplumpnames);

			for(int scanindex = 0; scanindex < (wadfile.Lumps.Count - 1); scanindex++)
			{
				if(MapNameFormatMismatch(mapnameformat, wadfile.Lumps[scanindex].Name)) return false;

				// A map header is a lump that is not itself a map lump, followed by the map lumps
				if(!maplumpnames.Contains(wadfile.Lumps[scanindex].Name))
				{
					int lumpsfound = CountMapLumps(cfg, maplumpnames, wadfile, scanindex);
					if(lumpsfound >= lumpsrequired) return true;
				}
			}

			return false;
		}

		private static int CountRequiredLumps(Configuration cfg, IDictionary maplumpnames)
		{
			int required = 0;
			foreach(DictionaryEntry ml in maplumpnames)
				if(ml.Key.ToString() != MapManager.CONFIG_MAP_HEADER && cfg.ReadSetting("maplumpnames." + ml.Key + ".required", false))
					required++;
			return required;
		}

		// How many required map lumps follow the header at scanindex (-1 when a forbidden lump is among them)
		private static int CountMapLumps(Configuration cfg, IDictionary maplumpnames, WAD wadfile, int scanindex)
		{
			int lumpsfound = 0;
			int checkoffset = 1;
			while(((scanindex + checkoffset) < wadfile.Lumps.Count) && maplumpnames.Contains(wadfile.Lumps[scanindex + checkoffset].Name))
			{
				string lumpname = wadfile.Lumps[scanindex + checkoffset].Name;
				if(cfg.ReadSetting("maplumpnames." + lumpname + ".forbidden", false)) return -1;
				if(cfg.ReadSetting("maplumpnames." + lumpname + ".required", false)) lumpsfound++;
				checkoffset++;
			}
			return lumpsfound;
		}

		private static bool MapNameFormatMismatch(string mapnameformat, string lumpname)
		{
			return (mapnameformat == MapManager.CONFIG_MAP_NAME_FORMAT_NO_EPISODE && episodemapregex.IsMatch(lumpname))
				|| (mapnameformat == MapManager.CONFIG_MAP_NAME_FORMAT_EPISODE && noepisodemapregex.IsMatch(lumpname));
		}

		// The maps of the WAD for the selected game configuration
		private void RebuildMaps()
		{
			string previous = SelectedMap;
			maps.Clear();
			if(selectedconfig == null || wadfile == null) { SelectedMap = null; return; }

			Configuration cfg = selectedconfig.Configuration;
			IDictionary maplumpnames = cfg.ReadSetting("maplumpnames", new Hashtable());
			int lumpsrequired = CountRequiredLumps(cfg, maplumpnames);

			for(int scanindex = 0; scanindex < (wadfile.Lumps.Count - 1); scanindex++)
			{
				if(maplumpnames.Contains(wadfile.Lumps[scanindex].Name)) continue;

				int lumpsfound = CountMapLumps(cfg, maplumpnames, wadfile, scanindex);
				string mapname = wadfile.Lumps[scanindex].Name;
				if((lumpsfound >= lumpsrequired) && !maps.Contains(mapname)) maps.Add(mapname);
			}

			maps.Sort(StringComparer.CurrentCulture);

			// Keep the selection when the new list still has it, else the first map (as the list view did)
			SelectedMap = maps.Contains(previous) ? previous : (maps.Count > 0 ? maps[0] : null);
			LoadMapSettings();
		}

		/// <summary>Chooses the map to open; its stored resources and script compiler come along.</summary>
		public void SelectMap(string mapname)
		{
			if(!maps.Contains(mapname)) throw new ArgumentException("The WAD has no map \"" + mapname + "\" for this configuration.", "mapname");
			SelectedMap = mapname;
			LoadMapSettings();
		}

		// A map remembers its own resources and script compiler in the .dbs file next to the WAD
		private void LoadMapSettings()
		{
			SelectDefaultScriptCompiler();
			if(SelectedMap == null) return;

			MapOptions stored = new MapOptions(mapsettings, SelectedMap, LongTextureNamesAvailable);
			foreach(DataLocation dl in new DataLocationList(mapsettings, "maps." + SelectedMap + ".resources"))
				if(!Resources.Contains(dl)) Resources.Add(dl);

			if(!string.IsNullOrEmpty(stored.ScriptCompiler) && General.CompiledScriptConfigs.ContainsKey(stored.ScriptCompiler))
				SelectedScriptCompiler = stored.ScriptCompiler;
		}

		// The configuration's own default compiler, when the map has not chosen one (null when there are no maps to compile for)
		private void SelectDefaultScriptCompiler()
		{
			if(selectedconfig != null && maps.Count > 0 && !string.IsNullOrEmpty(selectedconfig.DefaultScriptCompiler)
				&& General.CompiledScriptConfigs.ContainsKey(selectedconfig.DefaultScriptCompiler))
				SelectedScriptCompiler = selectedconfig.DefaultScriptCompiler;
			else if(maps.Count == 0)
				SelectedScriptCompiler = null;
		}

		#endregion

		#region ================== Applying

		/// <summary>
		/// The reason the map cannot be opened with the current choices (what the Open button warned about), or null when it can.
		/// <paramref name="confirmation"/> is a question the user may still answer "continue" to (null when there is none).
		/// </summary>
		public string Validate(out string confirmation)
		{
			confirmation = null;

			if(selectedconfig == null) return "Please select a game configuration to use for editing your map.";
			if(SelectedMap == null) return "Please select a map to load for editing.";

			DataLocationList all = Resources;
			if(!all.IsValid()) return "Cannot open map: at least one resource doesn't exist!";

			if(!selectedconfig.ValidateMapName(SelectedMap.ToUpperInvariant()))
				return "Selected map name conflicts with a lump name defined for current map format.\nPlease rename the map and try again.";

			if(!IsIwad && Resources.Count == 0 && selectedconfig.Resources.Count == 0)
				confirmation = "You are about to load a map without selecting any resources. Textures, flats and sprites may not be shown correctly or may not show up at all. Do you want to continue?";

			return null;
		}

		/// <summary>The script compiler choice only exists for maps whose configuration (or stored settings) name one.</summary>
		public bool ScriptCompilerAvailable { get { return !string.IsNullOrEmpty(SelectedScriptCompiler); } }

		/// <summary>The options to open the map with. Call after <see cref="Validate"/> succeeded.</summary>
		public MapOptions BuildOptions()
		{
			MapOptions options = new MapOptions(mapsettings, SelectedMap, LongTextureNamesAvailable);
			options.ClearResources();
			options.ConfigFile = selectedconfig.Filename;
			options.CurrentName = SelectedMap;
			options.StrictPatches = StrictPatches;
			options.CopyResources(Resources);

			if(!string.IsNullOrEmpty(SelectedScriptCompiler)) options.ScriptCompiler = SelectedScriptCompiler;
			if(LongTextureNamesAvailable) options.UseLongTextureNames = UseLongTextureNames;
			return options;
		}

		#endregion
	}
}
