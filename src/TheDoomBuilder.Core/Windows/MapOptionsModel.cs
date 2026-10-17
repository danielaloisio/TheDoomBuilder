#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	/// <summary>
	/// The logic of UDB's "Map Options" dialog without any UI, used both for a new map and for the options of the open one:
	/// level name, game configuration, resources and the checks (and warnings) before the change is accepted.
	/// </summary>
	public sealed class MapOptionsModel : IMapOptionsChoices
	{
		private readonly MapOptions options;
		private ConfigurationInfo selectedconfig;

		/// <param name="options">The options to edit.</param>
		/// <param name="newmap">True when creating a map: the last used configuration is preferred and the name follows the configuration.</param>
		public MapOptionsModel(MapOptions options, bool newmap)
		{
			this.options = options;
			NewMap = newmap;

			StrictPatches = options.StrictPatches;
			UseLongTextureNames = options.UseLongTextureNames;
			LevelName = options.CurrentName ?? string.Empty;
			Resources = new DataLocationList(options.Resources);

			// Enabled configurations are offered; a disabled one only when it is the one in use
			List<ConfigurationInfo> offered = General.Configs.Where(c => c.Enabled).ToList();
			ConfigurationInfo chosen = null;
			foreach(ConfigurationInfo info in offered)
			{
				if(newmap && !string.IsNullOrEmpty(General.Settings.LastUsedConfigName) && info.Name == General.Settings.LastUsedConfigName) chosen = info;
				else if(string.Compare(info.Filename, options.ConfigFile, true) == 0) chosen = info;
			}
			if(chosen == null)
			{
				foreach(ConfigurationInfo info in General.Configs.Where(c => !c.Enabled))
				{
					if((newmap && !string.IsNullOrEmpty(General.Settings.LastUsedConfigName) && info.Name == General.Settings.LastUsedConfigName)
						|| string.Compare(info.Filename, options.ConfigFile, true) == 0)
					{
						offered.Add(info);
						chosen = info;
						break;
					}
				}
			}
			Configs = offered;
			if(chosen == null && offered.Count > 0) chosen = offered[0];
			if(chosen != null) SelectedConfig = chosen;
		}

		public bool NewMap { get; private set; }
		public IReadOnlyList<ConfigurationInfo> Configs { get; private set; }
		public IReadOnlyDictionary<string, ScriptConfiguration> ScriptCompilers { get { return General.CompiledScriptConfigs; } }

		public string LevelName { get; set; }
		public bool StrictPatches { get; set; }
		public bool UseLongTextureNames { get; set; }
		public string SelectedScriptCompiler { get; set; }
		public DataLocationList Resources { get; private set; }

		public ConfigurationInfo SelectedConfig
		{
			get { return selectedconfig; }
			set
			{
				selectedconfig = value;
				if(value == null) return;

				// A new map (or one with no name yet) is named as the configuration wants ("MAP01", "E1M1")
				if(NewMap || LevelName.Trim().Length == 0) LevelName = value.DefaultLumpName;

				SelectedScriptCompiler = null;
				if(!string.IsNullOrEmpty(options.ScriptCompiler) && General.CompiledScriptConfigs.ContainsKey(options.ScriptCompiler))
					SelectedScriptCompiler = options.ScriptCompiler;
				else if(!string.IsNullOrEmpty(value.DefaultScriptCompiler) && General.CompiledScriptConfigs.ContainsKey(value.DefaultScriptCompiler))
					SelectedScriptCompiler = value.DefaultScriptCompiler;

				if(!LongTextureNamesAvailable) UseLongTextureNames = false;
			}
		}

		public string ExampleName { get { return selectedconfig != null ? selectedconfig.DefaultLumpName : string.Empty; } }
		public bool ScriptCompilerAvailable { get { return !string.IsNullOrEmpty(SelectedScriptCompiler); } }
		public bool LongTextureNamesAvailable { get { return selectedconfig != null && selectedconfig.Configuration.ReadSetting("longtexturenames", false); } }
		public DataLocationList FixedResources { get { return selectedconfig != null ? selectedconfig.Resources : new DataLocationList(); } }

		/// <summary>Characters a map name may have (the editor refuses the others while typing).</summary>
		public static bool IsAllowedNameChar(char c) { return Lump.MAP_LUMP_NAME_CHARS.IndexOf(char.ToUpperInvariant(c)) >= 0; }

		/// <summary>
		/// Why the options cannot be accepted (null when they can). <paramref name="warnings"/> are questions the user may still
		/// answer "continue" to, each asked in order.
		/// </summary>
		public string Validate(out List<string> warnings)
		{
			warnings = new List<string>();

			if(selectedconfig == null) return "Please select a game configuration to use for editing your map.";
			if(LevelName.Length == 0) return "Please enter a level name for your map.";
			if(!selectedconfig.ValidateMapName(LevelName.ToUpperInvariant())) return "Chosen map name conflicts with a lump name defined for current map format.\n";
			if(!Resources.IsValid()) return "Cannot " + (NewMap ? "create map" : "change map settings") + ": at least one resource doesn't exist!";

			if(!NewMap && General.Map != null)
			{
				// Renaming onto a lump that already exists in the source WAD would leave two lumps of that name in it
				if(LevelName != options.CurrentName && !string.IsNullOrEmpty(General.Map.FilePathName) && File.Exists(General.Map.FilePathName))
				{
					using(WAD source = new WAD(General.Map.FilePathName, true))
					{
						if(source.FindLumpIndex(LevelName) > -1)
							warnings.Add("The map name \"" + LevelName + "\" is already in use by another map or data lump in the source WAD file. Saving your map with this name will cause conflicting data lumps in the WAD file. Do you want to continue?");
					}
				}

				// Another file format than the map was made for
				if(!string.IsNullOrEmpty(options.ConfigFile) && selectedconfig.Filename != options.ConfigFile
					&& selectedconfig.Configuration.ReadSetting("formatinterface", "") != General.Map.Config.FormatInterface)
					warnings.Add("The game configuration you selected uses a different file format than your current map. Because your map was not designed for this format it may cause the map to work incorrectly in the game. Do you want to continue?");
			}

			return null;
		}

		/// <summary>Writes the choices into the options. Call after <see cref="Validate"/> succeeded and the warnings were accepted.</summary>
		public MapOptions Apply()
		{
			if(NewMap) General.Settings.LastUsedConfigName = selectedconfig.Name;

			if(!NewMap && General.Map != null)
			{
				// A map that was never saved takes its file title from the name
				if(LevelName != options.CurrentName && string.IsNullOrEmpty(General.Map.FilePathName))
					General.Map.FileTitle = LevelName + ".wad";

				// Switching to another file format changes the map
				if(!string.IsNullOrEmpty(options.ConfigFile) && selectedconfig.Filename != options.ConfigFile
					&& selectedconfig.Configuration.ReadSetting("formatinterface", "") != General.Map.Config.FormatInterface)
					General.Map.IsChanged = true;
			}

			options.ClearResources();
			options.ConfigFile = selectedconfig.Filename;
			options.CurrentName = LevelName.Trim().ToUpperInvariant();
			options.StrictPatches = StrictPatches;
			options.CopyResources(Resources);
			if(ScriptCompilerAvailable) options.ScriptCompiler = SelectedScriptCompiler;
			if(LongTextureNamesAvailable) options.UseLongTextureNames = UseLongTextureNames;
			return options;
		}
	}
}
